# Lab 7 — Performance Testing: Load and Stress

## Objective

Learn to perform load and stress testing on a web API. Identify performance bottlenecks, establish baselines, and determine system breaking points.

## Prerequisites

- .NET 10 SDK or later installed
- C# fundamentals and ASP.NET Core basics
- Understanding of HTTP methods (GET, POST) and status codes
- For **Option A (NBomber)**: familiarity with xUnit test structure
- For **Option B (k6)**: Node.js or Homebrew/Chocolatey for installation; basic JavaScript knowledge
- A terminal capable of running long-lived processes (the API must stay running during tests)
- Recommended: a machine with at least 4 CPU cores and 8 GB RAM for meaningful results

## Key Concepts

| Concept | Description |
|---------|-------------|
| **Virtual User (VU)** | A simulated user that executes a test scenario in a loop. Each VU maintains its own HTTP connection and cookie state. |
| **Requests Per Second (RPS)** | The throughput of the system — how many HTTP requests the server handles each second. |
| **Percentile (p50 / p95 / p99)** | A statistical measure of response times. p95 = 95 % of requests completed within this duration. |
| **Smoke Test** | A minimal-load test (1-2 VUs) that validates the system works at all before heavier tests. |
| **Load Test** | Simulates expected, normal traffic to verify the system meets performance targets. |
| **Stress Test** | Pushes the system beyond normal capacity to find the breaking point. |
| **Spike Test** | A sudden burst of traffic to see how the system handles sharp peaks. |
| **Endurance (Soak) Test** | Runs at moderate load for an extended period to detect memory leaks and resource exhaustion. |
| **Error Rate** | Percentage of failed requests out of total requests. A key indicator of system health under load. |
| **Breaking Point** | The load level at which the system starts returning unacceptable error rates or response times. |

## Tools

- Language: C#
- API: ASP.NET Core Web API (system under test)
- Load Testing: [k6](https://k6.io/) or [NBomber](https://nbomber.com/)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`, for NBomber-based tests)

## Setup

### Option A — NBomber (C# native)

```bash
dotnet new sln -n Lab7
dotnet new webapi -n Lab7.Api
dotnet new classlib -n Lab7.Tests
dotnet sln add Lab7.Api Lab7.Tests
dotnet add Lab7.Tests reference Lab7.Api
dotnet add Lab7.Tests package xunit.v3
dotnet add Lab7.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab7.Tests package NBomber
dotnet add Lab7.Tests package NBomber.Http
```

### Option B — k6 (JavaScript-based)

```bash
# Install k6: https://k6.io/docs/getting-started/installation/
brew install k6    # macOS
choco install k6   # Windows
```

## Tasks

### Task 1 — Build the System Under Test

Create a simple ASP.NET Core API with:

- `GET /api/products` — returns a list of products (simulate DB delay with `Task.Delay`)
- `GET /api/products/{id}` — returns single product
- `POST /api/products` — creates a product
- `GET /api/products/search?q=term` — search with simulated heavy computation

**Example — Minimal Product Controller (NBomber path)**

```csharp
public record Product(int Id, string Name, decimal Price);

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private static readonly List<Product> _products = new()
    {
        new(1, "Widget", 9.99m),
        new(2, "Gadget", 24.99m),
        new(3, "Doohickey", 4.99m),
    };

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        await Task.Delay(50); // simulate DB latency
        return Ok(_products);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        await Task.Delay(20);
        var product = _products.FirstOrDefault(p => p.Id == id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        await Task.Delay(30);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        // Simulate heavy computation
        await Task.Delay(200);
        var results = _products.Where(p =>
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        return Ok(results);
    }
}
```

> **Hint**: Keep the `Task.Delay` values realistic but not too large. 50-200 ms simulates a typical database round-trip. This makes it easier to observe how response times degrade under load.

### Task 2 — Load Testing

Write load test scenarios that:

1. **Smoke test**: 1 virtual user, 1 minute — verify the API responds correctly under minimal load
2. **Average load test**: 50 virtual users, 5 minutes — simulate normal traffic
3. **Spike test**: Ramp from 10 to 200 virtual users in 30 seconds, then back to 10

For each scenario, collect and report:

- Average response time (p50)
- 95th percentile response time (p95)
- 99th percentile response time (p99)
- Requests per second (RPS)
- Error rate (%)

**Example — NBomber Smoke Test**

```csharp
using NBomber.CSharp;
using NBomber.Http.CSharp;

public class LoadTests
{
    [Fact]
    public void SmokeTest_SingleUser_ShouldRespondWithoutErrors()
    {
        using var httpClient = new HttpClient();

        var scenario = Scenario.Create("smoke_get_products", async context =>
        {
            var request = Http.CreateRequest("GET", "http://localhost:5000/api/products");
            var response = await Http.Send(httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 1, interval: TimeSpan.FromSeconds(1),
                              during: TimeSpan.FromMinutes(1))
        );

        var result = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Verify results
        var stats = result.ScenarioStats[0];
        Assert.True(stats.Fail.Request.Count == 0,
            $"Expected zero failures but got {stats.Fail.Request.Count}");
    }
}
```

**Example — k6 Average Load Test**

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // ramp up
    { duration: '4m',  target: 50 },  // hold
    { duration: '30s', target: 0 },   // ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95 % of requests must complete < 500 ms
    http_req_failed:   ['rate<0.01'],  // error rate < 1 %
  },
};

