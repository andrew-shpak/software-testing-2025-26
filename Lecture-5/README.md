# Лекція 5: Тестування продуктивності з k6

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Explain why performance testing matters and identify different types of performance tests
- Describe key performance metrics: response time, throughput, error rate, and percentiles
- Install and configure k6 for performance testing
- Write k6 test scripts using JavaScript, including HTTP requests, checks, and thresholds
- Design load profiles with Virtual Users (VUs), scenarios, and executors
- Analyze k6 output and interpret results
- Apply performance testing to an ASP.NET Core API
- Recognize common performance anti-patterns
- Understand when to use BenchmarkDotNet for micro-benchmarks on the C# side

---

## 1. Чому тестування продуктивності має значення

### 1.1 Збої продуктивності в реальному світі

Функціональна коректність необхідна, але недостатня. Система, що повертає правильну відповідь за 30 секунд, для практичних цілей є зламаною.

| Incident | Year | Impact |
|---|---|---|
| Healthcare.gov launch | 2013 | Site crashed under load; only 6 users could register simultaneously |
| Amazon Prime Day outages | 2018 | Estimated $72M+ in lost sales during peak traffic |
| Twitter "Fail Whale" era | 2008-09 | Service collapsed during high-traffic events |
| Ticketmaster / Taylor Swift | 2022 | 3.5 billion requests overwhelmed systems; event canceled |
| BBC iPlayer Olympics | 2012 | Streaming service failed during opening ceremony peak |

> **Discussion (5 min):** Have you experienced a slow or unresponsive application? Did you abandon it? How long did you wait before giving up?

### 1.2 Вартість поганої продуктивності

Дослідження постійно показують, що продуктивність безпосередньо впливає на бізнес-результати:

```
Response Time vs. User Behavior:

  0-1 sec     ████████████████████████████████  Users feel the system is instant
  1-3 sec     ██████████████████████            Users notice delay but stay engaged
  3-5 sec     ████████████████                  Significant drop-off begins
  5-10 sec    ████████                          ~50% of users abandon
  10+ sec     ██                                Most users leave permanently

Source: Google / Akamai research on web performance
```

Key statistics:
- **Google:** A 0.5-second increase in search page load time caused a 20% drop in traffic
- **Amazon:** Every 100ms of latency costs roughly 1% in sales
- **Walmart:** Every 1-second improvement in page load time increased conversions by 2%

### 1.3 Тестування продуктивності vs. Функціональне тестування

| Aspect | Functional Testing | Performance Testing |
|---|---|---|
| **Question** | Does it work correctly? | Does it work fast enough under load? |
| **Focus** | Correctness of output | Speed, stability, scalability |
| **Load** | Single user / request | Many concurrent users / requests |
| **Pass/Fail** | Right answer vs. wrong answer | Within SLA vs. exceeds SLA |
| **When** | Every code change | Before releases, after infra changes |
| **Example** | "Login returns a token" | "Login completes in < 200ms at 1000 RPS" |

### 1.4 Коли проводити тестування продуктивності

```
Development Lifecycle and Performance Testing:

  Requirements ──► Design ──► Development ──► Testing ──► Staging ──► Production
                      │            │              │           │           │
                      ▼            ▼              ▼           ▼           ▼
                  Capacity     Micro-        Component    Full load   Monitoring
                  planning     benchmarks    perf tests   tests       & alerting
                  (estimates)  (BenchmarkDotNet) (k6)     (k6)        (APM tools)
```

Тестування продуктивності найцінніше:
- **Before major releases** — verify the system can handle expected traffic
- **After infrastructure changes** — new database, new cloud region, new hosting
- **After architecture changes** — new caching layer, new message queue, API redesign
- **Continuously in CI/CD** — lightweight smoke tests to catch regressions early

---

## 2. Типи тестування продуктивності

### 2.1 Огляд

Різні типи тестів продуктивності відповідають на різні питання:

```
                            Users / Load
                                │
  Spike Test               ┌───┤
                          │   │
                          │   │
  Stress Test         ────┤   │
                          │   │
                          │   │
  Load Test       ────────┤   │
                          │   │
                          │   │
  Soak Test       ────────┤   │  (long duration, moderate load)
                          │   │
                     ─────┘   │
                              └──────────────────────────────► Time
```

### 2.2 Навантажувальне тестування

**Question:** Can the system handle the expected number of concurrent users?

```
VUs
 │
 │        ┌──────────────────────┐
 │       /│                      │\
 │      / │    Steady State      │ \
 │     /  │   (target load)      │  \
 │    /   │                      │   \
 │   /    │                      │    \
 │  /     │                      │     \
 │ / Ramp │                      │ Ramp \
 │/  Up   │                      │ Down  \
 └────────┴──────────────────────┴────────► Time
     1m          5-10 min             1m
```

- Simulates **normal, expected traffic** levels
- Validates that performance meets SLAs under typical conditions
- Example: "100 concurrent users browsing and placing orders during business hours"

### 2.3 Стресове тестування

**Question:** What happens when load exceeds the expected maximum?

```
VUs
 │
 │                    ┌────────┐
 │                   /│ Beyond │\
 │                  / │  Max   │ \
 │        ┌────────┤  │        │  \
 │       /│ Normal │  │        │   \
 │      / │  Load  │  │        │    \
 │     /  │        │  │        │     \
 │    /   │        │  │        │      \
 │   /    │        │  │        │       \
 └────────┴────────┴──┴────────┴────────► Time
```

- Pushes the system **beyond normal operating capacity**
- Identifies the breaking point and failure behavior
- Answers: Does the system degrade gracefully or crash catastrophically?

### 2.4 Пікове тестування

**Question:** Can the system handle sudden, dramatic increases in traffic?

```
VUs
 │
 │          ▲
 │         / \
 │        /   \         ▲
 │       /     \       / \
 │      /       \     /   \
 │     /         \   /     \
 │    /           \ /       \
 │   /             V         \
 │──/                         \──
 └────────────────────────────────► Time
```

- Simulates **flash crowds**: product launches, breaking news, viral content
- Tests auto-scaling capabilities
- Example: "Traffic jumps from 100 to 10,000 users in 30 seconds"

### 2.5 Тривале (Endurance) тестування

**Question:** Does the system remain stable over extended periods?

```
VUs
 │
 │  ┌──────────────────────────────────────────────────┐
 │  │                                                  │
 │  │          Moderate, Constant Load                 │
 │  │          (4-24+ hours)                           │
 │  │                                                  │
 │  └──────────────────────────────────────────────────┘
 └──────────────────────────────────────────────────────► Time
                     Hours / Days
```

- Runs at **moderate load for extended duration** (hours or even days)
- Detects memory leaks, connection pool exhaustion, disk space issues, log file growth
- Example: "500 users continuously active for 8 hours"

### 2.6 Тестування масштабованості

**Question:** How does the system perform as resources or load scale up?

- Gradually increases load while adding resources (horizontal/vertical scaling)
- Measures whether performance scales linearly, sub-linearly, or hits a plateau
- Example: "Does doubling the servers double the throughput?"

### 2.7 Підсумкова таблиця

| Type | Load Level | Duration | Goal |
|---|---|---|---|
| **Load** | Normal/Expected | Minutes | Validate SLAs under typical use |
| **Stress** | Beyond maximum | Minutes | Find the breaking point |
| **Spike** | Sudden burst | Seconds-Minutes | Test auto-scaling and recovery |
| **Soak** | Moderate, constant | Hours-Days | Find memory leaks, resource exhaustion |
| **Scalability** | Incrementally increasing | Varies | Measure scaling characteristics |

> **Discussion (5 min):** For an e-commerce platform, which type of performance test is most important before Black Friday? What about for a banking API?

