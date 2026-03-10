# Лабораторна 2 — Модульне тестування: Імітації та тестові замінники

## Мета

Навчитися ізолювати модулі, що тестуються, за допомогою фреймворків для створення імітацій. Зрозуміти різницю між заглушками (stubs), моками (mocks), підробками (fakes) та шпигунами (spies). Застосувати впровадження залежностей для забезпечення тестовності коду.

**Тривалість:** 60 хвилин

## Передумови

- Виконана Лабораторна 1
- Розуміння інтерфейсів та впровадження залежностей у C#

## Інструменти

- Мова: C#
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Імітації: [NSubstitute](https://nsubstitute.github.io/)

## Ключові поняття

- **Тестовий замінник (Test double)** — об'єкт, що замінює реальну залежність
  - **Заглушка (Stub)** — повертає заздалегідь визначені дані (без перевірки поведінки)
  - **Мок (Mock)** — перевіряє, що певні методи були викликані
  - **Підробка (Fake)** — робоча реалізація (наприклад, база даних у пам'яті)
  - **Шпигун (Spy)** — записує виклики для подальшої перевірки
- **Впровадження залежностей (DI)** — передача залежностей через конструктор замість їх створення всередині класу
- Синтаксис **NSubstitute**:
  - `Substitute.For<IService>()` — створити мок
  - `service.Method(arg).Returns(value)` — налаштувати значення, що повертається
  - `service.Received().Method(arg)` — перевірити виклик

## Налаштування

```bash
dotnet new sln -n Lab2
dotnet new classlib -n Lab2.Core
dotnet new classlib -n Lab2.Tests
dotnet sln add Lab2.Core Lab2.Tests
dotnet add Lab2.Tests reference Lab2.Core
dotnet add Lab2.Tests package xunit.v3
dotnet add Lab2.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab2.Tests package NSubstitute
dotnet add Lab2.Tests package Shouldly
```

## Завдання

### Завдання 1 — Сервіс замовлень з імітованим репозиторієм

Створіть наступні інтерфейси та класи в `Lab2.Core`:

```csharp
public record Order(int Id, int CustomerId, string CustomerEmail,
    List<OrderItem> Items, decimal TotalAmount, OrderStatus Status);

public record OrderItem(string ProductName, int Quantity, decimal Price);

public enum OrderStatus { Pending, Confirmed, Shipped, Cancelled }

public record PaymentResult(bool Success, string TransactionId, string ErrorMessage);

public interface IOrderRepository
{
    Order GetById(int id);
    void Save(Order order);
    IEnumerable<Order> GetByCustomerId(int customerId);
}

public interface IPaymentGateway
{
    PaymentResult ProcessPayment(decimal amount, string currency);
}

public interface INotificationService
{
    void SendOrderConfirmation(string email, int orderId);
    void SendOrderCancellation(string email, int orderId);
}

public class OrderService
{
    // Впровадження через конструктор усіх трьох залежностей
    // Реалізувати: PlaceOrder, CancelOrder, GetOrderHistory
}
```

Напишіть тести для `OrderService`, що:

1. Імітують `IOrderRepository` для повернення заздалегідь визначених замовлень
2. Імітують `IPaymentGateway` для симуляції успішних/неуспішних платежів
3. Перевіряють, що `INotificationService.SendOrderConfirmation` викликається з правильними параметрами
4. Тестують, що `CancelOrder` кидає виняток, коли замовлення вже відправлено
5. Використовують `[Theory]` з `[InlineData]` для кількох сценаріїв оплати

**Приклад тесту:**

```csharp
[Fact]
public void PlaceOrder_WhenPaymentSucceeds_SavesOrderAndSendsConfirmation()
{
    // Arrange
    var repo = Substitute.For<IOrderRepository>();
    var payment = Substitute.For<IPaymentGateway>();
    var notifications = Substitute.For<INotificationService>();

    payment.ProcessPayment(Arg.Any<decimal>(), "USD")
        .Returns(new PaymentResult(true, "TX-123", null));

    var sut = new OrderService(repo, payment, notifications);

    // Act
    sut.PlaceOrder(customerId: 1, email: "test@example.com", items, "USD");

    // Assert
    repo.Received(1).Save(Arg.Is<Order>(o => o.Status == OrderStatus.Confirmed));
    notifications.Received(1).SendOrderConfirmation("test@example.com", Arg.Any<int>());
}
```

> **Примітка:** Додайте щонайменше 2 тести, що перевіряють порядок викликів, перехоплення аргументів або шаблони `DidNotReceive()`.

**Мінімальна кількість тестів:** 10 тестів

### Завдання 2 — Сервіс прогнозу погоди

Створіть:

```csharp
public record WeatherData(string City, double Temperature, string Description, DateTime Date);

public interface IWeatherApiClient
{
    Task<WeatherData> GetCurrentWeatherAsync(string city);
    Task<IEnumerable<WeatherData>> GetForecastAsync(string city, int days);
}

public interface ICacheService
{
    T Get<T>(string key);
    void Set<T>(string key, T value, TimeSpan expiration);
    bool Exists(string key);
}

public class WeatherForecastService
{
    // Використовує IWeatherApiClient та ICacheService
    // Реалізувати: GetForecastAsync (спочатку перевіряє кеш, потім API)
    // Формат ключа кешу: "weather:{city}:{days}"
    // Термін дії кешу: 30 хвилин
}
```

Напишіть тести, що:

1. Перевіряють, що кеш перевіряється перед викликом API
2. Перевіряють, що результат API зберігається в кеші
3. Коли кеш існує, API ніколи не викликається
4. Обробляють винятки API коректно (повертають закешовані дані або кидають власний виняток)
5. Правильно тестують асинхронні методи з тестовими методами `async Task`

**Мінімальна кількість тестів:** 5 тестів

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести сервісу замовлень |
| Завдання 2 — Тести сервісу погоди |
| Правильне використання mock/stub/verify у NSubstitute |
| Чисте впровадження залежностей |

## Здача роботи

- Рішення з проєктами `Lab2.Core` та `Lab2.Tests`
- Усі залежності впроваджені через конструктор (жодного `new` всередині класів сервісів)
- Мінімум 15 тестів загалом

## Посилання

- [Документація NSubstitute](https://nsubstitute.github.io/help/getting-started/)
- [NSubstitute — Перевірка отриманих викликів](https://nsubstitute.github.io/help/received-calls/)
- [Документація Shouldly](https://docs.shouldly.org/)
