using System;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace ConcurrencyDemo
{
    /// <summary>
    /// Redis 分布式锁工具类
    /// </summary>
    public class RedisLock
    {
        private readonly IDatabase _database;

        public RedisLock(IDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// 尝试获取锁
        /// </summary>
        /// <param name="key">锁的 Key</param>
        /// <param name="value">锁的 Value（通常是唯一标识，用于防止误删）</param>
        /// <param name="expiry">锁的过期时间（防止死锁）</param>
        /// <param name="waitTime">最大等待时间</param>
        /// <param name="retryInterval">重试间隔</param>
        /// <returns>获取锁的结果句柄</returns>
        public async Task<RedisLockHandle> AcquireAsync(string key, string value, TimeSpan expiry, TimeSpan waitTime, TimeSpan retryInterval)
        {
            var startTime = DateTime.UtcNow;

            while (true)
            {
                // 尝试获取锁
                // LockTakeAsync 对应 Redis 的 SET resource_name my_random_value NX PX 30000
                bool isLocked = await _database.LockTakeAsync(key, value, expiry);

                if (isLocked)
                {
                    // 抢到锁了
                    return new RedisLockHandle(_database, key, value, true);
                }

                // 检查是否超时
                if (DateTime.UtcNow - startTime > waitTime)
                {
                    // 等待超时，未抢到锁
                    return new RedisLockHandle(_database, key, value, false);
                }

                // 没抢到且没超时，等待一段时间后重试
                await Task.Delay(retryInterval);
            }
        }
    }

    /// <summary>
    /// 锁句柄，利用 IDisposable 实现自动释放
    /// </summary>
    public class RedisLockHandle : IDisposable
    {
        private readonly IDatabase _database;
        private readonly string _key;
        private readonly string _value;
        
        public bool IsAcquired { get; }

        public RedisLockHandle(IDatabase database, string key, string value, bool isAcquired)
        {
            _database = database;
            _key = key;
            _value = value;
            IsAcquired = isAcquired;
        }

        public void Dispose()
        {
            if (IsAcquired)
            {
                // 释放锁
                // LockReleaseAsync 会使用 Lua 脚本检查 value 是否匹配，防止误删别人的锁
                _database.LockRelease(_key, _value);
            }
        }
    }
}