---

## 3. Метрики тестування продуктивності

### 3.1 Основні метрики

Розуміння цих метрик є необхідним для інтерпретації результатів тестів продуктивності:

#### Час відповіді (Затримка)

Час між відправкою запиту та отриманням повної відповіді.

```
Client                                              Server
  │                                                    │
  │─── Request ──────────────────────────────────────►│
  │                                                    │  Processing
  │                                                    │  Time
  │◄── Response ─────────────────────────────────────│
  │                                                    │
  │◄──────────── Response Time ──────────────────────►│
```

- **Includes:** Network latency + server processing time + response transfer time
- **Measured in:** Milliseconds (ms) or seconds (s)
- **Typical SLAs:** API < 200ms, web page < 2s, database query < 50ms

#### Пропускна здатність

Кількість запитів, які система обробляє за одиницю часу.

- **Measured in:** Requests per second (RPS) or transactions per second (TPS)
- **Higher is better** (assuming acceptable response times)
- Example: "The API handles 5,000 RPS with p95 latency under 100ms"

#### Частота помилок

Відсоток запитів, що призводять до помилок (HTTP 4xx/5xx, тайм-аути, збої з'єднання).

- **Measured in:** Percentage (%)
- **Target:** < 0.1% for most production systems
- **Watch for:** Error rate increasing under load indicates the system is failing

#### Одночасні користувачі / Віртуальні користувачі (VUs)

Кількість імітованих користувачів, що активно надсилають запити одночасно.

- Not the same as "total users" — concurrent users are those actively sending requests
- A system with 10,000 registered users might have 500 concurrent users at peak

### 3.2 Перцентилі: Чому середні значення брешуть

**Середні значення приховують викиди.** Розглянемо цей сценарій:

```
9 requests at 50ms + 1 request at 5000ms

Average: (9 * 50 + 5000) / 10 = 545ms
Median (p50): 50ms

The average suggests a half-second response time,
but 90% of users experienced 50ms.
The 1 unlucky user waited 5 seconds.
```

Перцентилі дають більш точну картину:

| Percentile | Meaning | Use Case |
|---|---|---|
| **p50 (Median)** | 50% of requests are faster | "Typical" user experience |
| **p90** | 90% of requests are faster | Most users' experience |
| **p95** | 95% of requests are faster | Common SLA target |
| **p99** | 99% of requests are faster | Worst-case for almost all users |
| **p99.9** | 99.9% of requests are faster | Tail latency (1 in 1000 requests) |

```
Response Time Distribution:

Requests
 │
 │  ██
 │  ██
 │  ████
 │  ████
 │  ██████
 │  ████████
 │  ████████
 │  ██████████
 │  ██████████████
 │  ████████████████████                              ██
 └──────────────────────────────────────────────────────► Response Time
    p50   p90 p95    p99                             p99.9
   (50ms)(80ms)(120ms)(500ms)                       (5000ms)
            │                                          │
      Most users here                          Tail latency (outliers)
```

> **Key insight:** Always monitor p95 and p99, not just the average. If your average is 100ms but your p99 is 10 seconds, 1% of your users are having a terrible experience.

### 3.3 Інші важливі метрики

| Metric | Description | Why It Matters |
|---|---|---|
| **TTFB** | Time to First Byte | Server responsiveness |
| **Connection time** | Time to establish TCP/TLS connection | Network / infrastructure health |
| **Data transferred** | Total bytes sent/received | Bandwidth utilization |
| **Iteration duration** | Time for one complete user scenario | End-to-end user experience |
| **Request rate** | Requests per second actually achieved | Throughput under load |
| **VU count** | Active virtual users at each moment | Load profile verification |

> **Discussion (5 min):** An API has an average response time of 200ms and a p99 of 8 seconds. Is this acceptable? What might cause such a large gap between average and p99?

---

## 4. Вступ до k6

### 4.1 Що таке k6?

**k6** — це інструмент навантажувального тестування з відкритим кодом, розроблений для розробників. Був придбаний Grafana Labs у 2021 році.

Ключові характеристики:
- **Developer-centric** — tests are written in JavaScript (ES6+)
- **CLI-first** — runs from the command line, integrates with CI/CD
- **Performance-focused** — the engine is written in Go for efficiency
- **Scriptable** — full JavaScript API for complex test scenarios
- **Extensible** — supports custom metrics, outputs, and extensions

### 4.2 Архітектура k6

```
┌─────────────────────────────────────────────────────────────┐
│  k6 Process                                                 │
│                                                             │
│  ┌──────────────────┐   ┌──────────────────────────────┐   │
│  │  Go Runtime      │   │  JavaScript VM (goja)        │   │
│  │                  │   │                              │   │
│  │  - HTTP client   │◄──│  - Test script execution     │   │
│  │  - Metrics       │   │  - VU lifecycle management   │   │
│  │  - Scheduling    │   │  - Checks and thresholds     │   │
│  │  - Output        │   │  - Custom logic              │   │
│  │                  │   │                              │   │
│  └──────────────────┘   └──────────────────────────────┘   │
│           │                                                 │
│           ▼                                                 │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Output Modules                                      │   │
│  │  - Console (stdout)                                  │   │
│  │  - JSON file                                         │   │
│  │  - CSV file                                          │   │
│  │  - InfluxDB, Prometheus, Grafana Cloud, Datadog      │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
         │
         ▼
  ┌──────────────┐
  │ Target System │  (your ASP.NET Core API, web server, etc.)
  └──────────────┘
```

- The **Go runtime** handles the heavy lifting: HTTP connections, metric collection, scheduling
- The **JavaScript VM** (goja, a Go-based JS engine) executes your test scripts
- This design means k6 is **much more efficient** than tools that run real browsers or Node.js per VU

### 4.3 Чому k6?

| Feature | k6 | JMeter | Gatling | Locust |
|---|---|---|---|---|
| **Language** | JavaScript | XML/GUI | Scala/Java | Python |
| **Developer friendly** | Very high | Low (GUI-based) | Medium | High |
| **Resource efficiency** | Very high (Go) | Medium (JVM) | High (JVM) | Medium (Python) |
| **CI/CD integration** | Native | Possible | Good | Possible |
| **Learning curve** | Low (if you know JS) | Medium | Medium-High | Low |
| **VUs per machine** | 10,000+ | 1,000-5,000 | 5,000-10,000 | 1,000-5,000 |
| **Scripting power** | Full JS | Limited | Full Scala | Full Python |

> **Discussion (3 min):** Why might writing performance tests in JavaScript be advantageous, even when your application is built in C#?

---

## 5. Встановлення k6

### 5.1 Встановлення

#### macOS

```bash
brew install k6
```

#### Windows

```bash
# Using Chocolatey
choco install k6

# Using winget
winget install k6 --source winget
```

#### Linux (Debian/Ubuntu)

```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
  --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" \
  | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

#### Docker

```bash
docker run --rm -i grafana/k6 run - < script.js
```

### 5.2 Перевірка встановлення

```bash
k6 version
# Output: k6 v1.2.0 (go1.23.4, linux/amd64)
```

### 5.3 Ваш перший запуск k6

Create a file `hello.js`:

```javascript
import http from 'k6/http';
import { sleep } from 'k6';

export default function () {
  http.get('https://test.k6.io');
  sleep(1);
}
```

Run it:

```bash
k6 run hello.js
```

Ви повинні побачити вивід з метриками, такими як `http_req_duration`, `http_reqs` та `vus`.

---

## 6. Анатомія скрипта k6

### 6.1 Чотири етапи життєвого циклу

Скрипт k6 має чотири окремі етапи, що виконуються в певному порядку:

```javascript
// 1. INIT CODE — runs once per VU, before anything else
//    Used for: imports, reading files, defining options
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

const BASE_URL = 'https://api.example.com';

// 2. SETUP — runs once (total), before the test starts
//    Used for: creating test data, authentication tokens, DB seeding
export function setup() {
  const loginRes = http.post(`${BASE_URL}/auth/login`, JSON.stringify({
    username: 'testuser',
    password: 'testpass',
  }), { headers: { 'Content-Type': 'application/json' } });

  const token = loginRes.json('token');
  return { token }; // passed to default function and teardown
}

// 3. VU CODE (default function) — runs repeatedly for each VU
//    This is where the actual load testing happens
export default function (data) {
  const params = {
    headers: { Authorization: `Bearer ${data.token}` },
  };

  const res = http.get(`${BASE_URL}/api/items`, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });

  sleep(1); // simulate user think time
}

