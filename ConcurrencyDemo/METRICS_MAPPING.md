# Metrics Mapping (k6 <-> Prometheus)

This notes how the k6 load test metrics line up with the service metrics
exposed at `http://localhost:8080/metrics`.

## k6 custom metrics (loadtest.js)

- `biz_success` (Rate): 200/400 treated as business success
- `biz_failed` (Rate): non-200/400 treated as failure
- `status_count{status=...}` (Counter): response count by HTTP status
- `status_duration_ms{status=...}` (Trend): response duration by HTTP status
- `http_req_duration` (built-in): overall request duration

## Prometheus metrics (service)

- `http_requests_received_total{endpoint="/api/deduct",code="200|400|..."}`
- `http_request_duration_seconds_bucket{endpoint="/api/deduct",code=...,le=...}`
- `http_request_duration_seconds_sum` / `http_request_duration_seconds_count`
- `stock_consumed_total{material_id="1"}`
- `stock_added_total{material_id="1"}` (only when /api/add is used)
- `stock_write_errors_total`

## Suggested PromQL comparisons

- Requests by status (matches k6 `status_count`):
  `sum(rate(http_requests_received_total{endpoint="/api/deduct"}[1m])) by (code)`

- Business success rate (matches k6 `biz_success` / `biz_failed`):
  `sum(rate(http_requests_received_total{endpoint="/api/deduct",code=~"200|400"}[1m])) / sum(rate(http_requests_received_total{endpoint="/api/deduct"}[1m]))`

- P95 latency (matches k6 `http_req_duration`):
  `histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket{endpoint="/api/deduct"}[1m])) by (le))`

- Stock changes vs load test volume:
  `increase(stock_consumed_total{material_id="1"}[1m])`

- DB write errors:
  `increase(stock_write_errors_total[1m])`
