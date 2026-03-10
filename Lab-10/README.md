# Лабораторна 10 — Тестування сервісів: мікросервіси та контрактне тестування

## Мета

Вивчити контрактне тестування для комунікації мікросервісів. Переконатися, що постачальники та споживачі сервісів узгоджують контракти API без необхідності наскрізної інтеграції.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що у вас є:

- Встановлений .NET 10 SDK (або новіший)
- Розуміння REST API та HTTP-методів
- Знайомство зі структурою тестів xUnit (з попередніх лабораторних)
- Базові знання серіалізації/десеріалізації JSON у C#
- Виконані Лабораторні 1-9 (особливо Лабораторні 7-8 з інтеграційного та API-тестування)

Встановіть інструменти CLI Pact (необов'язково, для налагодження):

```bash
# На macOS
brew install pact-foundation/pact-ruby-standalone/pact

# На Windows (через Chocolatey)
choco install pact
```

## Ключові концепції

### Що таке контрактне тестування?

Контрактне тестування перевіряє, що два сервіси (**споживач** та **постачальник**) можуть правильно комунікувати, тестуючи проти спільного **контракту** (також називається "пакт"). На відміну від наскрізних інтеграційних тестів, контрактні тести запускаються незалежно для кожного сервісу.

**Чому б просто не використовувати інтеграційні тести?**

| Аспект | Інтеграційні тести | Контрактні тести |
|--------|---------------------|-------------------|
| Швидкість | Повільні (обидва сервіси мають працювати) | Швидкі (кожна сторона тестується незалежно) |
| Надійність | Нестабільні (мережа, середовище) | Детерміновані (без реальних мережевих викликів) |
| Зворотний зв'язок | Пізній (потрібні розгорнуті сервіси) | Ранній (запускається на етапі модульних тестів) |
| Охоплення | Тестує все одразу | Тестує лише межу API |

### Робочий процес Pact

1. **Споживач пише тести**, описуючи, що він очікує від постачальника (запити та очікувані відповіді).
2. **PactNet генерує файл Pact** (JSON), що фіксує ці очікування.
3. **Постачальник верифікує файл Pact**, відтворюючи взаємодії проти своєї реальної реалізації.
4. Якщо верифікація пройшла, обидві сторони сумісні. Якщо невдача — контракт порушено.

```
Тести споживача           Файл Pact (JSON)          Верифікація постачальника
 ┌───────────┐         ┌───────────────┐         ┌───────────────────┐
 │ Визначити │ ──────> │ Взаємодії     │ ──────> │ Відтворити проти  │
 │ очікувані │ генер.  │ у форматі     │ вериф.  │ реального API     │
 │ запити та │         │ JSON          │         │ постачальника     │
 │ відповіді │         └───────────────┘         └───────────────────┘
 └───────────┘
```

### Стани постачальника

Стани постачальника дозволяють споживачу описати, які дані мають існувати на стороні постачальника перед виконанням взаємодії. Наприклад:

- `"an order with id 1 exists"` -- постачальник створює замовлення з id=1
- `"no order with id 999 exists"` -- постачальник гарантує відсутність такого замовлення

Тест постачальника налаштовує обробник станів, який підготовлює необхідні дані для кожного стану.

## Інструменти

- Мова: C#
- Контрактне тестування: [PactNet](https://github.com/pact-foundation/pact-net)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Налаштування

```bash
dotnet new sln -n Lab10
dotnet new webapi -n Lab10.OrderService
dotnet new classlib -n Lab10.OrderClient
dotnet new classlib -n Lab10.Consumer.Tests
dotnet new classlib -n Lab10.Provider.Tests
dotnet sln add Lab10.OrderService Lab10.OrderClient Lab10.Consumer.Tests Lab10.Provider.Tests
dotnet add Lab10.Consumer.Tests reference Lab10.OrderClient
dotnet add Lab10.Consumer.Tests package xunit.v3
dotnet add Lab10.Consumer.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab10.Consumer.Tests package PactNet
dotnet add Lab10.Provider.Tests reference Lab10.OrderService
dotnet add Lab10.Provider.Tests package xunit.v3
dotnet add Lab10.Provider.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab10.Provider.Tests package PactNet
dotnet add Lab10.Provider.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add Lab10.Consumer.Tests package Shouldly
dotnet add Lab10.Provider.Tests package Shouldly
```

## Сценарій

У вас є два мікросервіси:

- **Order Service** (постачальник) — керує замовленнями через REST API
- **Notification Service** (споживач) — використовує API Order Service для отримання деталей замовлення для надсилання електронних листів

### Приклад моделі замовлення

```csharp
public class Order
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Pending";
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

### Приклад ендпоінтів API (постачальник)

| Метод | Ендпоінт | Опис |
|-------|----------|------|
| `GET` | `/api/orders/{id}` | Отримати окреме замовлення за ID |
| `GET` | `/api/orders?customerId={id}` | Отримати всі замовлення клієнта |
| `POST` | `/api/orders` | Створити нове замовлення |

### Приклад клієнта споживача

```csharp
public class OrderClient
{
    private readonly HttpClient _httpClient;

    public OrderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Order?> GetOrderAsync(int id)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Order>();
    }

    public async Task<List<Order>> GetOrdersByCustomerAsync(int customerId)
    {
        var response = await _httpClient.GetAsync($"/api/orders?customerId={customerId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Order>>() ?? new();
    }
}
```

## Завдання

### Завдання 1 — Контрактні тести на стороні споживача

У `Lab10.Consumer.Tests` напишіть контрактні тести Pact для споживача:

1. Визначте очікувану взаємодію для `GET /api/orders/{id}`:
   - Запит: `GET /api/orders/1` із `Accept: application/json`
   - Очікувана відповідь: 200 з JSON-тілом, що містить `id`, `customerEmail`, `items`, `totalAmount`, `status`

2. Визначте очікувану взаємодію для неіснуючого замовлення:
   - Запит: `GET /api/orders/999`
   - Очікувана відповідь: 404

3. Визначте очікувану взаємодію для `POST /api/orders`:
   - Запит: POST з JSON-тілом замовлення
   - Очікувана відповідь: 201 зі створеним замовленням

4. Згенеруйте файл Pact та перевірте, що він створений у директорії `pacts/`

#### Приклад структури тесту споживача

```csharp
public class OrderApiConsumerTests
{
    private readonly IPactBuilderV4 _pactBuilder;

    public OrderApiConsumerTests()
    {
        var pact = Pact.V4("NotificationService", "OrderService", new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "pacts")
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task GetOrder_WhenOrderExists_ReturnsOrderAsync()
    {
        // Arrange
        _pactBuilder
            .UponReceiving("a request for order with id 1")
            .Given("an order with id 1 exists")
            .WithRequest(HttpMethod.Get, "/api/orders/1")
            .WithHeader("Accept", "application/json")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new
            {
                id = 1,
                customerEmail = Match.Type("customer@example.com"),
                items = Match.MinType(new { productName = "Widget", quantity = 1, price = 9.99 }, 1),
                totalAmount = Match.Decimal(9.99),
                status = Match.Type("Pending")
            });

        // Act & Assert
        await _pactBuilder.VerifyAsync(async ctx =>
        {
            var client = new OrderClient(new HttpClient { BaseAddress = ctx.MockServerUri });
            var order = await client.GetOrderAsync(1);

            order.ShouldNotBeNull();
            order.Id.ShouldBe(1);
            order.CustomerEmail.ShouldNotBeNullOrEmpty();
        });
    }
}
```

> **Підказка:** Матчери PactNet, такі як `Match.Type(...)`, `Match.Decimal(...)` та `Match.MinType(...)`, дозволяють визначити гнучкі контракти. `Match.Type` перевіряє тип, а не точне значення, що робить контракти менш крихкими.

#### Очікувана структура файлу Pact

Після запуску тестів споживача має бути створений файл на кшталт `pacts/NotificationService-OrderService.json`. Він містить усі визначені взаємодії у форматі JSON.

### Завдання 2 — Верифікація контракту на стороні постачальника

У `Lab10.Provider.Tests` верифікуйте постачальника проти файлу Pact:

1. Налаштуйте `WebApplicationFactory` для Order Service
2. Налаштуйте стани постачальника:
   - `"an order with id 1 exists"` — наповнення тестовими даними
   - `"no order with id 999 exists"` — забезпечення чистого стану
   - `"customer 42 has orders"` — наповнення замовленнями клієнта
3. Запустіть верифікацію Pact та переконайтеся, що всі взаємодії проходять
4. Перевірте, що додавання нового обов'язкового поля до відповіді порушує контракт

#### Приклад верифікації постачальника

```csharp
public class OrderApiProviderTests : IDisposable
{
    private readonly PactVerifier _verifier;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IHost _host;

    public OrderApiProviderTests()
    {
        _factory = new WebApplicationFactory<Program>();
        _host = _factory.Server.Host;
        _verifier = new PactVerifier("OrderService", new PactVerifierConfig());
    }

    [Fact]
    public void VerifyPacts()
    {
        var pactPath = Path.Combine("..", "..", "..", "..", "pacts",
            "NotificationService-OrderService.json");

        _verifier
            .WithHttpEndpoint(_factory.Server.BaseAddress)
            .WithProviderStateUrl(new Uri(_factory.Server.BaseAddress, "/provider-states"))
            .WithFileSource(new FileInfo(pactPath))
            .Verify();
    }

    public void Dispose()
    {
        _factory.Dispose();
        _verifier.Dispose();
    }
}
```

> **Підказка:** Вам потрібно реалізувати ендпоінт `/provider-states` (або проміжне програмне забезпечення) у вашому тестовому сервері, який приймає POST-запити від верифікатора та налаштовує необхідні дані. Зверніть увагу на `IStartupFilter` або реєстрацію проміжного ПЗ всередині `WebApplicationFactory<T>.WithWebHostBuilder(...)`.

#### Шаблон обробника станів постачальника

```csharp
// У вашому тестовому проєкті додайте проміжне ПЗ для обробки станів постачальника
app.MapPost("/provider-states", async (HttpContext context) =>
{
    var providerState = await context.Request.ReadFromJsonAsync<ProviderState>();

    switch (providerState?.State)
    {
        case "an order with id 1 exists":
            // Наповнити базу даних або in-memory сховище замовленням
            break;
        case "no order with id 999 exists":
            // Переконатися, що замовлення з id 999 не існує (чистий стан)
            break;
        case "customer 42 has orders":
            // Наповнити кількома замовленнями для клієнта 42
            break;
    }
});
```

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Контрактні тести споживача |
| Завдання 2 — Верифікація постачальника |

## Здача роботи

- Рішення з усіма чотирма проєктами
- Згенеровані файли Pact у директорії `pacts/`

## Посилання

- [PactNet GitHub Repository](https://github.com/pact-foundation/pact-net) -- вихідний код бібліотеки, приклади та README
- [Pact Documentation (Official)](https://docs.pact.io/) -- повні посібники для всіх реалізацій Pact
- [Pact Introduction: 5-Minute Guide](https://docs.pact.io/5-minute-getting-started-guide) -- посібник для швидкого старту
- [Consumer-Driven Contract Testing](https://martinfowler.com/articles/consumerDrivenContracts.html) -- стаття Мартіна Фаулера про концепцію
- [Contract Testing vs Integration Testing](https://pactflow.io/blog/contract-testing-vs-integration-testing/) -- порівняння підходів
- [PactNet v5 Migration Guide](https://github.com/pact-foundation/pact-net/blob/master/docs/upgrading-to-5.md) -- при використанні PactNet v5
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) -- довідник тестового фреймворку
- [Shouldly Assertion Library](https://docs.shouldly.org/) -- бібліотека тверджень, що використовується в цій лабораторній
- [ASP.NET Core Integration Testing with WebApplicationFactory](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) -- актуально для налаштування на стороні постачальника