// 4. TEARDOWN — runs once (total), after all VUs finish
//    Used for: cleanup, deleting test data, sending notifications
export function teardown(data) {
  // Clean up test data, close connections, etc.
  http.del(`${BASE_URL}/api/test-data`, null, {
    headers: { Authorization: `Bearer ${data.token}` },
  });
}
```

### 6.2 Потік виконання життєвого циклу

```
┌────────────────────────────────────────────────────────────────┐
│                                                                │
│  INIT (once per VU)                                            │
│  ├── Import modules                                            │
│  ├── Define options                                            │
│  └── Read local files, define constants                        │
│                                                                │
│  SETUP (once total)                                            │
│  ├── Create test data                                          │
│  ├── Get auth tokens                                           │
│  └── Return shared data ──────────────────┐                    │
│                                           │                    │
│  VU CODE (repeated per VU)                ▼                    │
│  ┌──────────────────────────────────────────────────┐          │
│  │  VU 1: default(data) → iteration 1, 2, 3, ...   │          │
│  │  VU 2: default(data) → iteration 1, 2, 3, ...   │          │
│  │  VU 3: default(data) → iteration 1, 2, 3, ...   │          │
│  │  ...                                             │          │
│  │  VU N: default(data) → iteration 1, 2, 3, ...   │          │
│  └──────────────────────────────────────────────────┘          │
│                                           │                    │
│  TEARDOWN (once total)                    ▼                    │
│  ├── Clean up test data                                        │
│  └── Receives same data from setup                             │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

### 6.3 Важливі правила

| Rule | Detail |
|---|---|
| Init code cannot make HTTP requests | Use `setup()` for HTTP calls that run once |
| `setup()` runs **outside** of the VU context | It does not count toward VU metrics |
| Data from `setup()` is serialized | Passed as JSON to each VU and to `teardown()` |
| `default` function is the only code that generates load metrics | All other stages are overhead |
| `sleep()` simulates user think time | Without it, VUs will hammer the server unrealistically |

---

## 7. Віртуальні користувачі (VUs) та ітерації

### 7.1 Що таке віртуальний користувач?

A **Virtual User (VU)** is a simulated user that executes the `default` function in a loop. Each VU:
- Is an independent "thread" of execution
- Has its own cookie jar, TLS session, and TCP connections
- Runs the `default` function repeatedly until the test ends

```
VU 1: ──[iteration 1]──[iteration 2]──[iteration 3]──► ...
VU 2: ──[iteration 1]──[iteration 2]──[iteration 3]──► ...
VU 3: ──[iteration 1]──[iteration 2]──[iteration 3]──► ...
```

### 7.2 Налаштування VUs та тривалості

```javascript
// Option 1: Fixed VUs for a duration
export const options = {
  vus: 10,          // 10 concurrent virtual users
  duration: '30s',  // run for 30 seconds
};

// Option 2: Fixed VUs with a set number of iterations
export const options = {
  vus: 10,
  iterations: 100,  // 100 total iterations shared across all VUs
};

// Option 3: Via command line (overrides script options)
// k6 run --vus 50 --duration 1m script.js
```

### 7.3 VUs vs. Ітерації vs. Запити

Ці терміни часто плутають:

```
1 VU, 1 iteration of default():

export default function () {
  http.get('/api/users');       // Request 1
  http.get('/api/users/1');     // Request 2
  http.post('/api/users', ...); // Request 3
  sleep(1);
}

Result: 1 iteration = 3 HTTP requests
        10 VUs x 30 iterations = 300 iterations = 900 HTTP requests
```

| Term | Definition |
|---|---|
| **VU** | A simulated user running the default function in a loop |
| **Iteration** | One complete execution of the `default` function |
| **Request** | A single HTTP call within an iteration |

### 7.4 Час роздумів

Real users do not fire requests as fast as possible. They read pages, fill forms, and click buttons. **Think time** simulates this behavior:

```javascript
import { sleep } from 'k6';

export default function () {
  http.get('/api/products');
  sleep(1);                      // 1 second think time (fixed)

  http.get('/api/products/42');
  sleep(Math.random() * 3);     // 0-3 seconds (random)
}
```

Without `sleep()`, each VU sends requests as fast as the server responds, which may not represent realistic user behavior (but can be appropriate for stress testing).

---

## 8. Сценарії та виконавці k6

### 8.1 Що таке сценарії?

Сценарії дозволяють визначити **кілька незалежних робочих навантажень** в одному тестовому скрипті. Кожен сценарій може використовувати інший виконавець, профіль навантаження та навіть цільову функцію.

### 8.2 Виконавці

Executors control **how k6 schedules VUs and iterations**:

| Executor | Description | Use Case |
|---|---|---|
| `constant-vus` | Fixed number of VUs for a fixed duration | Simple load test |
| `ramping-vus` | VUs ramp up and down over stages | Classic ramp-up / steady / ramp-down |
| `constant-arrival-rate` | Fixed request rate regardless of response time | SLA-based testing |
| `ramping-arrival-rate` | Request rate ramps up and down | Gradually increasing load |
| `per-vu-iterations` | Each VU runs a fixed number of iterations | Controlled iteration count |
| `shared-iterations` | Total iterations shared across all VUs | Exact number of total iterations |
| `externally-controlled` | VUs controlled via k6 REST API | Interactive / manual control |

### 8.3 constant-vus

The simplest executor. Runs a fixed number of VUs for a fixed duration:

```javascript
export const options = {
  scenarios: {
    my_scenario: {
      executor: 'constant-vus',
      vus: 50,
      duration: '5m',
    },
  },
};
```

### 8.4 ramping-vus

The most common executor for realistic load tests. Defines stages to ramp VUs up and down:

```javascript
export const options = {
  scenarios: {
    load_test: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '2m', target: 50 },   // ramp up to 50 VUs over 2 min
        { duration: '5m', target: 50 },   // stay at 50 VUs for 5 min
        { duration: '2m', target: 100 },  // ramp up to 100 VUs
        { duration: '5m', target: 100 },  // stay at 100 VUs for 5 min
        { duration: '2m', target: 0 },    // ramp down to 0
      ],
    },
  },
};
```

```
VUs
 │
100│                    ┌───────────────────┐
   │                   /│                   │\
 50│  ┌───────────────┤ │                   │ \
   │ /│               │ │                   │  \
   │/ │               │ │                   │   \
  0│  │               │ │                   │    \
   └──┴───────────────┴─┴───────────────────┴─────► Time
     0   2m         7m 9m                14m  16m
```

### 8.5 constant-arrival-rate

Instead of controlling the number of VUs, this executor maintains a **constant request rate**. k6 automatically adjusts VUs to achieve the target rate:

