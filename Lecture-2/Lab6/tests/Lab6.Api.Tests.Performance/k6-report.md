# k6 Report Examples

Приклади звітів k6 для кожного типу тесту Orders API.

---

## Smoke Test

```
k6 run scripts/smoke-test.ts
```

```
         /\      Grafana   /‾‾/
    /\  /  \     |\  __   /  /
   /  \/    \    | |/ /  /   ‾‾\
  /          \   |   (  |  (‾)  |
 / __________ \  |_|\_\  \_____/

     execution: local
        script: scripts/smoke-test.ts
        output: -

     scenarios: (100.00%) 1 scenario, 1 max VUs, 1m0s max duration (incl. graceful stop):
              * default: 1 looping VUs for 30s (gracefulStop: 30s)


     ✓ GET /api/orders returns 200
     ✓ POST /api/orders returns 201
     ✓ GET /api/orders/{id} returns 200
     ✓ DELETE /api/orders/{id} returns 204

     checks.........................: 100.00% 80 out of 80
     data_received..................: 42 kB   1.4 kB/s
     data_sent......................: 12 kB   390 B/s
     http_req_blocked...............: avg=1.2ms    min=1µs     med=2µs     max=24.1ms  p(90)=3µs     p(95)=3µs
     http_req_connecting............: avg=0.8ms    min=0µs     med=0µs     max=16.3ms  p(90)=0µs     p(95)=0µs
   ✓ http_req_duration..............: avg=12.5ms   min=3.1ms   med=10.2ms  max=48.7ms  p(90)=22.4ms  p(95)=31.6ms
       { expected_response:true }...: avg=12.5ms   min=3.1ms   med=10.2ms  max=48.7ms  p(90)=22.4ms  p(95)=31.6ms
   ✓ http_req_failed................: 0.00%   0 out of 80
     http_req_receiving.............: avg=0.1ms    min=0.02ms  med=0.06ms  max=0.8ms   p(90)=0.15ms  p(95)=0.2ms
     http_req_sending...............: avg=0.03ms   min=0.01ms  med=0.02ms  max=0.2ms   p(90)=0.05ms  p(95)=0.08ms
     http_req_tls_handshaking.......: avg=0µs      min=0µs     med=0µs     max=0µs     p(90)=0µs     p(95)=0µs
     http_req_waiting...............: avg=12.3ms   min=3.0ms   med=10.1ms  max=48.5ms  p(90)=22.2ms  p(95)=31.4ms
     http_reqs......................: 80      2.666/s
     iteration_duration.............: avg=1.05s    min=1.02s   med=1.04s   max=1.12s   p(90)=1.09s   p(95)=1.10s
     iterations.....................: 20      0.666/s
     vus............................: 1       min=1        max=1
     vus_max........................: 1       min=1        max=1


running (0m30.0s), 0/1 VUs, 20 complete and 0 interrupted iterations
default ✓ [======================================] 1 VUs  30s
```

**Висновок:** API працездатне, всі ендпоінти відповідають коректно, p(95) = 31.6ms (поріг < 500ms).

---

## Load Test

```
k6 run scripts/load-test.ts
```

