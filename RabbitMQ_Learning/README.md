# RabbitMQ 循序渐进学习指南 (C# 版)

这是一个基于 .NET 10 和 `RabbitMQ.Client` 7.x 的循序渐进学习项目。本项目旨在通过 7 个章节的实战代码，帮助开发者从零开始掌握 RabbitMQ 的核心概念与高级特性。

## 项目特点

*   **循序渐进**: 从最简单的 Hello World 到复杂的死信队列、延时队列，难度螺旋上升。
*   **代码详尽**: 每个章节都是一个独立的类，包含详细的中文注释，解释每一行代码的作用。
*   **最新技术**: 使用 .NET 10 和 RabbitMQ 最新客户端库 (支持异步 API)。
*   **场景化**: 结合实际业务场景（如日志广播、订单超时）进行讲解。

## 章节目录

### 第一阶段：基础入门

*   **[Chapter 1: Hello World (基础直连)](Chapter1_HelloWorld.cs)**
    *   **概念**: 生产者、消费者、队列。
    *   **目标**: 建立连接，发送并接收第一条消息。
    *   **核心**: `BasicPublishAsync`, `BasicConsumeAsync`, `autoAck: true`。

*   **[Chapter 2: Work Queues (工作队列)](Chapter2_WorkQueues.cs)**
    *   **概念**: 竞争消费者模式、手动确认 (Ack)、公平分发 (Prefetch)。
    *   **目标**: 模拟耗时任务，让多个工人抢单，实现负载均衡。
    *   **核心**: `BasicAckAsync`, `BasicQosAsync (PrefetchCount=1)`, `durable: true` (持久化)。

### 第二阶段：交换机与路由

*   **[Chapter 3: Publish/Subscribe (发布/订阅)](Chapter3_PubSub.cs)**
    *   **概念**: Exchange (交换机)、Fanout (扇形/广播)、临时队列。
    *   **目标**: 构建一个日志广播系统，所有消费者都能收到同一条消息。
    *   **核心**: `ExchangeType.Fanout`, `QueueDeclareAsync()` (随机名/独占/自动删除)。

*   **[Chapter 4: Routing (路由模式)](Chapter4_Routing.cs)**
    *   **概念**: Direct Exchange (直连交换机)、Routing Key (路由键)、多重绑定。
    *   **目标**: 精确筛选消息。例如：只把 `Error` 级别的日志写磁盘，但把所有日志打印到屏幕。
    *   **核心**: `ExchangeType.Direct`, `QueueBindAsync`。

*   **[Chapter 5: Topics (主题模式)](Chapter5_Topics.cs)**
    *   **概念**: Topic Exchange (主题交换机)、通配符 (`*`, `#`)。
    *   **目标**: 实现最灵活的模式匹配。例如：订阅所有 `kern.*` (核心模块) 或 `*.critical` (严重错误) 的消息。
    *   **核心**: `ExchangeType.Topic`, Binding Key 匹配规则。

### 第三阶段：高级特性

*   **[Chapter 6: Dead Letter Exchange (死信队列)](Chapter6_DLX.cs)**
    *   **概念**: DLX (死信交换机)、Nack (拒绝)、Requeue (重回队列)。
    *   **目标**: 处理“垃圾消息”。当消息被拒绝且不重回队列时，自动转发到死信队列进行兜底处理。
    *   **核心**: `x-dead-letter-exchange`, `BasicNackAsync(requeue: false)`。

*   **[Chapter 7: TTL + DLX (延时队列)](Chapter7_TTL_Delay.cs)**
    *   **概念**: TTL (消息过期时间)、无消费者队列。
    *   **目标**: 利用“消息过期变成死信”的机制，实现延时任务（如：下单30分钟未支付自动取消）。
    *   **核心**: `x-message-ttl`, `x-dead-letter-routing-key`。

## 如何运行

1.  **环境准备**:
    *   安装 .NET 8.0/10.0 SDK。
    *   安装并启动 RabbitMQ 服务 (推荐使用 Docker: `docker run -it --rm --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`)。

2.  **切换章节**:
    打开 `Program.cs`，根据你想运行的章节，取消对应行代码的注释。
    
    ```csharp
    // 例如，运行第七章：
    // await Chapter6_DLX.Run();
    await Chapter7_TTL_Delay.Run();
    ```

3.  **运行项目**:
    在终端中执行：
    ```bash
    dotnet run
    ```

## 学习建议

建议按照章节顺序依次学习。每一章都基于前一章的知识。在阅读代码时，请重点关注 `Run` 方法中的注释，它们解释了每一步操作的意图和背后的原理。