```javascript
export const options = {
  scenarios: {
    constant_load: {
      executor: 'constant-arrival-rate',
      rate: 100,              // 100 iterations per timeUnit
      timeUnit: '1s',         // = 100 iterations per second
      duration: '5m',
      preAllocatedVUs: 50,    // pre-allocate 50 VUs
      maxVUs: 200,            // allow up to 200 VUs if needed
    },
  },
};
```

Why use this?

- With `constant-vus`, if the server slows down, VUs send requests slower (back-pressure)
- With `constant-arrival-rate`, the request rate stays constant regardless of response time
- This better simulates real traffic patterns where users do not wait for each other

### 8.6 ramping-arrival-rate

Combines arrival rate control with ramp-up stages:

```javascript
export const options = {
  scenarios: {
    ramping_load: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 500,
      stages: [
        { duration: '2m', target: 50 },   // ramp to 50 RPS
        { duration: '5m', target: 50 },   // hold at 50 RPS
        { duration: '2m', target: 200 },  // ramp to 200 RPS
        { duration: '5m', target: 200 },  // hold at 200 RPS
        { duration: '2m', target: 0 },    // ramp down
      ],
    },
  },
};
```

### 8.7 Кілька сценаріїв

Ви можете запускати різні робочі навантаження одночасно:

```javascript
export const options = {
  scenarios: {
    browse_products: {
      executor: 'constant-vus',
      vus: 50,
      duration: '10m',
      exec: 'browseProducts', // calls the browseProducts function
    },
    place_orders: {
      executor: 'constant-arrival-rate',
      rate: 5,
      timeUnit: '1s',
      duration: '10m',
      preAllocatedVUs: 10,
      maxVUs: 50,
      exec: 'placeOrder', // calls the placeOrder function
    },
  },
};

// Named function for the browse scenario
export function browseProducts() {
  http.get(`${BASE_URL}/api/products`);
  sleep(2);
}

// Named function for the order scenario
export function placeOrder() {
  http.post(`${BASE_URL}/api/orders`, JSON.stringify({
    productId: 1,
    quantity: 1,
  }), { headers: { 'Content-Type': 'application/json' } });
}
```

### 8.8 Choosing the Right Executor

```
What are you trying to test?
         │
         ├── "Can we handle N concurrent users?"
         │        └── constant-vus or ramping-vus
         │
         ├── "Can we sustain N requests per second?"
         │        └── constant-arrival-rate or ramping-arrival-rate
         │
         ├── "Run exactly N iterations total"
         │        └── shared-iterations or per-vu-iterations
         │
         └── "I need to manually control VUs during the test"
                  └── externally-controlled
```

> **Discussion (5 min):** You need to test an API that must handle 1,000 requests per second for Black Friday. Which executor would you choose and why? What if the requirement is "500 concurrent users"?

---

## 9. Writing k6 Scripts: HTTP Requests, Checks, Thresholds

### 9.1 HTTP Requests

k6 provides methods for all standard HTTP verbs:

```javascript
import http from 'k6/http';

export default function () {
  // GET
  const getRes = http.get('https://api.example.com/users');

  // POST with JSON body
  const postRes = http.post(
    'https://api.example.com/users',
    JSON.stringify({
      name: 'Alice',
      email: 'alice@example.com',
    }),
    {
      headers: { 'Content-Type': 'application/json' },
    }
  );

  // PUT
  const putRes = http.put(
    'https://api.example.com/users/1',
    JSON.stringify({
      name: 'Alice Updated',
    }),
    {
      headers: { 'Content-Type': 'application/json' },
    }
  );

  // PATCH
  const patchRes = http.patch(
    'https://api.example.com/users/1',
    JSON.stringify({
      email: 'newalice@example.com',
    }),
    {
      headers: { 'Content-Type': 'application/json' },
    }
  );

  // DELETE
  const delRes = http.del('https://api.example.com/users/1');
}
```

### 9.2 Working with Responses

```javascript
export default function () {
  const res = http.get('https://api.example.com/users');

  // Status code
  console.log(`Status: ${res.status}`);          // 200

  // Response body (string)
  console.log(`Body: ${res.body}`);

  // Parse JSON response
  const users = res.json();                       // parsed JSON object
  const firstUser = res.json('users.0.name');     // JSONPath-like access

  // Response headers
  console.log(`Content-Type: ${res.headers['Content-Type']}`);

  // Timing information
  console.log(`Duration: ${res.timings.duration}ms`);
  console.log(`Waiting (TTFB): ${res.timings.waiting}ms`);
  console.log(`Connecting: ${res.timings.connecting}ms`);
  console.log(`TLS handshake: ${res.timings.tls_handshaking}ms`);
  console.log(`Sending: ${res.timings.sending}ms`);
  console.log(`Receiving: ${res.timings.receiving}ms`);
}
```

### 9.3 Response Timing Breakdown

```
│◄──────────────── http_req_duration ────────────────►│
│                                                      │
│  ┌─────────┬─────────┬────────┬──────────┬────────┐ │
│  │ blocked │ connect │ TLS    │ sending  │waiting │ │receiving│
│  │         │ (TCP)   │ handshk│ (request)│ (TTFB) │ │(response)│
│  └─────────┴─────────┴────────┴──────────┴────────┘ │
│                                                      │
│◄─────── http_req_connecting ──►│                     │
│◄──── http_req_tls_handshaking ─────►│                │
│                                     │◄ http_req_sending │
│                                                │◄waiting│
│                                                       │◄receiving│
```

### 9.4 Checks

Checks are **assertions** that verify response properties. Unlike thresholds, a failed check does **not** stop the test or mark it as failed.

```javascript
import { check } from 'k6';
import http from 'k6/http';

export default function () {
  const res = http.get('https://api.example.com/users');

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response body is not empty': (r) => r.body.length > 0,
    'response time < 500ms': (r) => r.timings.duration < 500,
    'content type is JSON': (r) =>
      r.headers['Content-Type'] && r.headers['Content-Type'].includes('application/json'),
  });

  // Checks on JSON response
  const res2 = http.get('https://api.example.com/users/1');

  check(res2, {
    'user has an id': (r) => r.json('id') !== undefined,
    'user name is not empty': (r) => r.json('name') !== '',
    'user has valid email': (r) => r.json('email').includes('@'),
  });
}
```

Check results appear in the output:

```
✓ status is 200
✓ response body is not empty
✗ response time < 500ms
  ↳  94% — ✓ 4700 / ✗ 300
✓ content type is JSON
```

### 9.5 Thresholds

Thresholds define **pass/fail criteria** for the entire test. If any threshold fails, k6 exits with a non-zero exit code (useful for CI/CD).

```javascript
export const options = {
  vus: 50,
  duration: '5m',
  thresholds: {
    // 95th percentile response time must be < 500ms
    http_req_duration: ['p(95)<500'],

    // 99th percentile response time must be < 1500ms
    'http_req_duration': ['p(95)<500', 'p(99)<1500'],

    // Error rate must be < 1%
    http_req_failed: ['rate<0.01'],

    // At least 95% of checks must pass
    checks: ['rate>0.95'],

    // Throughput must be at least 100 RPS
    http_reqs: ['rate>100'],

    // Specific check threshold
    'checks{check_name:status is 200}': ['rate>0.99'],
  },
};
```

### 9.6 Checks vs. Thresholds

| Feature | Checks | Thresholds |
|---|---|---|
| **Scope** | Per-request validation | Aggregated over entire test |
| **Failure behavior** | Logged but test continues | Fails the test (non-zero exit code) |
| **Use case** | "Was this response correct?" | "Did the system meet SLAs overall?" |
| **CI/CD gate** | No (informational) | Yes (can block deployment) |
| **Syntax** | `check(res, { ... })` | `thresholds: { metric: [...] }` |

**Best practice:** Use checks to validate individual responses, and thresholds to enforce overall performance requirements.

---

