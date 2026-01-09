using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Learning
{
    public class Chapter5_Topics
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第五章：Topics (主题模式) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 1. 声明 Topic 交换机
            await channel.ExchangeDeclareAsync(exchange: "topic_logs", type: ExchangeType.Topic);

            // 2. 启动订阅者
            // 符号说明:
            // * (星号) 匹配一个单词
            // # (井号) 匹配零个或多个单词
            
            // 订阅者 1: 关心所有核心模块 (kern.*)
            await StartSubscriber(connection, "核心模块监控", "kern.*");
            
            // 订阅者 2: 关心所有严重错误 (*.critical)
            await StartSubscriber(connection, "严重错误报警", "*.critical");
            
            // 订阅者 3: 关心所有日志 (#)
            await StartSubscriber(connection, "全量日志归档", "#");

            // 3. 发送消息
            var routingKeys = new[] 
            { 
                "kern.info", 
                "kern.critical", 
                "auth.info", 
                "auth.critical" 
            };

            foreach (var key in routingKeys)
            {
                string message = $"Message for {key}";
                var body = Encoding.UTF8.GetBytes(message);
                await channel.BasicPublishAsync(exchange: "topic_logs", routingKey: key, body: body);
                Console.WriteLine($" [x] 发送: {key}");
            }

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }

        private static async Task StartSubscriber(IConnection connection, string name, string bindingKey)
        {
            var channel = await connection.CreateChannelAsync();
            var queueName = (await channel.QueueDeclareAsync()).QueueName;

            await channel.QueueBindAsync(queue: queueName, exchange: "topic_logs", routingKey: bindingKey);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($" [{name}] 收到 [{ea.RoutingKey}]: {message}");
                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);
            Console.WriteLine($" [*] {name} 监听: {bindingKey}");
        }
    }
}