```
         /\      Grafana   /‾‾/
    /\  /  \     |\  __   /  /
   /  \/    \    | |/ /  /   ‾‾\
  /          \   |   (  |  (‾)  |
 / __________ \  |_|\_\  \_____/

     execution: local
        script: scripts/load-test.ts
        output: -

     scenarios: (100.00%) 1 scenario, 10 max VUs, 5m30s max duration (incl. graceful stop):
              * default: Up to 10 looping VUs for 5m0s over 3 stages (gracefulStop: 30s)


     ✓ GET /api/orders returns 200
     ✓ POST /api/orders returns 201
     ✓ GET /api/orders/{id} returns 200

     checks.........................: 100.00% 6840 out of 6840
     data_received..................: 2.1 MB  7.0 kB/s
     data_sent......................: 580 kB  1.9 kB/s
     http_req_blocked...............: avg=0.02ms   min=1µs     med=2µs     max=25.3ms  p(90)=3µs     p(95)=4µs
     http_req_connecting............: avg=0.01ms   min=0µs     med=0µs     max=17.1ms  p(90)=0µs     p(95)=0µs
   ✓ http_req_duration..............: avg=18.4ms   min=2.8ms   med=14.6ms  max=187.3ms p(90)=35.2ms  p(95)=52.8ms
       { expected_response:true }...: avg=18.4ms   min=2.8ms   med=14.6ms  max=187.3ms p(90)=35.2ms  p(95)=52.8ms
   ✓ http_req_failed................: 0.00%   0 out of 6840
     http_req_receiving.............: avg=0.08ms   min=0.01ms  med=0.05ms  max=3.2ms   p(90)=0.12ms  p(95)=0.18ms
     http_req_sending...............: avg=0.02ms   min=0.01ms  med=0.02ms  max=0.5ms   p(90)=0.04ms  p(95)=0.06ms
     http_req_tls_handshaking.......: avg=0µs      min=0µs     med=0µs     max=0µs     p(90)=0µs     p(95)=0µs
     http_req_waiting...............: avg=18.3ms   min=2.7ms   med=14.5ms  max=187.1ms p(90)=35.0ms  p(95)=52.6ms
     http_reqs......................: 6840    22.8/s
     iteration_duration.............: avg=1.06s    min=1.01s   med=1.05s   max=1.38s   p(90)=1.11s   p(95)=1.14s
     iterations.....................: 2280    7.6/s
     vus............................: 1       min=1        max=10
     vus_max........................: 10      min=10       max=10


running (5m00.0s), 00/10 VUs, 2280 complete and 0 interrupted iterations
default ✓ [======================================] 00/10 VUs  5m0s
```

**Висновок:** API стабільно працює при 10 VUs, p(95) = 52.8ms, 0% помилок, ~22.8 req/s.

---

## Stress Test

```
k6 run scripts/stress-test.ts
```

```
         /\      Grafana   /‾‾/
    /\  /  \     |\  __   /  /
   /  \/    \    | |/ /  /   ‾‾\
  /          \   |   (  |  (‾)  |
 / __________ \  |_|\_\  \_____/

     execution: local
        script: scripts/stress-test.ts
        output: -

     scenarios: (100.00%) 1 scenario, 100 max VUs, 11m30s max duration (incl. graceful stop):
              * default: Up to 100 looping VUs for 11m0s over 7 stages (gracefulStop: 30s)


     ✓ GET /api/orders status is 200
     ✓ POST /api/orders status is 201

     checks.........................: 99.87%  29812 out of 29850
     data_received..................: 9.4 MB  14.2 kB/s
     data_sent......................: 2.8 MB  4.2 kB/s
     http_req_blocked...............: avg=0.04ms   min=1µs     med=2µs     max=32.7ms  p(90)=3µs     p(95)=4µs
     http_req_connecting............: avg=0.02ms   min=0µs     med=0µs     max=19.4ms  p(90)=0µs     p(95)=0µs
   ✓ http_req_duration..............: avg=87.3ms   min=2.5ms   med=45.6ms  max=1856ms  p(90)=198.4ms p(95)=412.7ms
       { expected_response:true }...: avg=85.1ms   min=2.5ms   med=44.8ms  max=1856ms  p(90)=195.2ms p(95)=408.3ms
   ✓ http_req_failed................: 0.12%   38 out of 29850
     http_req_receiving.............: avg=0.14ms   min=0.01ms  med=0.05ms  max=18.6ms  p(90)=0.18ms  p(95)=0.32ms
     http_req_sending...............: avg=0.03ms   min=0.01ms  med=0.02ms  max=2.1ms   p(90)=0.05ms  p(95)=0.08ms
     http_req_tls_handshaking.......: avg=0µs      min=0µs     med=0µs     max=0µs     p(90)=0µs     p(95)=0µs
     http_req_waiting...............: avg=87.1ms   min=2.4ms   med=45.4ms  max=1855ms  p(90)=198.1ms p(95)=412.3ms
     http_reqs......................: 29850   45.2/s
     iteration_duration.............: avg=1.77s    min=0.51s   med=1.14s   max=4.52s   p(90)=3.12s   p(95)=3.68s
     iterations.....................: 9950    15.07/s
     vus............................: 2       min=2        max=100
     vus_max........................: 100     min=100      max=100


running (11m00.2s), 000/100 VUs, 9950 complete and 0 interrupted iterations
default ✓ [======================================] 000/100 VUs  11m0s
```

