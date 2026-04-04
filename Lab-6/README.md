# Лабораторна 6 — Тестування продуктивності: навантаження та стрес

## Мета

Навчитися виконувати навантажувальне та стрес-тестування веб-API, що працює з реальною базою даних через Testcontainers. Виявляти вузькі місця продуктивності, встановлювати базові показники та визначати межі працездатності системи.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений .NET 10 SDK або новіший
- Встановлений та запущений Docker (необхідний для Testcontainers)
- Основи C# та ASP.NET Core
- Розуміння HTTP-методів (GET, POST) та кодів стану
- Виконана Лабораторна 4 (тестування баз даних з Testcontainers)
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
| **Testcontainers** | Бібліотека для запуску реальних баз даних у Docker-контейнерах під час тестування. Забезпечує реалістичне середовище без ручного налаштування інфраструктури. |

## Інструменти

- Мова: C#
- API: ASP.NET Core Web API (система, що тестується)
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- База даних: SQL Server у Docker (через [Testcontainers](https://dotnet.testcontainers.org/))
- Навантажувальне тестування: [k6](https://k6.io/) або [NBomber](https://nbomber.com/)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`, для тестів на основі NBomber)

## Налаштування

### Варіант A — NBomber (нативний C#)

```bash
dotnet new sln -n Lab6
dotnet new webapi -n Lab6.Api
dotnet new classlib -n Lab6.Tests
dotnet sln add Lab6.Api Lab6.Tests
dotnet add Lab6.Api package Microsoft.EntityFrameworkCore
dotnet add Lab6.Api package Microsoft.EntityFrameworkCore.SqlServer
dotnet add Lab6.Tests reference Lab6.Api
dotnet add Lab6.Tests package xunit.v3
dotnet add Lab6.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab6.Tests package NBomber
dotnet add Lab6.Tests package NBomber.Http
dotnet add Lab6.Tests package Testcontainers.MsSql
dotnet add Lab6.Tests package Microsoft.AspNetCore.Mvc.Testing
```

### Варіант B — k6 (на основі JavaScript)

```bash
# Встановлення k6: https://k6.io/docs/getting-started/installation/
brew install k6    # macOS
choco install k6   # Windows
```

> **Примітка:** При використанні k6 API все одно має працювати з реальною базою даних через Testcontainers. Запустіть API окремо з підключенням до контейнера SQL Server.

## Завдання

### Завдання 1 — Побудова системи, що тестується (з Testcontainers)

Створіть ASP.NET Core API з реальною базою даних, використовуючи Entity Framework Core та Testcontainers для запуску SQL Server у Docker.

#### Модель даних

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

#### DbContext

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
            entity.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}
```

#### Ендпоінти API

- `GET /api/products` — повертає список продуктів з бази даних
- `GET /api/products/{id}` — повертає окремий продукт
- `POST /api/products` — створює продукт у базі даних
- `GET /api/products/search?q=term` — пошук продуктів за назвою

#### Приклад контролера

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Products.ToListAsync();
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _context.Products.FindAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var results = await _context.Products
            .Where(p => p.Name.Contains(q))
            .ToListAsync();
        return Ok(results);
    }
}
```

#### Налаштування Testcontainers для тестового середовища

Використовуйте `WebApplicationFactory` з Testcontainers для створення тестового сервера з реальною базою даних:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string BaseUrl => Server.BaseAddress.ToString().TrimEnd('/');

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Видалити існуючу реєстрацію DbContext
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Додати DbContext з підключенням до Testcontainers
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(_dbContainer.GetConnectionString()));
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Застосувати міграції після запуску контейнера
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();

        // Заповнити тестовими даними
        await SeedDataAsync(context);
    }

    private static async Task SeedDataAsync(AppDbContext context)
    {
        var products = Enumerable.Range(1, 100).Select(i => new Product
        {
            Name = $"Product {i}",
            Price = 9.99m + i,
            Category = i % 3 == 0 ? "Electronics" : i % 3 == 1 ? "Books" : "Clothing",
            StockQuantity = i * 10
        });

        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
```

> **Підказка**: Заповнення бази 100 продуктами дає реалістичне навантаження для запитів. У реальних проєктах обсяг тестових даних має відповідати очікуваному виробничому навантаженню.

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

**Приклад — smoke-тест NBomber з Testcontainers**

```csharp
public class LoadTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LoadTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SmokeTest_SingleUser_ShouldRespondWithoutErrors()
    {
        var httpClient = _factory.CreateClient();

        var scenario = Scenario.Create("smoke_get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{_factory.BaseUrl}/api/products");
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

// URL API, що працює з Testcontainers (запустіть окремо)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

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
  const res = http.get(`${BASE_URL}/api/products`);
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

> Це приблизні цільові значення. Фактичні значення залежать від вашого обладнання та продуктивності бази даних у контейнері. Запишіть ваші реальні вимірювання та поясніть відхилення.

**Мінімальна кількість тестів для Завдання 2**: 2 тестових методи/скрипти (по одному на сценарій).

> **Підказка**: Завжди запускайте API у режимі Release (`dotnet run -c Release`) для узгоджених результатів. Режим Debug включає додаткове навантаження, яке спотворює вимірювання.

### Завдання 3 — Стрес-тестування

Напишіть сценарії стрес-тестування:

1. **Тест з поступовим наростанням**: Поступово збільшуйте кількість користувачів з 10 до 500 протягом 10 хвилин. Визначте точку відмови, де частка помилок перевищує 5%.

Задокументуйте:

- При якому навантаженні API починає відмовляти?
- Який максимальний RPS до того, як частка помилок перевищить 1%?
- Як поводиться база даних під навантаженням (час виконання запитів, кількість з'єднань)?

**Приклад — стрес-тест з поступовим наростанням NBomber**

```csharp
[Fact]
public void StressTest_RampUp_FindsBreakingPoint()
{
    var httpClient = _factory.CreateClient();

    var scenario = Scenario.Create("stress_ramp_up", async context =>
    {
        var request = Http.CreateRequest("GET", $"{_factory.BaseUrl}/api/products");
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

    var result = NBomberRunner
        .RegisterScenarios(scenario)
        .Run();

    var stats = result.ScenarioStats[0];
    // Зафіксувати результати для звіту
}
```

**Очікувана поведінка**

| Фаза (цільовий RPS) | Очікуваний p95 | Очікувана частка помилок | Примітки |
|----------------------|---------------|--------------------------|----------|
| 10 RPS | < 200 мс | 0 % | Базовий рівень / розігрів |
| 50 RPS | < 400 мс | 0 % | Нормальна потужність |
| 100 RPS | < 800 мс | < 1 % | Наближення до меж |
| 250 RPS | < 2000 мс | 1-5 % | Очікується деградація (пул з'єднань до БД) |
| 500 RPS | > 2000 мс | > 5 % | Ймовірна точка відмови |

> **Примітка:** З реальною базою даних у контейнері точка відмови може настати раніше, ніж з in-memory даними. Це більш реалістичний результат — саме так поводиться виробнича система.

**Мінімальна кількість тестів для Завдання 3**: 1 тестовий метод/скрипт (стрес-тест з поступовим наростанням).

### Завдання 4 — Звіт про результати

Створіть `REPORT.md` з:

1. Зведеною таблицею всіх результатів тестування
2. Виявленими вузькими місцями та рекомендованими оптимізаціями
3. Аналізом впливу бази даних на продуктивність (порівняння часу відповіді ендпоінтів з різною складністю запитів)

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Налаштування API з Testcontainers |
| Завдання 2 — Сценарії навантажувального тестування |
| Завдання 3 — Сценарії стрес-тестування |
| Завдання 4 — Звіт про результати |

## Здача роботи

- Проєкт API з Entity Framework Core та Testcontainers
- Тестові скрипти/проєкти
- `REPORT.md` з результатами, таблицями та аналізом
- Згенеровані HTML-звіти від k6/NBomber

## Посилання

- [Testcontainers для .NET](https://dotnet.testcontainers.org/) — тестова інфраструктура на основі контейнерів
- [Модуль Testcontainers для SQL Server](https://dotnet.testcontainers.org/modules/mssql/) — конфігурація контейнера SQL Server
- [WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) — інтеграційне тестування ASP.NET Core
- [NBomber Documentation](https://nbomber.com/docs/getting-started/overview/)
- [NBomber.Http Plugin](https://nbomber.com/docs/plugins/http/)
- [k6 Documentation](https://k6.io/docs/)
- [k6 Test Types (Smoke, Load, Stress, Spike, Soak)](https://grafana.com/docs/k6/latest/testing-guides/test-types/)
- [ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline)
- [Understanding Latency Percentiles (p50, p95, p99)](https://www.brendangregg.com/blog/2016-10-01/latency-heat-maps.html)
