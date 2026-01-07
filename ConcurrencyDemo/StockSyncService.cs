using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using StackExchange.Redis;

namespace ConcurrencyDemo
{
    /// <summary>
    /// 库存同步后台服务。
    /// <para>
    /// 核心职责：作为“削峰填谷”的消费者，负责将 Redis 的高频扣减操作异步、批量地持久化到 SQLite 数据库。
    /// 同时通过 Redis SETNX 实现了消息的幂等性检查，防止重复消费。
    /// </para>
    /// </summary>
    public class StockSyncService : BackgroundService
    {
        /// <summary>
        /// 内存缓冲队列。使用 Unbounded 模式以吞吐量优先，生产环境建议根据内存限制使用 Bounded。
        /// </summary>
        private readonly Channel<(int Id, int Qty, string TransactionId)> _channel;
        
        private readonly string _connectionString;
        private readonly IDatabase _redisDb;
        private readonly ILogger<StockSyncService> _logger;

        // Prometheus Metrics
        /// <summary>
        /// 记录成功持久化到数据库的库存扣减总量。
        /// </summary>
        private static readonly Counter StockConsumedTotal = Metrics.CreateCounter(
            "stock_consumed_total", 
            "Total amount of stock deducted from database", 
            new CounterConfiguration { LabelNames = new[] { "material_id" } });

        /// <summary>
        /// 记录数据库写入失败的次数。
        /// </summary>
        private static readonly Counter StockWriteErrorsTotal = Metrics.CreateCounter(
            "stock_write_errors_total", 
            "Total number of database write errors");

        public StockSyncService(
            string connectionString, 
            IConnectionMultiplexer redis,
            ILogger<StockSyncService> logger)
        {
            _connectionString = connectionString;
            _redisDb = redis.GetDatabase();
            _logger = logger;
            _channel = Channel.CreateUnbounded<(int, int, string)>();
        }

        /// <summary>
        /// 生产者接口：将扣减请求放入内存队列。
        /// <para>此操作是非阻塞的，极快。</para>
        /// </summary>
        /// <param name="materialId">物料ID</param>
        /// <param name="qty">扣减数量</param>
        /// <param name="transactionId">业务流水号（用于幂等性）</param>
        public void Enqueue(int materialId, int qty, string transactionId)
        {
            _channel.Writer.TryWrite((materialId, qty, transactionId));
        }

        /// <summary>
        /// 消费者主循环。
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var validBatch = new Dictionary<int, int>();
            var pendingItems = new List<(int Id, int Qty, string TransactionId)>();

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
                }

                if (pendingItems.Count == 0) continue;

                // 3. 幂等性检查 (Redis Pipeline)
                // 使用 Redis SETNX (Set if Not Exists) 批量检查 TransactionId 是否已处理
                var checkTasks = new List<Task<bool>>();
                foreach (var item in pendingItems)
                {
                    string key = $"processed:tid:{item.TransactionId}";
                    // 24小时过期，防止 Redis 内存无限增长
                    checkTasks.Add(_redisDb.StringSetAsync(key, "1", TimeSpan.FromDays(1), When.NotExists));
                }

                var results = await Task.WhenAll(checkTasks);

                // 4. 聚合有效消息
                // 将通过检查的消息按 MaterialId 进行聚合 (e.g., 10个 "减1" -> 1个 "减10")
                int duplicateCount = 0;
                for (int i = 0; i < pendingItems.Count; i++)
                {
                    if (results[i]) // SETNX 成功，说明是新消息
                    {
                        var item = pendingItems[i];
                        if (validBatch.ContainsKey(item.Id))
                            validBatch[item.Id] += item.Qty;
                        else
                            validBatch[item.Id] = item.Qty;
                    }
                    else
                    {
                        duplicateCount++;
                    }
                }

                if (duplicateCount > 0)
                {
                    _logger.LogWarning("拦截重复请求: {DuplicateCount} 条", duplicateCount);
                }

                // 5. 批量写入数据库
                if (validBatch.Count > 0)
                {
                    await BatchUpdateDatabase(validBatch);
                }
            }
        }

        /// <summary>
        /// 将聚合后的扣减请求写入数据库。
        /// </summary>
        private async Task BatchUpdateDatabase(Dictionary<int, int> batch)
        {
            try
            {
                using (var conn = new SqliteConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        foreach (var kvp in batch)
                        {
                            var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            // 直接扣减，不检查余额（相信 Redis 的判断）
                            cmd.CommandText = "UPDATE MaterialStock SET Qty = Qty - @qty WHERE Id = @id";
                            cmd.Parameters.AddWithValue("@id", kvp.Key);
                            cmd.Parameters.AddWithValue("@qty", kvp.Value);
                            await cmd.ExecuteNonQueryAsync();
                            
                            _logger.LogInformation("DB Async Write: Id={Id}, Deduct={Qty}", kvp.Key, kvp.Value);
                            
                            // Metrics: 记录扣减量
                            StockConsumedTotal.WithLabels(kvp.Key.ToString()).Inc(kvp.Value);
                        }
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量写入失败");
                StockWriteErrorsTotal.Inc();
                // 生产环境建议：将失败的 Batch 写入死信队列或本地文件，以便后续人工补偿
            }
        }
    }
}
