using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConcurrencyDemo
{
    public class SimulationService : BackgroundService
    {
        private readonly RedisLuaStock _stockService;
        private readonly StockSyncService _syncService;
        private readonly ILogger<SimulationService> _logger;

        public SimulationService(
            RedisLuaStock stockService, 
            StockSyncService syncService,
            ILogger<SimulationService> logger)
        {
            _stockService = stockService;
            _syncService = syncService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 给其他服务一点启动时间
            await Task.Delay(1000, stoppingToken);

            _logger.LogInformation("初始化 Redis 库存: 10");
            await _stockService.InitializeStockAsync(10);

            _logger.LogInformation("开始模拟 100 个并发 Task...");
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    bool success = await _stockService.DeductStockAsync(1);
                    if (success)
                    {
                        string tid = Guid.NewGuid().ToString("N");
                        _logger.LogInformation("Task {TaskId}: 抢购成功！TID={Tid}", taskId, tid.Substring(0, 8));

                        // 正常发送
                        _syncService.Enqueue(1, 1, tid);
                        
                        // 模拟重复发送
                        _syncService.Enqueue(1, 1, tid);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("模拟请求发送完毕。");
        }
    }
}
