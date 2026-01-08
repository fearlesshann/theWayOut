using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace ConcurrencyDemo
{
    /// <summary>
    /// 基于 Redis Lua 脚本的库存扣减服务。
    /// <para>
    /// 核心职责：利用 Redis 的单线程特性和 Lua 脚本原子性，实现高性能、无锁的库存扣减。
    /// 这是秒杀系统的“抗压层”，能够处理数万 TPS 的并发请求。
    /// </para>
    /// </summary>
    public class RedisLuaStock
    {
        private readonly IDatabase _db;
        private const string StockKey = "material:stock:1";

        /// <summary>
        /// 原子扣减脚本。
        /// <para>
        /// 逻辑：
        /// 1. 检查库存是否存在且大于请求数量 (deductQty)。
        /// 2. 如果满足，执行 DECRBY 扣减。
        /// 3. 返回 1 (成功) 或 -1 (失败)。
        /// </para>
        /// <para>
        /// 为什么用 Lua？
        /// Redis 会将整个脚本作为一个原子操作执行，期间不会插入其他命令。
        /// 这避免了 "Check-Then-Act" 造成的竞态条件 (Race Condition)。
        /// </para>
        /// </summary>
        private const string DeductScript = @"
            local stockKey = KEYS[1]
            local deductQty = tonumber(ARGV[1])

            -- 获取当前库存，如果不存在则默认为 0
            local currentStock = tonumber(redis.call('GET', stockKey) or '0')

            if currentStock >= deductQty then
                -- 库存充足，执行扣减
                redis.call('DECRBY', stockKey, deductQty)
                return 1 
            else
                -- 库存不足
                return -1
            end
        ";

        public RedisLuaStock(IDatabase db)
        {
            _db = db;
        }

        /// <summary>
        /// 初始化/重置库存。
        /// </summary>
        /// <param name="qty">初始库存数量</param>
        public async Task InitializeStockAsync(int qty)
        {
            // 初始化 Redis 库存
            await _db.StringSetAsync(StockKey, qty);
        }

        /// <summary>
        /// 尝试扣减库存。
        /// </summary>
        /// <param name="qty">扣减数量</param>
        /// <returns>true: 扣减成功; false: 库存不足</returns>
        public async Task<bool> DeductStockAsync(int qty)
        {
            // 执行 Lua 脚本
            // 注意：这里直接传递 RedisKey[] 和 RedisValue[]，脚本中对应 KEYS[1] 和 ARGV[1]
            var result = await _db.ScriptEvaluateAsync(
                DeductScript, 
                new RedisKey[] { StockKey }, 
                new RedisValue[] { qty }
            );

            return (int)result == 1;
        }

        /// <summary>
        /// 增加库存。
        /// </summary>
        /// <param name="qty">增加数量</param>
        public async Task AddStockAsync(int qty)
        {
            await _db.StringIncrementAsync(StockKey, qty);
        }
        
        // ----------------- 架构师思考：关于“Redis 扣减成功但程序崩溃”的数据一致性问题 -----------------
        /*
         * 问题核心：
         * Redis 是内存操作，速度极快；SQL Server 是磁盘 IO，速度较慢。
         * 如果 Redis 扣减成功（库存 10 -> 9），但在代码执行到 SQL 写入之前，C# 进程崩溃了（断电、OOM），
         * 此时 Redis 中是 9，数据库中依然是 10。这就出现了“少卖”或“数据不一致”。
         * 
         * 解决方案层级（由轻到重）：
         * 
         * 1. 【预扣减 + 异步写入 (MQ)】（最推荐 - 高并发场景）
         *    - 流程：
         *      1. Redis Lua 扣减成功。
         *      2. 发送“扣减成功”消息到消息队列 (RabbitMQ/Kafka)。
         *      3. 消费者监听队列，异步将扣减操作写入 SQL Server。
         *    - 崩溃处理：
         *      如果第 2 步（发消息）失败怎么办？
         *      需要引入“本地消息表”模式：在 Redis 扣减的同时，在本地存储（或可靠存储）记录一条“待发送消息”。
         *      或者使用 RocketMQ 的“事务消息”。
         * 
         * 2. 【最终一致性 - 定期对账】
         *    - 允许短时间内不一致。Redis 作为“主权”数据（Source of Truth）。
         *    - 启动一个定时任务（每分钟），扫描 Redis 中的库存快照，与数据库进行比对和同步。
         *    - 风险：如果 Redis 宕机且数据丢失（未开启 AOF fsync always），则数据永久丢失。
         * 
         * 3. 【TCC (Try-Confirm-Cancel) 分布式事务】（极重，不推荐用于秒杀）
         *    - 引入分布式事务框架（如 Seata）。
         *    - 性能损耗巨大，违背了引入 Redis 做秒杀的初衷。
         * 
         * 4. 【Lua 脚本扩展 - 记录流水】
         *    - 修改 Lua 脚本，在 DECRBY 的同时，LPUSH 一条“扣减记录”到 Redis 的一个 List 中。
         *    - 这样即使 C# 挂了，Redis 里也留存了“谁扣减了”的证据。
         *    - 恢复服务后，通过处理这个 List 来补写数据库。
         */
    }
}
