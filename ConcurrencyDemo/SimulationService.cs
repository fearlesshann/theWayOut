using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConcurrencyDemo
{
    /// <summary>
    /// 模拟并发流量服务。
    /// <para>
    /// 仅用于演示目的。在应用启动后，自动生成一批并发请求，模拟“秒杀”场景。
    /// 同时也起到了“预热” (Warmup) 的作用。
    /// </para>
    /// </summary>
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
            try 
            {
                // 等待其他服务（如 Redis 连接）完全就绪
                await Task.Delay(2000, stoppingToken);

                // 1. 初始化 Redis 库存
                _logger.LogInformation("初始化 Redis 库存: 100000");
                await _stockService.InitializeStockAsync(100000);

                // 2. 模拟 100 个并发请求
                _logger.LogInformation("开始模拟 100 个并发 Task...");
                var tasks = new List<Task>();

                for (int i = 0; i < 100; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        // 模拟扣减
                        bool success = await _stockService.DeductStockAsync(1);
                        if (success)
                        {
                            string tid = Guid.NewGuid().ToString("N");
                            _logger.LogInformation("Task {TaskId}: 抢购成功！TID={Tid}", taskId, tid.Substring(0, 8));

                            // 正常发送
                            _syncService.Enqueue(1, 1, tid);
                            
                            // 模拟网络抖动导致的重复发送（验证幂等性）
                            _syncService.Enqueue(1, 1, tid);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                _logger.LogInformation("模拟请求发送完毕。");
            }
            catch (Exception ex)
            {
                // 捕获异常，防止因为模拟服务失败导致整个应用退出
                _logger.LogError(ex, "模拟服务执行失败 (可能是 Redis 连接超时)");
            }
        }
    }
}
