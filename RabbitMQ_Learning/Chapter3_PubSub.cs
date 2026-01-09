using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Learning
{
    public class Chapter3_PubSub
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第三章：Publish/Subscribe (发布/订阅) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 1. 声明 Fanout 交换机 (广播)
            await channel.ExchangeDeclareAsync(exchange: "logs", type: ExchangeType.Fanout);

            // 2. 启动两个订阅者
            // 订阅者 1: 打印到屏幕
            await StartSubscriber(connection, "屏幕打印者");
            // 订阅者 2: (模拟) 写入磁盘
            await StartSubscriber(connection, "磁盘写入者");

            // 3. 发送广播消息
            Console.WriteLine(" [*] 开始广播日志...");
            for (int i = 0; i < 5; i++)
            {
                string message = $"Log info {i}";
                var body = Encoding.UTF8.GetBytes(message);

                // 发送到交换机，RoutingKey 在 Fanout 模式下被忽略
                await channel.BasicPublishAsync(exchange: "logs", routingKey: "", body: body);
                Console.WriteLine($" [x] 广播: {message}");
            }

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }

        private static async Task StartSubscriber(IConnection connection, string name)
        {
            var channel = await connection.CreateChannelAsync();
            
            // 获取一个随机名称的临时队列 (非持久，独占，自动删除)
            QueueDeclareOk queueDeclareResult = await channel.QueueDeclareAsync();
            string queueName = queueDeclareResult.QueueName;

            // 绑定队列到交换机
            await channel.QueueBindAsync(queue: queueName, exchange: "logs", routingKey: "");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [{name}] 收到: {message}");
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
            Console.WriteLine($" [*] {name} 已订阅");
        }
    }
}
