using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using StackExchange.Redis;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConcurrencyDemo
{
    /// <summary>
    /// 库存同步后台服务。
    /// <para>
    /// 核心职责：
    /// 1. [生产者] 接收 API 请求，将扣减消息发布到 RabbitMQ。
    /// 2. [消费者] 监听 RabbitMQ，通过内存 Channel 缓冲，实现批量写入 SQLite。
    /// </para>
    /// </summary>
    public class StockSyncService : BackgroundService
    {
        // 内部缓冲队列，用于将 RabbitMQ 的单条推送转换为批量处理
        // 包含 DeliveryTag 用于后续手动 Ack
        private readonly Channel<(int Id, int Qty, string TransactionId, ulong DeliveryTag)> _channel;
        
        private readonly string _connectionString;
        private readonly IDatabase _redisDb;
        private readonly IConnection _rabbitConnection;
        private IModel _rabbitChannel;
        private readonly ILogger<StockSyncService> _logger;

        private const string QueueName = "stock_deduct_queue";

        // Prometheus Metrics
        private static readonly Counter StockConsumedTotal = Metrics.CreateCounter(
            "stock_consumed_total", 
            "Total amount of stock deducted from database", 
            new CounterConfiguration { LabelNames = new[] { "material_id" } });

        private static readonly Counter StockAddedTotal = Metrics.CreateCounter(
            "stock_added_total", 
            "Total amount of stock added to database", 
            new CounterConfiguration { LabelNames = new[] { "material_id" } });

        private static readonly Counter StockWriteErrorsTotal = Metrics.CreateCounter(
            "stock_write_errors_total", 
            "Total number of database write errors");

        public StockSyncService(
            string connectionString, 
            IConnectionMultiplexer redis,
            IConnection rabbitConnection,
            ILogger<StockSyncService> logger)
        {
            _connectionString = connectionString;
            _redisDb = redis.GetDatabase();
            _rabbitConnection = rabbitConnection;
            _logger = logger;
            // Unbounded channel for internal buffering
            _channel = Channel.CreateUnbounded<(int, int, string, ulong)>();
            
            InitializeRabbitMq();
        }

        private void InitializeRabbitMq()
        {
            try
            {
                _rabbitChannel = _rabbitConnection.CreateModel();
                _rabbitChannel.QueueDeclare(
                    queue: QueueName,
                    durable: true,      // 持久化队列
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                    
                // 设置 PrefetchCount，控制未确认消息的数量
                // 这对于批量处理很重要，保证我们能取到足够多的消息进行批量入库
                _rabbitChannel.BasicQos(prefetchSize: 0, prefetchCount: 200, global: false);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to initialize RabbitMQ channel/queue");
                throw;
            }
        }

        /// <summary>
        /// 生产者接口：将扣减请求发布到 RabbitMQ。
        /// </summary>
        public void Enqueue(int materialId, int qty, string transactionId)
        {
            // 原方法名保留为 Enqueue 以兼容现有代码，但逻辑改为 Publish
            try
            {
                var message = new StockMessage { Id = materialId, Qty = qty, TransactionId = transactionId };
                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                var properties = _rabbitChannel.CreateBasicProperties();
                properties.Persistent = true; // 消息持久化

                lock (_rabbitChannel) // IModel 不是线程安全的
                {
                    _rabbitChannel.BasicPublish(
                        exchange: "",
                        routingKey: QueueName,
                        basicProperties: properties,
                        body: body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to RabbitMQ");
                // 在生产环境中，这里可能需要降级策略（如写入本地文件或内存重试）
                // 暂时抛出异常让上层感知
                throw;
            }
        }

        /// <summary>
        /// 消费者主循环。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 启动 RabbitMQ 消费者
            var consumer = new AsyncEventingBasicConsumer(_rabbitChannel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var msg = JsonSerializer.Deserialize<StockMessage>(json);
                    
                    if (msg != null)
                    {
                        // 写入内存 Channel 进行缓冲，带上 DeliveryTag 用于后续 Ack
                        // TryWrite 是同步的，如果 Channel 满了（虽然这里是 Unbounded）会返回 false
                        // 对于 Unbounded Channel，TryWrite 总是成功
                        _channel.Writer.TryWrite((msg.Id, msg.Qty, msg.TransactionId, ea.DeliveryTag));
                    }
                    else
                    {
                        // 格式错误，直接拒绝（不重回队列）
                        lock (_rabbitChannel) _rabbitChannel.BasicReject(ea.DeliveryTag, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RabbitMQ message");
                    // 处理失败，重回队列
                    lock (_rabbitChannel) _rabbitChannel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
                await Task.CompletedTask;
            };

            lock (_rabbitChannel)
            {
                _rabbitChannel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            }

            var validBatch = new Dictionary<int, int>();
            var pendingItems = new List<(int Id, int Qty, string TransactionId, ulong DeliveryTag)>();

            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. 等待队列中有数据
                if (!await _channel.Reader.WaitToReadAsync(stoppingToken)) break;

                validBatch.Clear();
                pendingItems.Clear();

                // 2. 批量读取：尽可能多地读取当前积压的消息，形成一个 Batch
                while (_channel.Reader.TryRead(out var item))
                {
                    pendingItems.Add(item);
                    // 限制每批次最大数量，与 PrefetchCount 配合
                    if (pendingItems.Count >= 100) break;
                }

                if (pendingItems.Count == 0) continue;

                // 3. 幂等性检查 & 批量写入 (基于 SQL Server 本地事务表)
                // 放弃 Redis 去重，改用 DB 强一致性去重，防止 Redis 写成功但 DB 写失败导致的消息丢失
                bool dbSuccess = true;
                if (pendingItems.Count > 0)
                {
                    using var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync(stoppingToken);
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        // 3.1 筛选出新消息
                        // 查询已存在的 TransactionId
                        var tids = pendingItems.Select(x => x.TransactionId).ToList();
                        var existingTids = new HashSet<string>();
                        
                        // 构建 IN 查询
                        var commandCheck = connection.CreateCommand();
                        commandCheck.Transaction = transaction;
                        var paramNames = new List<string>();
                        for (int i = 0; i < tids.Count; i++)
                        {
                            string pName = $"@p{i}";
                            paramNames.Add(pName);
                            commandCheck.Parameters.AddWithValue(pName, tids[i]);
                        }
                        commandCheck.CommandText = $"SELECT TransactionId FROM ProcessedTransactions WHERE TransactionId IN ({string.Join(",", paramNames)})";
                        
                        using (var reader = await commandCheck.ExecuteReaderAsync(stoppingToken))
                        {
                            while (await reader.ReadAsync(stoppingToken))
                            {
                                existingTids.Add(reader.GetString(0));
                            }
                        }

                        // 3.2 记录新消息的 TransactionId 并聚合扣减量
                        validBatch.Clear();
                        var newItems = new List<(int Id, int Qty, string TransactionId)>();
                        var currentBatchTids = new HashSet<string>(); // 用于本批次内去重

                        foreach (var item in pendingItems)
                        {
                            // 1. 检查是否在数据库已存在
                            if (existingTids.Contains(item.TransactionId))
                            {
                                _logger.LogWarning("Duplicate transaction detected (DB): {Tid}", item.TransactionId);
                                continue;
                            }

                            // 2. 检查是否在本批次已处理 (防止 RabbitMQ 推送重复消息在同一批次中)
                            if (currentBatchTids.Contains(item.TransactionId))
                            {
                                _logger.LogWarning("Duplicate transaction detected (Batch): {Tid}", item.TransactionId);
                                continue;
                            }

                            currentBatchTids.Add(item.TransactionId);
                            newItems.Add((item.Id, item.Qty, item.TransactionId));

                            // 聚合库存扣减
                            if (validBatch.ContainsKey(item.Id))
                                validBatch[item.Id] += item.Qty;
                            else
                                validBatch[item.Id] = item.Qty;
                        }

                        // 3.3 批量插入新 Tid (防止下次重复)
                        if (newItems.Count > 0)
                        {
                            foreach (var item in newItems)
                            {
                                var cmdInsert = connection.CreateCommand();
                                cmdInsert.Transaction = transaction;
                                cmdInsert.CommandText = "INSERT INTO ProcessedTransactions (TransactionId) VALUES (@tid)";
                                cmdInsert.Parameters.AddWithValue("@tid", item.TransactionId);
                                await cmdInsert.ExecuteNonQueryAsync(stoppingToken);
                            }

                            // 3.4 批量更新库存
                            foreach (var kvp in validBatch)
                            {
                                var cmdUpdate = connection.CreateCommand();
                                cmdUpdate.Transaction = transaction;
                                // 增加 Qty >= @qty 检查，防止负库存
                                cmdUpdate.CommandText = "UPDATE MaterialStock SET Qty = Qty - @qty WHERE Id = @id AND Qty >= @qty";
                                cmdUpdate.Parameters.AddWithValue("@qty", kvp.Value);
                                cmdUpdate.Parameters.AddWithValue("@id", kvp.Key);
                                int rows = await cmdUpdate.ExecuteNonQueryAsync(stoppingToken);
                                
                                if (rows > 0)
                                {
                                    if (kvp.Value > 0)
                                        StockConsumedTotal.WithLabels(kvp.Key.ToString()).Inc(kvp.Value);
                                    else if (kvp.Value < 0)
                                        StockAddedTotal.WithLabels(kvp.Key.ToString()).Inc(-kvp.Value);
                                }
                                else
                                {
                                    _logger.LogError("Stock update failed (Insufficient stock or ID not found): Id={Id}, Qty={Qty}", kvp.Key, kvp.Value);
                                    // 这里可以选择抛出异常回滚，或者仅记录错误（取决于业务策略）
                                    // 在秒杀场景下，如果 DB 库存不足，通常意味着 Redis 和 DB 不一致，或者超卖。
                                    // 由于我们依赖 Redis 挡住了大部分请求，这里失败说明 Redis 放进来了但 DB 没了。
                                    // 记录错误即可，不回滚 Tid，否则下次还会重试并失败。
                                }
                            }
                        }

                        await transaction.CommitAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Batch DB transaction failed");
                        StockWriteErrorsTotal.Inc();
                        dbSuccess = false;
                    }
                }

                // 6. Ack / Nack
                // 必须在 lock 中调用，因为 _rabbitChannel 可能在另一个线程（消费者回调）中被使用
                lock (_rabbitChannel)
                {
                    if (dbSuccess)
                    {
                        foreach (var item in pendingItems)
                        {
                            _rabbitChannel.BasicAck(item.DeliveryTag, multiple: false);
                        }
                    }
                    else
                    {
                        // 数据库写入失败，全部重回队列
                        foreach (var item in pendingItems)
                        {
                            _rabbitChannel.BasicNack(item.DeliveryTag, multiple: false, requeue: true);
                        }
                        // 暂停一下避免死循环风暴
                        Thread.Sleep(1000);
                    }
                }
            }
        }
        
        public override void Dispose()
        {
            _rabbitChannel?.Close();
            _rabbitChannel?.Dispose();
            base.Dispose();
        }
        
        private class StockMessage 
        {
            public int Id { get; set; }
            public int Qty { get; set; }
            public string TransactionId { get; set; } = "";
        }
    }
}