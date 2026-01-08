using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
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

                // 3. 幂等性检查 (Redis Pipeline)
                // 使用 Redis SETNX (Set if Not Exists) 批量检查 TransactionId 是否已处理
                var tasks = new List<Task<bool>>();
                
                // 使用 Pipeline 提高性能
                var batch = _redisDb.CreateBatch();
                foreach (var item in pendingItems)
                {
                    string key = $"processed:tid:{item.TransactionId}";
                    tasks.Add(batch.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists));
                }
                batch.Execute();
                
                var results = await Task.WhenAll(tasks);

                // 4. 合并扣减请求 & 筛选有效请求
                // 即使是重复消息，也需要包含在 pendingItems 里以便最后 Ack，但不计入 validBatch
                for (int i = 0; i < pendingItems.Count; i++)
                {
                    if (results[i]) // Set 成功，说明是新消息
                    {
                        var item = pendingItems[i];
                        if (validBatch.ContainsKey(item.Id))
                            validBatch[item.Id] += item.Qty;
                        else
                            validBatch[item.Id] = item.Qty;
                    }
                    else
                    {
                        _logger.LogWarning("Duplicate transaction detected: {Tid}", pendingItems[i].TransactionId);
                    }
                }

                // 5. 批量写入 SQLite
                bool dbSuccess = true;
                if (validBatch.Count > 0)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    await connection.OpenAsync(stoppingToken);
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        foreach (var kvp in validBatch)
                        {
                            var command = connection.CreateCommand();
                            command.CommandText = "UPDATE MaterialStock SET Qty = Qty - $qty WHERE Id = $id";
                            command.Parameters.AddWithValue("$qty", kvp.Value);
                            command.Parameters.AddWithValue("$id", kvp.Key);
                            await command.ExecuteNonQueryAsync(stoppingToken);
                            
                            StockConsumedTotal.WithLabels(kvp.Key.ToString()).Inc(kvp.Value);
                        }

                        await transaction.CommitAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Batch DB write failed");
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