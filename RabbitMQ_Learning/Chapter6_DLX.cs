using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Generic;

namespace RabbitMQ_Learning
{
    public class Chapter6_DLX
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第六章：Dead Letter Exchange (死信队列) ===");
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 1. 声明死信交换机 (DLX) 和 两个死信队列
            await channel.ExchangeDeclareAsync(exchange: "my_dlx", type: ExchangeType.Direct);
            
            await channel.QueueDeclareAsync(queue: "my_dlq1", durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue: "my_dlq1", exchange: "my_dlx", routingKey: "dlx_key1");
            
            await channel.QueueDeclareAsync(queue: "my_dlq2", durable: true, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue: "my_dlq2", exchange: "my_dlx", routingKey: "dlx_key2");

            // 2. 声明两个正常业务队列，分别指向不同的死信路由键
            var args1 = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "my_dlx" }, 
                { "x-dead-letter-routing-key", "dlx_key1" } // 队列1的死信 -> my_dlq1
            };

            var args2 = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "my_dlx" }, 
                { "x-dead-letter-routing-key", "dlx_key2" } // 队列2的死信 -> my_dlq2
            };
            
            await channel.ExchangeDeclareAsync(exchange: "normal_exchange", type: ExchangeType.Direct);
            
            await channel.QueueDeclareAsync(queue: "normal_queue1", durable: true, exclusive: false, autoDelete: false, arguments: args1);
            await channel.QueueBindAsync(queue: "normal_queue1", exchange: "normal_exchange", routingKey: "normal_key");

            await channel.QueueDeclareAsync(queue: "normal_queue2", durable: true, exclusive: false, autoDelete: false, arguments: args2);
            await channel.QueueBindAsync(queue: "normal_queue2", exchange: "normal_exchange", routingKey: "normal_key");
            
            // 3. 启动死信队列的消费者
            // 注意：这里我们分别为两个死信队列启动监听
            await StartConsumer(connection, "死信组A", "my_dlq1");
            await StartConsumer(connection, "死信组B", "my_dlq2");

            // 4. 启动正常业务消费者 (都执行拒绝操作)
            await StartBusinessConsumer(channel, "normal_queue1", "业务员1");
            await StartBusinessConsumer(channel, "normal_queue2", "业务员2");

            // 5. 发送消息
            Console.WriteLine(" [*] 发送一条消息到正常交换机...");
            var body = Encoding.UTF8.GetBytes("这是一条注定被抛弃的消息");
            // 这条消息会被分发给两个业务队列，最终产生两条死信
            await channel.BasicPublishAsync(exchange: "normal_exchange", routingKey: "normal_key", body: body);

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }

        private static async Task StartBusinessConsumer(IChannel channel, string queue, string name)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($" [{name}] 在 {queue} 收到: {msg}，但我决定拒绝它！");
                // 拒绝且不重回队列 -> 触发死信转发
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            };
            await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
        }

        private static async Task StartConsumer(IConnection connection, string name, string queue)
        {
            var channel = await connection.CreateChannelAsync();
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($" [{name}] 在死信队列 {queue} 收到: {msg}");
                await channel.BasicAckAsync(ea.DeliveryTag, false);
            };
            await channel.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
        }
    }
}
