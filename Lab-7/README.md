# Лабораторна 7 — Тестування продуктивності: навантаження та стрес

## Мета

Навчитися виконувати навантажувальне та стрес-тестування веб-API. Виявляти вузькі місця продуктивності, встановлювати базові показники та визначати межі працездатності системи.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений .NET 10 SDK або новіший
- Основи C# та ASP.NET Core
- Розуміння HTTP-методів (GET, POST) та кодів стану
- Для **варіанту A (NBomber)**: знайомство зі структурою тестів xUnit
- Для **варіанту B (k6)**: Node.js або Homebrew/Chocolatey для встановлення; базові знання JavaScript
- Термінал, здатний запускати довготривалі процеси (API має працювати під час тестів)
- Рекомендовано: машина з щонайменше 4 ядрами CPU та 8 ГБ RAM для значущих результатів

## Ключові концепції

| Концепція | Опис |
|-----------|------|
| **Віртуальний користувач (VU)** | Симульований користувач, який виконує тестовий сценарій у циклі. Кожен VU підтримує власне HTTP-з'єднання та стан cookie. |
| **Запити на секунду (RPS)** | Пропускна здатність системи — кількість HTTP-запитів, які сервер обробляє щосекунди. |
| **Перцентиль (p50 / p95 / p99)** | Статистична міра часу відповіді. p95 = 95 % запитів виконано за цей час. |
| **Smoke-тест** | Тест з мінімальним навантаженням (1-2 VU), який перевіряє, що система взагалі працює перед більш важкими тестами. |
| **Навантажувальний тест** | Симулює очікуваний, нормальний трафік для перевірки відповідності системи цільовим показникам продуктивності. |
| **Стрес-тест** | Навантажує систему понад нормальну потужність для знаходження точки відмови. |
| **Spike-тест** | Раптовий сплеск трафіку для перевірки, як система справляється з різкими піками. |
| **Тест на витривалість (Soak)** | Працює при помірному навантаженні протягом тривалого періоду для виявлення витоків пам'яті та вичерпання ресурсів. |
| **Частка помилок** | Відсоток невдалих запитів від загальної кількості запитів. Ключовий показник стану системи під навантаженням. |
| **Точка відмови** | Рівень навантаження, при якому система починає повертати неприйнятну частку помилок або час відповіді. |

## Інструменти

- Мова: C#
- API: ASP.NET Core Web API (система, що тестується)
- Навантажувальне тестування: [k6](https://k6.io/) або [NBomber](https://nbomber.com/)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`, для тестів на основі NBomber)

## Налаштування

### Варіант A — NBomber (нативний C#)

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

### Варіант B — k6 (на основі JavaScript)

```bash
# Встановлення k6: https://k6.io/docs/getting-started/installation/
brew install k6    # macOS
choco install k6   # Windows
```

## Завдання

### Завдання 1 — Побудова системи, що тестується

Створіть простий ASP.NET Core API з:

- `GET /api/products` — повертає список продуктів (симуляція затримки БД через `Task.Delay`)
- `GET /api/products/{id}` — повертає окремий продукт
- `POST /api/products` — створює продукт
- `GET /api/products/search?q=term` — пошук з симуляцією важких обчислень

**Приклад — мінімальний контролер продуктів (шлях NBomber)**

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
        await Task.Delay(50); // симуляція затримки БД
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
        // Симуляція важких обчислень
        await Task.Delay(200);
        var results = _products.Where(p =>
            p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        return Ok(results);
    }
}
```

> **Підказка**: Тримайте значення `Task.Delay` реалістичними, але не занадто великими. 50-200 мс симулює типовий зворотний виклик до бази даних. Це полегшує спостереження за тим, як час відповіді деградує під навантаженням.

### Завдання 2 — Навантажувальне тестування

Напишіть сценарії навантажувального тестування, які:

1. **Smoke-тест**: 1 віртуальний користувач, 1 хвилина — перевірити, що API коректно відповідає при мінімальному навантаженні
2. **Тест середнього навантаження**: 50 віртуальних користувачів, 5 хвилин — симуляція нормального трафіку

Для кожного сценарію зберіть та повідомте:

- Середній час відповіді (p50)
- 95-й перцентиль часу відповіді (p95)
- 99-й перцентиль часу відповіді (p99)
- Запити на секунду (RPS)
- Частка помилок (%)

**Приклад — smoke-тест NBomber**

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

        // Перевірка результатів
        var stats = result.ScenarioStats[0];
        Assert.True(stats.Fail.Request.Count == 0,
            $"Expected zero failures but got {stats.Fail.Request.Count}");
    }
}
```

