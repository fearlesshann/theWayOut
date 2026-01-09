using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Learning
{
    public class Chapter4_Routing
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第四章：Routing (路由模式) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 1. 声明 Direct 交换机
            await channel.ExchangeDeclareAsync(exchange: "direct_logs", type: ExchangeType.Direct);

            // 2. 启动订阅者
            // 订阅者 1: 只接收 Error
            await StartSubscriber(connection, "错误记录员", new[] { "error" });
            // 订阅者 2: 接收 Info, Warning, Error
            await StartSubscriber(connection, "综合显示屏", new[] { "info", "warning", "error" });

            // 3. 发送不同级别的日志
            var levels = new[] { "info", "warning", "error", "info" };
            foreach (var level in levels)
            {
                string message = $"This is a {level} log";
                var body = Encoding.UTF8.GetBytes(message);

                // 发送到交换机，指定 RoutingKey
                await channel.BasicPublishAsync(exchange: "direct_logs", routingKey: level, body: body);
                Console.WriteLine($" [x] 发送 ({level}): {message}");
            }

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }

        private static async Task StartSubscriber(IConnection connection, string name, string[] levels)
        {
            var channel = await connection.CreateChannelAsync();
            var queueName = (await channel.QueueDeclareAsync()).QueueName;

            // 根据级别进行多次绑定
            foreach (var level in levels)
            {
                await channel.QueueBindAsync(queue: queueName, exchange: "direct_logs", routingKey: level);
            }

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [{name}] 收到 [{ea.RoutingKey}]: {message}");
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
            Console.WriteLine($" [*] {name} 监听级别: {string.Join(", ", levels)}");
        }
    }
}