## 10. Working with Headers, Cookies, and Authentication

### 10.1 Custom Headers

```javascript
import http from 'k6/http';

export default function () {
  const params = {
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
      'X-Request-ID': `req-${__VU}-${__ITER}`,
      'X-Custom-Header': 'my-value',
    },
  };

  http.get('https://api.example.com/data', params);
}
```

### 10.2 Cookies

k6 automatically handles cookies via a per-VU cookie jar:

```javascript
import http from 'k6/http';

export default function () {
  // Cookies from Set-Cookie headers are automatically stored
  http.get('https://example.com/login');  // may set session cookie

  // Subsequent requests automatically send stored cookies
  http.get('https://example.com/dashboard');

  // Manually set a cookie
  const jar = http.cookieJar();
  jar.set('https://example.com', 'my_cookie', 'cookie_value');

  // Access cookies from a response
  const res = http.get('https://example.com/api');
  const cookies = res.cookies;
  console.log(`Session ID: ${cookies.session_id[0].value}`);
}
```

### 10.3 Authentication Patterns

#### Bearer Token (JWT)

```javascript
import http from 'k6/http';

const BASE_URL = 'https://api.example.com';

// Get token once in setup, share with all VUs
export function setup() {
  const loginRes = http.post(`${BASE_URL}/auth/login`, JSON.stringify({
    username: 'loadtest_user',
    password: 'loadtest_pass',
  }), {
    headers: { 'Content-Type': 'application/json' },
  });

  const token = loginRes.json('token');

  if (!token) {
    throw new Error('Failed to get auth token');
  }

  return { token };
}

export default function (data) {
  const authHeaders = {
    headers: {
      Authorization: `Bearer ${data.token}`,
      'Content-Type': 'application/json',
    },
  };

  http.get(`${BASE_URL}/api/protected-resource`, authHeaders);
}
```

#### Basic Authentication

```javascript
import http from 'k6/http';
import encoding from 'k6/encoding';

export default function () {
  const credentials = encoding.b64encode('username:password');

  const res = http.get('https://api.example.com/secure', {
    headers: {
      Authorization: `Basic ${credentials}`,
    },
  });
}
```

#### API Key

```javascript
import http from 'k6/http';

export default function () {
  // API key in header
  http.get('https://api.example.com/data', {
    headers: { 'X-API-Key': 'your-api-key-here' },
  });

  // API key as query parameter
  http.get('https://api.example.com/data?api_key=your-api-key-here');
}
```

---

## 11. Custom Metrics and Tags

### 11.1 Built-in Metrics

k6 collects many metrics automatically:

| Metric | Type | Description |
|---|---|---|
| `http_reqs` | Counter | Total number of HTTP requests |
| `http_req_duration` | Trend | Total request duration (ms) |
| `http_req_blocked` | Trend | Time waiting for a free TCP connection |
| `http_req_connecting` | Trend | Time establishing TCP connection |
| `http_req_tls_handshaking` | Trend | Time for TLS handshake |
| `http_req_sending` | Trend | Time sending request data |
| `http_req_waiting` | Trend | Time waiting for response (TTFB) |
| `http_req_receiving` | Trend | Time receiving response data |
| `http_req_failed` | Rate | Percentage of failed requests |
| `iteration_duration` | Trend | Time to complete one full iteration |
| `iterations` | Counter | Total number of completed iterations |
| `vus` | Gauge | Current number of active VUs |
| `data_received` | Counter | Amount of data received (bytes) |
| `data_sent` | Counter | Amount of data sent (bytes) |
| `checks` | Rate | Percentage of passed checks |

### 11.2 Custom Metrics

k6 supports four types of custom metrics:

```javascript
import http from 'k6/http';
import { check } from 'k6';
import { Counter, Gauge, Rate, Trend } from 'k6/metrics';

// Define custom metrics
const orderCount = new Counter('orders_created');           // Cumulative count
const activeUsers = new Gauge('active_users');              // Current value
const successRate = new Rate('order_success_rate');          // Percentage
const orderDuration = new Trend('order_processing_time');   // Statistical distribution

export const options = {
  thresholds: {
    // Set thresholds on custom metrics
    order_processing_time: ['p(95)<2000', 'avg<1000'],
    order_success_rate: ['rate>0.95'],
  },
};

export default function () {
  const start = Date.now();

  const res = http.post('https://api.example.com/orders', JSON.stringify({
    productId: 1,
    quantity: 1,
  }), {
    headers: { 'Content-Type': 'application/json' },
  });

  const duration = Date.now() - start;
  const success = res.status === 201;

  // Record custom metrics
  orderCount.add(1);                    // increment counter
  activeUsers.add(__VU);                // set gauge
  successRate.add(success);             // add to rate (true = success)
  orderDuration.add(duration);          // add data point to trend
}
```

### 11.3 Tags

Tags add metadata to metrics, enabling filtering and grouping in analysis:

```javascript
import http from 'k6/http';
import { check } from 'k6';

export default function () {
  // Add tags to a request
  const res = http.get('https://api.example.com/products', {
    tags: {
      name: 'GetProducts',       // name the request for the summary
      type: 'api',
      feature: 'catalog',
    },
  });

  // Tags on checks
  check(res, {
    'products returned': (r) => r.status === 200,
  }, { feature: 'catalog' });
}
```

You can use tags in thresholds:

```javascript
export const options = {
  thresholds: {
    // Threshold only for requests tagged with name:GetProducts
    'http_req_duration{name:GetProducts}': ['p(95)<300'],
    'http_req_duration{name:CreateOrder}': ['p(95)<1000'],
  },
};
```

### 11.4 Groups

Groups organize requests into logical transactions:

```javascript
import http from 'k6/http';
import { group, sleep } from 'k6';

export default function () {
  group('User Login Flow', function () {
    http.get('https://example.com/login');
    http.post('https://example.com/login', JSON.stringify({
      username: 'user',
      password: 'pass',
    }));
  });

  group('Browse Products', function () {
    http.get('https://example.com/products');
    http.get('https://example.com/products/1');
    http.get('https://example.com/products/2');
  });

  group('Place Order', function () {
    http.post('https://example.com/orders', JSON.stringify({
      productId: 1,
      quantity: 2,
    }));
  });

  sleep(1);
}
```

---

## 12. Load Profiles

### 12.1 Classic Load Test Profile

The most common load test pattern: ramp-up, steady state, ramp-down.

```javascript
export const options = {
  stages: [
    { duration: '2m', target: 100 },   // Ramp up
    { duration: '5m', target: 100 },   // Steady state
    { duration: '2m', target: 0 },     // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};
```

### 12.2 Stress Test Profile

Incrementally increase load to find the breaking point:

```javascript
export const options = {
  stages: [
    { duration: '2m', target: 100 },    // Normal load
    { duration: '5m', target: 100 },    // Stay at normal
    { duration: '2m', target: 200 },    // Above normal
    { duration: '5m', target: 200 },    // Stay above normal
    { duration: '2m', target: 400 },    // Push toward breaking point
    { duration: '5m', target: 400 },    // Stay and observe
    { duration: '2m', target: 800 },    // Beyond capacity
    { duration: '5m', target: 800 },    // Stay and observe failures
    { duration: '5m', target: 0 },      // Ramp down and observe recovery
  ],
};
```

### 12.3 Spike Test Profile

Sudden dramatic load increase:

```javascript
export const options = {
  stages: [
    { duration: '1m', target: 10 },     // Normal traffic
    { duration: '10s', target: 1000 },  // Sudden spike!
    { duration: '3m', target: 1000 },   // Stay at spike level
    { duration: '10s', target: 10 },    // Spike ends
    { duration: '2m', target: 10 },     // Recovery period
    { duration: '1m', target: 0 },      // Ramp down
  ],
};
```

