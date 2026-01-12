using System;
using System.Threading.Tasks;

namespace RabbitMQ_Learning
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== RabbitMQ 循序渐进学习项目 ===");
            Console.WriteLine("请在代码中取消注释以运行相应章节：");
            Console.WriteLine();

            // --- 第一阶段：基础 ---

            // 第一章：Hello World (基础直连)
            // 目标：确保环境正常，发送第一条消息
            // await Chapter1_HelloWorld.Run();

            // 第二章：Work Queues (工作队列)
            // 目标：学习竞争消费者模式、手动 Ack、Prefetch (公平分发)
            // await Chapter2_WorkQueues.Run();

            // --- 第二阶段：交换机与路由 ---

            // 第三章：Publish/Subscribe (发布/订阅)
            // 目标：学习 Fanout 交换机 (广播模式)
            // await Chapter3_PubSub.Run();

            // 第四章：Routing (路由模式)
            // 目标：学习 Direct 交换机 (路由键匹配)
            // await Chapter4_Routing.Run();

            // 第五章：Topics (主题模式)
            // 目标：学习 Topic 交换机 (通配符匹配)
            // await Chapter5_Topics.Run();

            // --- 第三阶段：高级特性 ---

            // 第六章：Dead Letter Exchange (死信队列)
            // 目标：学习处理被拒绝的消息 (Nack + Requeue=False)
            // await Chapter6_DLX.Run();

            // 第七章：TTL + DLX (延时队列)
            // 目标：学习利用消息过期实现延时任务 (如订单超时取消)
            await Chapter7_TTL_Delay.Run();
        }
    }
}
