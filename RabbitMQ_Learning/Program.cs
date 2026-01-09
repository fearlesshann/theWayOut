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
            await Chapter2_WorkQueues.Run();

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
        }
    }
}