### 12.4 Soak Test Profile

Moderate load for an extended duration:

```javascript
export const options = {
  stages: [
    { duration: '5m', target: 200 },     // Ramp up
    { duration: '8h', target: 200 },     // Sustain for 8 hours
    { duration: '5m', target: 0 },       // Ramp down
  ],
};
```

### 12.5 Choosing the Right Profile

| Goal | Profile | Typical Duration |
|---|---|---|
| Validate SLAs | Classic load test | 10-30 minutes |
| Find breaking point | Stress test (stepped) | 30-60 minutes |
| Test auto-scaling | Spike test | 5-15 minutes |
| Detect resource leaks | Soak test | 4-24 hours |

---

## 13. Analyzing k6 Output and Results

### 13.1 Console Output

When you run `k6 run script.js`, you get a summary like this:

```
          /\      |‾‾| /‾‾/   /‾‾/
     /\  /  \     |  |/  /   /  /
    /  \/    \    |     (   /   ‾‾\
   /          \   |  |\  \ |  (‾)  |
  / __________ \  |__| \__\ \_____/ .io

  execution: local
     script: load-test.js
     output: -

  scenarios: (100.00%) 1 scenario, 50 max VUs, 10m30s max duration
           ✓ default: 50 looping VUs for 10m0s (gracefulStop: 30s)

     ✓ status is 200............: 99.85% ✓ 29955  ✗ 45
     ✓ response time < 500ms...: 97.20% ✓ 29160  ✗ 840

     checks.........................: 98.52% ✓ 59115  ✗ 885
     data_received..................: 125 MB  209 kB/s
     data_sent......................: 3.4 MB  5.7 kB/s
     http_req_blocked...............: avg=1.2ms   min=0s     med=0s     max=502ms  p(90)=0s     p(95)=0s
     http_req_connecting............: avg=0.8ms   min=0s     med=0s     max=450ms  p(90)=0s     p(95)=0s
   ✓ http_req_duration..............: avg=45.2ms  min=2.1ms  med=32ms   max=4.2s   p(90)=89ms   p(95)=142ms
       { expected_response:true }...: avg=42.1ms  min=2.1ms  med=30ms   max=3.8s   p(90)=85ms   p(95)=135ms
   ✓ http_req_failed................: 0.15%  ✓ 45    ✗ 29955
     http_req_receiving.............: avg=0.3ms   min=0s     med=0.2ms  max=45ms   p(90)=0.5ms  p(95)=0.8ms
     http_req_sending...............: avg=0.1ms   min=0s     med=0.1ms  max=12ms   p(90)=0.2ms  p(95)=0.3ms
     http_req_tls_handshaking.......: avg=0.9ms   min=0s     med=0s     max=380ms  p(90)=0s     p(95)=0s
     http_req_waiting...............: avg=44.8ms  min=1.8ms  med=31ms   max=4.1s   p(90)=88ms   p(95)=141ms
     http_reqs......................: 30000  50/s
     iteration_duration.............: avg=1.05s   min=1s     med=1.03s  max=5.2s   p(90)=1.09s  p(95)=1.15s
     iterations.....................: 30000  50/s
     vus............................: 50     min=50  max=50
     vus_max........................: 50     min=50  max=50
```

### 13.2 Reading the Output

Key elements to focus on:

```
1. Checks section:
   ✓ status is 200............: 99.85%   <-- 99.85% of responses had status 200
   ✗ response time < 500ms...: 97.20%   <-- 2.8% of requests were slower than 500ms

2. Thresholds (✓ = passed, ✗ = failed):
   ✓ http_req_duration......: p(95)<500   <-- 95th percentile was under 500ms
   ✓ http_req_failed........: rate<0.01   <-- error rate was under 1%

3. Key metrics:
   http_req_duration:  avg=45.2ms  p(90)=89ms  p(95)=142ms
                       ▲            ▲            ▲
                       Average      90% under    95% under
                       (less        89ms         142ms
                       useful)

4. Throughput:
   http_reqs: 30000  50/s         <-- 50 requests per second sustained
```

### 13.3 JSON Output

Export detailed results for post-processing:

```bash
# Export to JSON (every data point)
k6 run --out json=results.json script.js

# Export to CSV
k6 run --out csv=results.csv script.js
```

### 13.4 Summary Export

Export the end-of-test summary to a JSON file:

```javascript
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';

export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: '  ', enableColors: true }),
    'summary.json': JSON.stringify(data, null, 2),
  };
}
```

### 13.5 Visualizing Results with Grafana

For production-grade performance testing, send metrics to a time-series database and visualize with Grafana:

```bash
# Run k6 with InfluxDB output
k6 run --out influxdb=http://localhost:8086/k6 script.js

# Run k6 with Prometheus remote write
k6 run --out experimental-prometheus-rw script.js
```

```
┌──────────┐          ┌──────────┐          ┌──────────┐
│   k6     │ ──────►  │ InfluxDB │ ──────►  │ Grafana  │
│  (test)  │  metrics │  (TSDB)  │  query   │ (charts) │
└──────────┘          └──────────┘          └──────────┘
```

> **Discussion (5 min):** Looking at the example output above, is this system performing well? What metrics would concern you? What would you investigate further?

---

## 14. Lifecycle Hooks: setup() and teardown()

### 14.1 Practical Use Cases

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  vus: 20,
  duration: '5m',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

// SETUP: Create test data and authenticate
export function setup() {
  // 1. Create a test user
  const signupRes = http.post(`${BASE_URL}/api/auth/signup`, JSON.stringify({
    username: `loadtest_${Date.now()}`,
    email: `loadtest_${Date.now()}@test.com`,
    password: 'TestPass123!',
  }), { headers: { 'Content-Type': 'application/json' } });

  check(signupRes, {
    'signup successful': (r) => r.status === 201,
  });

  // 2. Login to get token
  const loginRes = http.post(`${BASE_URL}/api/auth/login`, JSON.stringify({
    username: `loadtest_${Date.now()}`,
    password: 'TestPass123!',
  }), { headers: { 'Content-Type': 'application/json' } });

  const token = loginRes.json('token');
  const userId = loginRes.json('userId');

  // 3. Seed test products
  const productIds = [];
  for (let i = 0; i < 10; i++) {
    const res = http.post(`${BASE_URL}/api/products`, JSON.stringify({
      name: `Test Product ${i}`,
      price: 9.99 + i,
    }), {
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
    });
    productIds.push(res.json('id'));
  }

  // Return data shared with all VUs and teardown
  return { token, userId, productIds };
}

// VU CODE: The actual load test
export default function (data) {
  const headers = {
    Authorization: `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  };

  // Browse products
  const listRes = http.get(`${BASE_URL}/api/products`, { headers });
  check(listRes, { 'list products OK': (r) => r.status === 200 });

  // View a random product
  const randomProductId = data.productIds[
    Math.floor(Math.random() * data.productIds.length)
  ];
  const detailRes = http.get(
    `${BASE_URL}/api/products/${randomProductId}`,
    { headers }
  );
  check(detailRes, { 'product detail OK': (r) => r.status === 200 });

  sleep(1);
}

// TEARDOWN: Clean up test data
export function teardown(data) {
  const headers = {
    Authorization: `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  };

  // Delete test products
  for (const id of data.productIds) {
    http.del(`${BASE_URL}/api/products/${id}`, null, { headers });
  }

  // Delete test user
  http.del(`${BASE_URL}/api/users/${data.userId}`, null, { headers });

  console.log('Teardown complete: test data cleaned up.');
}
```

### 14.2 Important Notes on setup/teardown

| Aspect | Detail |
|---|---|
| **Execution count** | `setup()` and `teardown()` each run exactly **once** per test |
| **VU context** | They run outside the VU context (not counted in VU metrics) |
| **Data passing** | `setup()` returns data that is **serialized (JSON)** and passed to `default()` and `teardown()` |
| **No functions** | You cannot pass functions, class instances, or closures via setup data |
| **Failure** | If `setup()` fails, the test aborts |

---

## 15. Testing an ASP.NET Core API with k6

### 15.1 Example API

Consider a simple ASP.NET Core minimal API for a task management system:

```csharp
// Program.cs (simplified)
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<TaskDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.MapGet("/api/tasks", async (TaskDbContext db) =>
    await db.Tasks.ToListAsync());

