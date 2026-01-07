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
    public class StockSyncService : BackgroundService
    {
        private readonly Channel<(int Id, int Qty, string TransactionId)> _channel;
        private readonly string _connectionString;
        private readonly IDatabase _redisDb;
        private readonly ILogger<StockSyncService> _logger;

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
            ILogger<StockSyncService> logger)
        {
            _connectionString = connectionString;
            _redisDb = redis.GetDatabase();
            _logger = logger;
            _channel = Channel.CreateUnbounded<(int, int, string)>();
        }

        public void Enqueue(int materialId, int qty, string transactionId)
        {
            _channel.Writer.TryWrite((materialId, qty, transactionId));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var validBatch = new Dictionary<int, int>();
            var pendingItems = new List<(int Id, int Qty, string TransactionId)>();

            while (!stoppingToken.IsCancellationRequested)
            {
                // 等待读取
                if (!await _channel.Reader.WaitToReadAsync(stoppingToken)) break;

                validBatch.Clear();
                pendingItems.Clear();

                // 读取本批次所有消息
                while (_channel.Reader.TryRead(out var item))
                {
                    pendingItems.Add(item);
                }

                if (pendingItems.Count == 0) continue;

                // 幂等性检查
                var checkTasks = new List<Task<bool>>();
                foreach (var item in pendingItems)
                {
                    string key = $"processed:tid:{item.TransactionId}";
                    checkTasks.Add(_redisDb.StringSetAsync(key, "1", TimeSpan.FromDays(1), When.NotExists));
                }

                var results = await Task.WhenAll(checkTasks);

                int duplicateCount = 0;
                for (int i = 0; i < pendingItems.Count; i++)
                {
                    if (results[i])
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

                if (validBatch.Count > 0)
                {
                    await BatchUpdateDatabase(validBatch);
                }
            }
        }

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
            }
        }
    }
}
