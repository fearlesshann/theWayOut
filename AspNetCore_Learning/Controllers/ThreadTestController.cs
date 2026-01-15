using System.Diagnostics;

namespace AspNetCore_Learning.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class ThreadTestController : ControllerBase
{
    // 1. 模拟 I/O 密集型任务 (如查数据库、调 API)
    // 这里的关键是：Task.Delay 底层使用定时器，不占用线程
    [HttpGet("io")]
    public async Task<IActionResult> TestIOBound()
    {
        var initialThreads = Process.GetCurrentProcess().Threads.Count;
        var tasks = new List<Task>();
        
        // 启动 100 个任务
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(SimulateDbCallAsync());
        }

        var duringThreads = Process.GetCurrentProcess().Threads.Count;

        await Task.WhenAll(tasks);

        return Ok(new
        {
            Type = "I/O Bound (Task.Delay)",
            TaskCount = 100,
            InitialThreads = initialThreads,
            DuringExecutionThreads = duringThreads,
            ThreadIncrease = duringThreads - initialThreads,
            Conclusion = "几乎没有增加线程。因为 I/O 任务在等待时会释放线程。"
        });
    }

    // 2. 模拟 CPU 密集型任务 (如复杂计算)
    // 这里使用 Task.Run 强制在线程池中运行
    [HttpGet("cpu")]
    public async Task<IActionResult> TestCPUBound()
    {
        var initialThreads = Process.GetCurrentProcess().Threads.Count;
        var tasks = new List<Task>();

        // 启动 100 个任务
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => SimulateHeavyCalculation()));
        }
        
        // 给一点时间让线程池尝试扩张
        await Task.Delay(100); 

        var duringThreads = Process.GetCurrentProcess().Threads.Count;
        
        // 获取线程池信息
        ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
        ThreadPool.GetMaxThreads(out int maxWorker, out int maxCompletion);

        await Task.WhenAll(tasks);

        return Ok(new
        {
            Type = "CPU Bound (Task.Run)",
            TaskCount = 100,
            InitialThreads = initialThreads,
            DuringExecutionThreads = duringThreads,
            ThreadIncrease = duringThreads - initialThreads,
            ThreadPoolStats = $"Available: {workerThreads}/{maxWorker}",
            Conclusion = "线程数增加了，但远没有达到 100 个。线程池会复用有限的线程 (通常是 CPU 核心数的倍数) 来轮流处理这 100 个任务。"
        });
    }

    private async Task SimulateDbCallAsync()
    {
        // 模拟 1 秒的异步 I/O 等待
        await Task.Delay(1000);
    }

    private void SimulateHeavyCalculation()
    {
        // 模拟 1 秒的 CPU 占用
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1000)
        {
            // 空转 CPU
        }
    }
}