app.MapGet("/api/tasks/{id:int}", async (int id, TaskDbContext db) =>
    await db.Tasks.FindAsync(id) is TaskItem task
        ? Results.Ok(task)
        : Results.NotFound());

app.MapPost("/api/tasks", async (TaskItem task, TaskDbContext db) =>
{
    db.Tasks.Add(task);
    await db.SaveChangesAsync();
    return Results.Created($"/api/tasks/{task.Id}", task);
});

app.MapPut("/api/tasks/{id:int}", async (int id, TaskItem input, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    task.Title = input.Title;
    task.IsCompleted = input.IsCompleted;
    await db.SaveChangesAsync();
    return Results.Ok(task);
});

app.MapDelete("/api/tasks/{id:int}", async (int id, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();
    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
```

### 15.2 k6 Test Script for the Task API

```javascript
// task-api-load-test.js
import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const taskCreationTime = new Trend('task_creation_time');
const taskCreationSuccess = new Rate('task_creation_success');

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  scenarios: {
    smoke: {
      executor: 'constant-vus',
      vus: 1,
      duration: '30s',
      tags: { test_type: 'smoke' },
    },
    load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 20 },
        { duration: '3m', target: 20 },
        { duration: '1m', target: 0 },
      ],
      startTime: '40s', // start after smoke test
      tags: { test_type: 'load' },
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500', 'p(99)<1500'],
    http_req_failed: ['rate<0.01'],
    task_creation_time: ['p(95)<1000'],
    task_creation_success: ['rate>0.95'],
    checks: ['rate>0.98'],
  },
};

const headers = { 'Content-Type': 'application/json' };

export default function () {
  let taskId;

  // Create a task
  group('Create Task', function () {
    const payload = JSON.stringify({
      title: `Task from VU ${__VU} iter ${__ITER}`,
      isCompleted: false,
    });

    const res = http.post(`${BASE_URL}/api/tasks`, payload, { headers });

    const success = check(res, {
      'create: status is 201': (r) => r.status === 201,
      'create: has task id': (r) => r.json('id') !== undefined,
    });

    taskCreationTime.add(res.timings.duration);
    taskCreationSuccess.add(success);

    if (success) {
      taskId = res.json('id');
    }
  });

  sleep(0.5);

  // List all tasks
  group('List Tasks', function () {
    const res = http.get(`${BASE_URL}/api/tasks`, { headers });

    check(res, {
      'list: status is 200': (r) => r.status === 200,
      'list: returns array': (r) => Array.isArray(r.json()),
    });
  });

  sleep(0.5);

  // Get specific task (if created successfully)
  if (taskId) {
    group('Get Task', function () {
      const res = http.get(`${BASE_URL}/api/tasks/${taskId}`, { headers });

      check(res, {
        'get: status is 200': (r) => r.status === 200,
        'get: correct id': (r) => r.json('id') === taskId,
      });
    });

    sleep(0.3);

    // Update the task
    group('Update Task', function () {
      const payload = JSON.stringify({
        title: `Updated Task ${taskId}`,
        isCompleted: true,
      });

      const res = http.put(`${BASE_URL}/api/tasks/${taskId}`, payload, { headers });

      check(res, {
        'update: status is 200': (r) => r.status === 200,
        'update: task is completed': (r) => r.json('isCompleted') === true,
      });
    });

    sleep(0.3);

    // Delete the task (cleanup)
    group('Delete Task', function () {
      const res = http.del(`${BASE_URL}/api/tasks/${taskId}`);

      check(res, {
        'delete: status is 204': (r) => r.status === 204,
      });
    });
  }

  sleep(1);
}
```

### 15.3 Running the Test

```bash
# Start your ASP.NET Core API
dotnet run --project src/TaskApi

# In another terminal, run the k6 test
k6 run task-api-load-test.js

# Override the base URL
k6 run -e BASE_URL=http://staging.example.com:5000 task-api-load-test.js

# Override VU count from CLI
k6 run --vus 100 --duration 10m task-api-load-test.js
```

### 15.4 Environment Variables in k6

```javascript
// Access environment variables with __ENV
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.API_KEY || 'default-key';

// Set from CLI:
// k6 run -e BASE_URL=https://staging.api.com -e API_KEY=secret123 script.js
```

### 15.5 Useful k6 Built-in Variables

| Variable | Description |
|---|---|
| `__VU` | Current Virtual User number (1-based) |
| `__ITER` | Current iteration number for this VU (0-based) |
| `__ENV` | Object containing environment variables |

---

## 16. Common Performance Anti-Patterns

### 16.1 Application-Level Anti-Patterns

| Anti-Pattern | Symptom | Solution |
|---|---|---|
| **N+1 Queries** | Response time grows linearly with data | Use eager loading, batch queries |
| **Missing database indexes** | Slow queries under load | Add appropriate indexes |
| **Synchronous I/O** | Thread pool exhaustion under load | Use async/await consistently |
| **No connection pooling** | Connection errors at moderate load | Configure connection pool sizes |
| **Unbounded queries** | Memory spikes, timeouts | Add pagination (LIMIT/OFFSET) |
| **No caching** | Repeated expensive computations | Add response/data caching |
| **Large payloads** | High bandwidth, slow responses | Paginate, compress, use partial responses |
| **Memory leaks** | Degradation over time (soak test) | Profile memory, use `IDisposable` |

### 16.2 Testing Anti-Patterns

| Anti-Pattern | Problem | Fix |
|---|---|---|
| **No think time** | Unrealistic load (each VU hammers the server) | Add `sleep()` between requests |
| **Testing from localhost** | Network latency not measured | Test against staging/remote environment |
| **Single endpoint only** | Misses contention between endpoints | Test realistic user flows |
| **Ignoring ramp-up** | Sudden load is not realistic | Use `ramping-vus` with warm-up stage |
| **Not cleaning up data** | Test data accumulates, skews results | Use `setup()`/`teardown()` |
| **Running once** | Results may not be reproducible | Run multiple times, compare results |
| **Only checking averages** | Missing tail latency problems | Always check p95 and p99 |
| **No baseline** | Cannot tell if performance improved or degraded | Establish baseline metrics first |

### 16.3 Example: The N+1 Query Problem

```csharp
// BAD: N+1 queries — 1 query for orders + N queries for items
app.MapGet("/api/orders", async (OrderDbContext db) =>
{
    var orders = await db.Orders.ToListAsync();
    // Each order.Items access triggers a separate query!
    return orders.Select(o => new {
        o.Id,
        o.CustomerName,
        Items = o.Items.Select(i => i.Name) // N additional queries
    });
});

