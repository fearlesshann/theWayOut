import http from 'k6/http';
import { check, sleep } from 'k6';

// 压测配置
export const options = {
  stages: [
    { duration: '5s', target: 10 },    // 热身：5秒内升至 10 VUs
    { duration: '10s', target: 500 },  // 爆发：10秒内升至 500 VUs
    { duration: '30s', target: 500 },  // 持续：保持 500 VUs 压测 30秒
    { duration: '5s', target: 0 },     // 冷却
  ],
  thresholds: {
    http_req_duration: ['p(95)<50'], // 95% 的请求响应时间应小于 50ms
    http_req_failed: ['rate<0.01'],  // 错误率应小于 1% (注意：400 库存不足也算业务成功，不算系统错误，但在 k6 默认算 failed)
  },
};

export default function () {
  // 从环境变量读取 BASE_URL，如果未设置则默认为 http://localhost:5001
  // 注意：本地直接运行 k6 时使用 localhost
  const baseUrl = __ENV.BASE_URL || 'http://localhost:5000';
  const url = `${baseUrl}/api/deduct`;
  
  const res = http.post(url);

  // 验证结果：
  // 200: 抢购成功
  // 400: 库存不足 (这也是正常的业务响应)
  check(res, {
    'status is 200 or 400': (r) => r.status === 200 || r.status === 400,
  });

  // 模拟用户点击间隔 (0.1s)
  sleep(0.1);
}
