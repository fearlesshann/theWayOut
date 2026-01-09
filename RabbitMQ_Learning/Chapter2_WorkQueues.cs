using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Learning
{
    public class Chapter2_WorkQueues
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第二章：Work Queues (工作队列) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 声明持久化队列
            await channel.QueueDeclareAsync(queue: "task_queue", durable: true, exclusive: false, autoDelete: false, arguments: null);

            // 关键点：设置 PrefetchCount = 1 (公平分发)
            // 告诉 RabbitMQ：在我处理完上一条消息并 Ack 之前，不要给我发新消息
            // 只要保证channel和策略绑定就行了，不需要在循环里重复设置
            await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

            // 1. 启动两个消费者 (模拟两个工人)
            for (int i = 1; i <= 2; i++)
            {
                var workerId = i;
                
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    Console.WriteLine($" [工人{workerId}] 接收任务: {message}");

                    // 模拟耗时任务 (点号越多越耗时)
                    int dots = message.Split('.').Length - 1;
                    await Task.Delay(dots * 1000);

                    Console.WriteLine($" [工人{workerId}] 完成任务");

                    // 手动确认 (Ack)
                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
                };

                // autoAck: false (必须手动确认)
                await channel.BasicConsumeAsync(queue: "task_queue", autoAck: false, consumer: consumer);
                Console.WriteLine($" [*] 工人 {workerId} 就绪");
            }

            // 2. 发送一堆任务
            Console.WriteLine(" [*] 开始派发任务...");
            for (int i = 0; i < 5; i++)
            {
                string message = $"Task {i} " + new string('.', i + 1); // Task 0 ., Task 1 .., etc.
                var body = Encoding.UTF8.GetBytes(message);

                var properties = new BasicProperties { Persistent = true }; // 消息持久化

                await channel.BasicPublishAsync(exchange: "", routingKey: "task_queue", mandatory: false, basicProperties: properties, body: body);
                Console.WriteLine($" [x] 派发: {message}");
            }

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }
    }
}