// GOOD: Eager loading — 1 query with JOIN
app.MapGet("/api/orders", async (OrderDbContext db) =>
{
    var orders = await db.Orders
        .Include(o => o.Items)  // JOIN in a single query
        .ToListAsync();
    return orders.Select(o => new {
        o.Id,
        o.CustomerName,
        Items = o.Items.Select(i => i.Name)
    });
});
```

k6 will reveal this: under load, the N+1 version shows response times growing linearly with data volume, while the eager loading version stays constant.

> **Discussion (5 min):** You run a soak test and notice that response times slowly increase over 4 hours. What are the likely causes? How would you diagnose this?

---

## 17. BenchmarkDotNet: Micro-Benchmarks in C#

### 17.1 When to Use BenchmarkDotNet vs. k6

| Aspect | k6 | BenchmarkDotNet |
|---|---|---|
| **What it tests** | System under load (HTTP, end-to-end) | Individual methods/algorithms |
| **Perspective** | External (client-side) | Internal (code-level) |
| **Measures** | Response time, throughput, error rate | Execution time, memory allocations |
| **Environment** | Runs against a deployed server | Runs in-process, no server needed |
| **Use case** | "Can the API handle 1000 RPS?" | "Is StringBuilder faster than string concat?" |
| **Language** | JavaScript | C# |

### 17.2 BenchmarkDotNet Example

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Text;

BenchmarkRunner.Run<StringBenchmarks>();

[MemoryDiagnoser]
public class StringBenchmarks
{
    private const int N = 1000;

    [Benchmark(Baseline = true)]
    public string StringConcat()
    {
        var result = "";
        for (int i = 0; i < N; i++)
            result += i.ToString();
        return result;
    }

    [Benchmark]
    public string StringBuilder_Append()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < N; i++)
            sb.Append(i);
        return sb.ToString();
    }

    [Benchmark]
    public string StringJoin()
    {
        return string.Join("", Enumerable.Range(0, N));
    }
}
```

Run in Release mode (required for accurate results):

```bash
dotnet run -c Release
```

Example output:

```
|            Method |         Mean |      Error |     StdDev | Ratio |     Gen0 |    Gen1 | Allocated |
|------------------ |-------------:|-----------:|-----------:|------:|---------:|--------:|----------:|
|      StringConcat | 1,245.678 us | 24.123 us | 22.567 us |  1.00 | 998.0469 | 15.6250 | 4,089 KB  |
| StringBuilder     |     8.456 us |  0.167 us |  0.156 us |  0.01 |   1.8311 |  0.0305 |     7 KB  |
|        StringJoin |    12.345 us |  0.245 us |  0.229 us |  0.01 |   1.4648 |  0.0305 |     6 KB  |
```

### 17.3 Key Takeaway

- Use **k6** to test how your **system** performs under load (HTTP-level, end-to-end)
- Use **BenchmarkDotNet** to test how individual **code paths** perform (method-level, in-process)
- They are complementary: BenchmarkDotNet helps you optimize hot paths that k6 identifies as slow

---

## 18. Putting It All Together: A Complete Test Suite

### 18.1 Smoke Test

A minimal test to verify the system is working before running heavier tests:

```javascript
// smoke-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_duration: ['p(99)<1500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/tasks`);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time OK': (r) => r.timings.duration < 1000,
  });

  sleep(1);
}
```

### 18.2 Test Execution Strategy

Run tests in order, from lightest to heaviest:

```
1. Smoke Test    ──► System up and responding?
       │
       ▼ (pass)
2. Load Test     ──► Meets SLAs under expected load?
       │
       ▼ (pass)
3. Stress Test   ──► Where does it break?
       │
       ▼ (pass)
4. Spike Test    ──► Handles sudden traffic bursts?
       │
       ▼ (pass)
5. Soak Test     ──► Stable over extended periods?
```

```bash
# Run tests in sequence
k6 run smoke-test.js && \
k6 run load-test.js && \
k6 run stress-test.js
```

### 18.3 Integrating k6 with CI/CD

k6 can run in GitHub Actions as a quality gate:

```yaml
# .github/workflows/performance.yml
name: Performance Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  performance:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build and start API
        run: |
          dotnet build src/TaskApi
          dotnet run --project src/TaskApi &
          sleep 5  # wait for API to start

      - name: Install k6
        run: |
          sudo gpg -k
          sudo gpg --no-default-keyring \
            --keyring /usr/share/keyrings/k6-archive-keyring.gpg \
            --keyserver hkp://keyserver.ubuntu.com:80 \
            --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
          echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] \
            https://dl.k6.io/deb stable main" | \
            sudo tee /etc/apt/sources.list.d/k6.list
          sudo apt-get update
          sudo apt-get install k6

      - name: Run smoke test
        run: k6 run tests/performance/smoke-test.js

      - name: Run load test
        run: k6 run tests/performance/load-test.js
```

If any threshold fails, k6 returns a non-zero exit code, which fails the CI pipeline and blocks the deployment.

---

## 19. Practical Exercise

### Task: Performance Test a REST API

You are given an ASP.NET Core API for a bookstore (or use any API you have from previous labs). Write a k6 test suite that includes:

1. **Smoke test** — 1 VU, 30 seconds, verify all endpoints respond correctly
2. **Load test** — ramp up to 50 VUs, hold for 3 minutes, ramp down
3. **Stress test** — incrementally increase to 200 VUs in steps

**Requirements:**
- Test at least 3 different HTTP methods (GET, POST, DELETE)
- Use `check()` to validate all responses
- Define meaningful `thresholds` (response time p95 < 500ms, error rate < 1%)
- Use at least one custom metric
- Use `setup()` for authentication or test data creation
- Use `teardown()` for cleanup
- Group requests into logical user flows using `group()`

**Bonus:**
- Create a spike test profile
- Use `constant-arrival-rate` executor to maintain a fixed RPS
- Export results to JSON and analyze the summary

> **Discussion (15 min):** Review your test results as a class. Compare response times across different load levels. At what point did the system start degrading? What metrics indicated the problem first?

---

## 20. Summary

### Ключові висновки

1. **Performance testing is not optional** — functional correctness means nothing if the system cannot handle real-world load
2. **Different test types answer different questions** — load, stress, spike, and soak tests each reveal different failure modes
3. **Percentiles matter more than averages** — always monitor p95 and p99, not just the mean response time
4. **k6 is developer-friendly** — JavaScript scripts, CLI-first design, efficient Go runtime, native CI/CD integration
5. **Script lifecycle matters** — understand init, setup, VU code (default), and teardown stages
6. **Choose the right executor** — `ramping-vus` for user-count-based tests, `constant-arrival-rate` for RPS-based tests
7. **Checks validate individual responses; thresholds enforce overall SLAs** — use both together
8. **Custom metrics and tags** enable fine-grained analysis of specific operations
9. **Test progressively** — smoke, then load, then stress, then spike, then soak
10. **BenchmarkDotNet complements k6** — use it for micro-level C# code optimization

### Анонс наступної лекції

In **Lecture 6: Test Design Techniques and Code Coverage**, we will:
- Explore black-box test design techniques: equivalence partitioning, boundary value analysis, decision tables
- Learn white-box techniques: statement coverage, branch coverage, condition coverage
- Set up code coverage with Coverlet and ReportGenerator
- Understand how much coverage is enough and when to stop testing
- Apply coverage metrics to guide test improvement

---

## Посилання та додаткова література

- **k6 Documentation** — https://grafana.com/docs/k6/latest/
- **k6 Examples and Guides** — https://grafana.com/docs/k6/latest/examples/
- **k6 JavaScript API Reference** — https://grafana.com/docs/k6/latest/javascript-api/
- **BenchmarkDotNet Documentation** — https://benchmarkdotnet.org/articles/overview.html
- **"The Art of Capacity Planning"** — John Allspaw (O'Reilly, 2nd edition, 2017)
- **Google Web Performance Research** — https://web.dev/performance/
- **ISTQB Performance Testing Syllabus** — https://www.istqb.org/certifications/performance-testing
- **ASP.NET Core Performance Best Practices** — https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices
