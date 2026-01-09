using System;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_Learning
{
    public class Chapter1_HelloWorld
    {
        public static async Task Run()
        {
            Console.WriteLine("=== 第一章：Hello World (基础直连) ===");

            // 1. 创建连接工厂
            var factory = new ConnectionFactory { HostName = "localhost" };

            // 2. 建立连接 (Connection) 和 信道 (Channel)
            // 在 RabbitMQ 7.x+ 中，一切都是异步的
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 3. 声明队列 (Queue)
            // queue: 队列名称
            // durable: 是否持久化 (false: 重启后消失)
            // exclusive: 是否独占 (false: 其他连接也能访问)
            // autoDelete: 是否自动删除 (false: 即使没人用也不删)
            // arguments: 其他参数
            await channel.QueueDeclareAsync(queue: "hello", durable: false, exclusive: false, autoDelete: false, arguments: null);

            Console.WriteLine(" [*] 准备发送消息...");

            // 4. 发送消息 (Producer)
            const string message = "Hello World!";
            var body = Encoding.UTF8.GetBytes(message);

            // exchange: 交换机名称 (空字符串表示默认交换机)
            // routingKey: 路由键 (默认交换机下，直接写队列名)
            await channel.BasicPublishAsync(exchange: "", routingKey: "hello", body: body);
            
            Console.WriteLine($" [x] 已发送: {message}");

            // 5. 接收消息 (Consumer)
            Console.WriteLine(" [*] 等待接收消息...");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] 收到消息: {message}");
                await Task.CompletedTask;
            };

            // autoAck: true (自动确认，收到即认为处理成功)
            await channel.BasicConsumeAsync(queue: "hello", autoAck: true, consumer: consumer);

            Console.WriteLine(" 按任意键退出...");
            Console.ReadLine();
        }
    }
}
