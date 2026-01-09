import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const bizSuccess = new Rate('biz_success');
const bizFailed = new Rate('biz_failed');
const statusCount = new Counter('status_count');
const statusDuration = new Trend('status_duration_ms', true);

// 鍘嬫祴閰嶇疆
export const options = {
  stages: [
    { duration: '5s', target: 10 },    // 鐑韩锛?绉掑唴鍗囪嚦 10 VUs
    { duration: '10s', target: 500 },  // 鐖嗗彂锛?0绉掑唴鍗囪嚦 500 VUs
    { duration: '30s', target: 500 },  // 鎸佺画锛氫繚鎸?500 VUs 鍘嬫祴 30绉?    { duration: '5s', target: 0 },     // 鍐峰嵈
  ],
  thresholds: {
    http_req_duration: ['p(95)<50'], // 95% 鐨勮姹傚搷搴旀椂闂村簲灏忎簬 50ms
    biz_failed: ['rate<0.01'],       // non-200/400 responses
    biz_success: ['rate>0.99'],      // 200/400 treated as business success
  },
};

export default function () {
  // 浠庣幆澧冨彉閲忚鍙?BASE_URL锛屽鏋滄湭璁剧疆鍒欓粯璁や负 http://localhost:5001
  // 娉ㄦ剰锛氭湰鍦扮洿鎺ヨ繍琛?k6 鏃朵娇鐢?localhost
  const baseUrl = __ENV.BASE_URL || 'http://localhost:5000';
  const url = `${baseUrl}/api/deduct`;
  
  const res = http.post(url);
  const ok = res.status === 200 || res.status === 400;

  // 楠岃瘉缁撴灉锛?  // 200: 鎶㈣喘鎴愬姛
  // 400: 搴撳瓨涓嶈冻 (杩欎篃鏄甯哥殑涓氬姟鍝嶅簲)
  check(res, {
    'status is 200 or 400': () => ok,
  });
  bizSuccess.add(ok);
  bizFailed.add(!ok);
  statusCount.add(1, { status: String(res.status) });
  statusDuration.add(res.timings.duration, { status: String(res.status) });

  // 妯℃嫙鐢ㄦ埛鐐瑰嚮闂撮殧 (0.1s)
  sleep(0.1);
}