**Приклад — тест середнього навантаження k6**

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 50 },  // наростання
    { duration: '4m',  target: 50 },  // утримання
    { duration: '30s', target: 0 },   // зниження
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],  // 95 % запитів мають завершитись < 500 мс
    http_req_failed:   ['rate<0.01'],  // частка помилок < 1 %
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

**Очікувана поведінка**

| Сценарій | Очікуваний p95 | Очікувана частка помилок | Очікуваний RPS |
|----------|---------------|--------------------------|----------------|
| Smoke (1 VU) | < 200 мс | 0 % | ~1 |
| Середнє навантаження (50 VU) | < 500 мс | < 1 % | ~40-50 |

> Це приблизні цільові значення. Фактичні значення залежать від вашого обладнання та налаштувань `Task.Delay`. Запишіть ваші реальні вимірювання та поясніть відхилення.

**Мінімальна кількість тестів для Завдання 2**: 2 тестових методи/скрипти (по одному на сценарій).

> **Підказка**: Завжди запускайте API у режимі Release (`dotnet run -c Release`) для узгоджених результатів. Режим Debug включає додаткове навантаження, яке спотворює вимірювання.

### Завдання 3 — Стрес-тестування

Напишіть сценарії стрес-тестування:

1. **Тест з поступовим наростанням**: Поступово збільшуйте кількість користувачів з 10 до 500 протягом 10 хвилин. Визначте точку відмови, де частка помилок перевищує 5%.

Задокументуйте:

- При якому навантаженні API починає відмовляти?
- Який максимальний RPS до того, як частка помилок перевищить 1%?

**Приклад — стрес-тест з поступовим наростанням NBomber**

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

**Очікувана поведінка**

| Фаза (цільовий RPS) | Очікуваний p95 | Очікувана частка помилок | Примітки |
|----------------------|---------------|--------------------------|----------|
| 10 RPS | < 200 мс | 0 % | Базовий рівень / розігрів |
| 50 RPS | < 400 мс | 0 % | Нормальна потужність |
| 100 RPS | < 800 мс | < 1 % | Наближення до меж |
| 250 RPS | < 2000 мс | 1-5 % | Очікується деградація |
| 500 RPS | > 2000 мс | > 5 % | Ймовірна точка відмови |

**Мінімальна кількість тестів для Завдання 3**: 1 тестовий метод/скрипт (стрес-тест з поступовим наростанням).

### Завдання 4 — Звіт про результати

Створіть `REPORT.md` з:

1. Зведеною таблицею всіх результатів тестування
2. Виявленими вузькими місцями та рекомендованими оптимізаціями

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Налаштування API |
| Завдання 2 — Сценарії навантажувального тестування |
| Завдання 3 — Сценарії стрес-тестування |
| Завдання 4 — Звіт про результати |

## Здача роботи

- Проєкт API та тестові скрипти/проєкти
- `REPORT.md` з результатами, таблицями та аналізом
- Згенеровані HTML-звіти від k6/NBomber

## Посилання

- [NBomber Documentation](https://nbomber.com/docs/getting-started/overview/)
- [NBomber.Http Plugin](https://nbomber.com/docs/plugins/http/)
- [k6 Documentation](https://k6.io/docs/)
- [k6 Test Types (Smoke, Load, Stress, Spike, Soak)](https://grafana.com/docs/k6/latest/testing-guides/test-types/)
- [ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline)
- [Understanding Latency Percentiles (p50, p95, p99)](https://www.brendangregg.com/blog/2016-10-01/latency-heat-maps.html)
