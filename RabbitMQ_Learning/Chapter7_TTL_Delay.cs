using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Generic;

namespace RabbitMQ_Learning
{
    public class Chapter7_TTL_Delay
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第七章：TTL + DLX (延时队列) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 原理：
            // 1. 发消息到一个没有消费者的队列 (TTL Queue)。
            // 2. 消息在这个队列里“睡” 5秒钟 (TTL)。
            // 3. 消息过期后，变成死信，被自动转发到死信队列 (Process Queue)。
            // 4. 消费者只监听死信队列，从而实现“延时处理”。

            // 1. 声明死信交换机和实际处理队列
            await channel.ExchangeDeclareAsync(exchange: "delay_process_exchange", type: ExchangeType.Direct);
            await channel.QueueDeclareAsync(queue: "delay_process_queue", durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue: "delay_process_queue", exchange: "delay_process_exchange", routingKey: "process");

            // 2. 声明 TTL 队列 (中转站)
            var args = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "delay_process_exchange" }, // 过期后去哪？
                { "x-dead-letter-routing-key", "process" },             // 带什么路由键去？
                { "x-message-ttl", 5000 }                               // 统一设置过期时间 5000ms (5秒)
            };
            
            await channel.QueueDeclareAsync(queue: "ttl_buffer_queue", durable: true, exclusive: false, autoDelete: false, arguments: args);
            // 不需要绑定消费者！

            // 3. 启动实际处理的消费者
            await StartConsumer(connection, "延时任务处理器", "delay_process_queue");

            // 4. 发送消息到 TTL 队列
            Console.WriteLine($" [*] {DateTime.Now:HH:mm:ss} 发送延时任务...");
            
            for (int i = 1; i <= 3; i++)
            {
                var msg = $"订单 #{i} (需5秒后处理)";
                var body = Encoding.UTF8.GetBytes(msg);
                await Task.Delay(1000);
                // 直接发到 TTL 队列 (默认交换机)
                await channel.BasicPublishAsync(exchange: "", routingKey: "ttl_buffer_queue", body: body);
                Console.WriteLine($" [x] {DateTime.Now:HH:mm:ss} 发送: {msg}");
            }

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }

        private static async Task StartConsumer(IConnection connection, string name, string queue)
        {
            var channel = await connection.CreateChannelAsync();
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($" [{name}] {DateTime.Now:HH:mm:ss} 收到并处理: {msg}");
                await channel.BasicAckAsync(ea.DeliveryTag, false);
            };
            await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
        }
    }
}