export default function () {
  const res = http.get('http://localhost:5000/api/products');
  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  sleep(1);
}
```

**Expected Behavior**

| Scenario | Expected p95 | Expected Error Rate | Expected RPS |
|----------|-------------|---------------------|-------------|
| Smoke (1 VU) | < 200 ms | 0 % | ~1 |
| Average load (50 VU) | < 500 ms | < 1 % | ~40-50 |
| Spike (200 VU) | < 2000 ms | < 5 % | varies |

> These are rough targets. Actual values depend on your hardware and `Task.Delay` settings. Record your real measurements and explain deviations.

**Minimum test count for Task 2**: 3 test methods/scripts (one per scenario).

> **Hint**: Always start the API in Release mode (`dotnet run -c Release`) for consistent results. Debug mode includes extra overhead that skews measurements.

### Task 3 — Stress Testing

Write stress test scenarios:

1. **Ramp-up stress test**: Gradually increase users from 10 to 500 over 10 minutes. Identify the breaking point where error rate exceeds 5%.
2. **Endurance test**: 50 virtual users for 15 minutes. Monitor for memory leaks or degradation over time.

Document:

- At what load does the API start failing?
- What is the maximum RPS before error rate exceeds 1%?
- Are there any memory leaks (compare memory usage at start vs end)?

**Example — NBomber Ramp-Up Stress Test**

```csharp
var scenario = Scenario.Create("stress_ramp_up", async context =>
{
    var request = Http.CreateRequest("GET", "http://localhost:5000/api/products");
    var response = await Http.Send(httpClient, request);
    return response;
})
.WithWarmUpDuration(TimeSpan.FromSeconds(10))
.WithLoadSimulations(
    Simulation.InjectPerSec(rate: 10,  during: TimeSpan.FromMinutes(2)),
    Simulation.InjectPerSec(rate: 50,  during: TimeSpan.FromMinutes(2)),
    Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromMinutes(2)),
    Simulation.InjectPerSec(rate: 250, during: TimeSpan.FromMinutes(2)),
    Simulation.InjectPerSec(rate: 500, during: TimeSpan.FromMinutes(2))
);
```

**Expected Behavior**

| Phase (RPS target) | Expected p95 | Expected Error Rate | Notes |
|---------------------|-------------|---------------------|-------|
| 10 RPS | < 200 ms | 0 % | Baseline / warm-up |
| 50 RPS | < 400 ms | 0 % | Normal capacity |
| 100 RPS | < 800 ms | < 1 % | Approaching limits |
| 250 RPS | < 2000 ms | 1-5 % | Degradation expected |
| 500 RPS | > 2000 ms | > 5 % | Breaking point likely |

**Minimum test count for Task 3**: 2 test methods/scripts (one ramp-up, one endurance).

> **Hint**: For the endurance test, capture memory usage at the start and end using `GC.GetTotalMemory(true)` or `dotnet-counters`. If memory grows monotonically over 15 minutes, that is a leak.

### Task 4 — Results Report

Create a `REPORT.md` with:

1. Test environment description (hardware, OS, .NET version)
2. Summary table of all test results
3. Charts/graphs of response times over duration (k6 or NBomber generates these)
4. Identified bottlenecks and recommended optimizations

> **Hint**: NBomber generates HTML reports automatically in the `reports/` directory after each run. k6 can output to JSON or InfluxDB for visualization. Include screenshots or links to these artifacts in your report.

## Grading

| Criteria |
|----------|
| Task 1 — API setup |
| Task 2 — Load test scenarios |
| Task 3 — Stress test scenarios |
| Task 4 — Results report |

## Submission

- API project and test scripts/projects
- `REPORT.md` with results, tables, and analysis
- Generated HTML reports from k6/NBomber

## References

- [NBomber Documentation](https://nbomber.com/docs/getting-started/overview/)
- [NBomber.Http Plugin](https://nbomber.com/docs/plugins/http/)
- [k6 Documentation](https://k6.io/docs/)
- [k6 Test Types (Smoke, Load, Stress, Spike, Soak)](https://grafana.com/docs/k6/latest/testing-guides/test-types/)
- [ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline)
- [Understanding Latency Percentiles (p50, p95, p99)](https://www.brendangregg.com/blog/2016-10-01/latency-heat-maps.html)