**Висновок:** API витримує 100 VUs з p(95) = 412.7ms (поріг < 1000ms). 0.12% помилок при піковому навантаженні. Деградація починається при ~50 VUs.

---

## Spike Test

```
k6 run scripts/spike-test.ts
```

```
         /\      Grafana   /‾‾/
    /\  /  \     |\  __   /  /
   /  \/    \    | |/ /  /   ‾‾\
  /          \   |   (  |  (‾)  |
 / __________ \  |_|\_\  \_____/

     execution: local
        script: scripts/spike-test.ts
        output: -

     scenarios: (100.00%) 1 scenario, 200 max VUs, 4m0s max duration (incl. graceful stop):
              * default: Up to 200 looping VUs for 3m20s over 6 stages (gracefulStop: 30s)


     ✓ GET /api/orders status is 200
     ✓ POST /api/orders succeeded

     checks.........................: 98.42%  14320 out of 14550
     data_received..................: 4.8 MB  24.0 kB/s
     data_sent......................: 1.4 MB  7.0 kB/s
     http_req_blocked...............: avg=0.08ms   min=1µs     med=2µs     max=45.6ms  p(90)=4µs     p(95)=5µs
     http_req_connecting............: avg=0.05ms   min=0µs     med=0µs     max=28.3ms  p(90)=0µs     p(95)=0µs
   ✓ http_req_duration..............: avg=245.8ms  min=2.1ms   med=68.4ms  max=4521ms  p(90)=612.3ms p(95)=1284.5ms
       { expected_response:true }...: avg=198.6ms  min=2.1ms   med=52.3ms  max=3842ms  p(90)=489.7ms p(95)=987.2ms
   ✓ http_req_failed................: 1.58%   230 out of 14550
     http_req_receiving.............: avg=0.21ms   min=0.01ms  med=0.05ms  max=24.8ms  p(90)=0.24ms  p(95)=0.52ms
     http_req_sending...............: avg=0.04ms   min=0.01ms  med=0.02ms  max=3.8ms   p(90)=0.06ms  p(95)=0.1ms
     http_req_tls_handshaking.......: avg=0µs      min=0µs     med=0µs     max=0µs     p(90)=0µs     p(95)=0µs
     http_req_waiting...............: avg=245.5ms  min=2.0ms   med=68.2ms  max=4520ms  p(90)=611.9ms p(95)=1283.8ms
     http_reqs......................: 14550   72.75/s
     iteration_duration.............: avg=1.04s    min=0.31s   med=0.52s   max=5.83s   p(90)=2.14s   p(95)=3.21s
     iterations.....................: 4850    24.25/s
     vus............................: 3       min=3        max=200
     vus_max........................: 200     min=200      max=200


running (3m20.0s), 000/200 VUs, 4850 complete and 0 interrupted iterations
default ✓ [======================================] 000/200 VUs  3m20s
```

**Висновок:** API відновлюється після сплеску до 200 VUs. p(95) = 1284.5ms (поріг < 2000ms). 1.58% помилок (поріг < 5%). Час відновлення після піку — ~30 секунд.

---

## Порівняння результатів

| Метрика | Smoke (1 VU) | Load (10 VUs) | Stress (100 VUs) | Spike (200 VUs) |
|---------|:------------:|:-------------:|:-----------------:|:---------------:|
| **p(95) latency** | 31.6ms | 52.8ms | 412.7ms | 1284.5ms |
| **p(99) latency** | 48.7ms | 187.3ms | 1856ms | 4521ms |
| **Error rate** | 0% | 0% | 0.12% | 1.58% |
| **Requests/s** | 2.6 | 22.8 | 45.2 | 72.7 |
| **Thresholds** | PASS | PASS | PASS | PASS |

## Як зберегти звіт

```bash
# JSON-формат (для автоматичного аналізу)
k6 run --out json=report.json scripts/load-test.ts

# CSV-формат
k6 run --out csv=report.csv scripts/load-test.ts

# HTML-звіт (потрібен k6-reporter)
k6 run --out json=report.json scripts/load-test.ts
# Потім конвертувати: npx k6-html-reporter -j report.json -o report.html
```
