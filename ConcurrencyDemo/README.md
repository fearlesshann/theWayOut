# ConcurrencyDemo - 高并发秒杀库存扣减演示

这是一个用于演示高并发场景下库存扣减方案的 .NET 演示项目。它展示了如何结合 Redis 原子操作、RabbitMQ 异步消息队列和 SQL Server 持久化存储，来实现一个高性能、高可用且数据一致的“秒杀”系统核心逻辑。

## 核心架构

该项目采用 **Redis + Lua 脚本** 进行前端限流与扣减，利用 **RabbitMQ** 进行削峰填谷，最终通过 **SQL Server** 落库，实现了典型的最终一致性架构。

### 技术栈

- **.NET 10**: 核心业务服务 (Minimal API)。
- **Redis**: 核心组件。利用 Lua 脚本实现原子性库存检查与扣减，抗住高并发读写。
- **RabbitMQ**: 消息中间件。用于解耦扣减动作与数据库写入，保护数据库不被瞬时流量击垮。
- **SQL Server**: 数据库。用于持久化存储库存和交易流水。
- **Docker & Docker Compose**: 容器化部署与编排。
- **Prometheus**: 监控指标收集。
- **Serilog**: 结构化日志。
- **k6**: (可选) 包含 `loadtest.js` 用于进行压力测试。

### 业务流程

1.  **初始化**:
    *   应用启动时，自动初始化 SQL Server 表结构。
    *   将数据库中的库存预热加载到 Redis 中。
2.  **秒杀请求 (/api/deduct)**:
    *   **Step 1 (Redis)**: 调用 Lua 脚本 (`RedisLuaStock.cs`) 执行原子扣减。如果库存不足，直接返回失败。这一步完全在内存中完成，极其快速。
    *   **Step 2 (MQ)**: 扣减成功后，生成交易流水号 (TransactionId)，并将扣减事件发送到 RabbitMQ 队列。
    *   **Step 3 (Response)**: 立即向用户返回“抢购成功”响应。
3.  **异步处理 (StockSyncService)**:
    *   后台消费者监听 RabbitMQ 队列。
    *   **幂等性检查**: 根据 TransactionId 检查数据库中是否已处理过该交易，防止重复扣减。
    *   **持久化**: 将扣减操作写入 SQL Server (`MaterialStock` 表更新，`ProcessedTransactions` 表插入)。

## 项目结构

*   `Program.cs`: 应用入口，配置 DI、中间件、API 路由及 Redis/SQL 初始化。
*   `RedisLuaStock.cs`: 封装 Redis Lua 脚本，实现原子性 `DeductStock` 和 `AddStock`。
*   `StockSyncService.cs`: 既是 RabbitMQ 的生产者（发送消息），也是后台消费者（处理消息并落库）。
*   `SimulationService.cs`: 一个后台任务，启动后自动模拟 100 个并发请求，用于演示和自测（包含幂等性测试）。
*   `docker-compose.yml`: 一键启动所有依赖服务 (Redis, RabbitMQ, SQL Server)。

## 快速开始

### 前置要求

*   Docker Desktop 或 Docker Engine
*   .NET SDK (如果要本地运行代码)

### 运行方式

**推荐使用 Docker Compose 一键启动：**

```bash
docker-compose up --build
```

启动后：
1.  SQL Server, Redis, RabbitMQ 容器将启动。
2.  应用容器启动，并自动执行数据库初始化和 Redis 预热。
3.  `SimulationService` 会自动运行，模拟 100 次并发抢购。
4.  观察控制台日志，可以看到 Redis 扣减成功、RabbitMQ 发送消息、后台消费者落库的全过程。

### 接口测试

你可以使用 Postman 或 curl 手动调用接口：

*   **扣减库存**:
    ```bash
    curl -X POST http://localhost:8080/api/deduct
    ```
*   **增加库存**:
    ```bash
    curl -X POST http://localhost:8080/api/add
    ```
*   **健康检查**:
    ```bash
    curl http://localhost:8080/health
    ```
*   **Prometheus 指标**:
    ```bash
    curl http://localhost:8080/metrics
    ```

## 监控与观测

*   **Logs**: 通过 `docker-compose logs -f app` 查看实时日志。
*   **RabbitMQ Management**: 访问 `http://localhost:15672` (guest/guest) 查看队列堆积情况。

## 关键代码说明

### Redis 原子扣减 (Lua)

为了防止超卖，我们不使用 `Get` + `Set`，而是使用 Lua 脚本保证原子性：

```lua
-- Key: 库存Key
-- Arg1: 扣减数量
local stock = tonumber(redis.call('GET', KEYS[1]))
if stock == nil then return -1 end
if stock < tonumber(ARGV[1]) then return 0 end
redis.call('DECRBY', KEYS[1], ARGV[1])
return 1
```

### 最终一致性与幂等性

`StockSyncService` 在消费消息时，会利用 `ProcessedTransactions` 表进行去重：

```csharp
// SQL 逻辑
IF EXISTS (SELECT 1 FROM ProcessedTransactions WHERE TransactionId = @Tid)
    RETURN; // 已处理，直接返回
// 否则执行扣减并插入流水
```

这确保了即使 RabbitMQ 发生消息重试（At-least-once delivery），数据库的数据也不会错乱。
