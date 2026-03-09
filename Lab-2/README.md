# Lab 2 — Unit Testing: Mocking and Test Doubles

## Objective

Learn to isolate units under test using mocking frameworks. Understand the difference between stubs, mocks, fakes, and spies. Apply dependency injection to make code testable.

**Duration:** 60 minutes

## Prerequisites

- Completed Lab 1
- Understanding of interfaces and dependency injection in C#

## Tools

- Language: C#
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Mocking: [NSubstitute](https://nsubstitute.github.io/)

## Key Concepts

- **Test double** — an object that stands in for a real dependency
  - **Stub** — returns predefined data (no behavior verification)
  - **Mock** — verifies that certain methods were called
  - **Fake** — a working implementation (e.g., in-memory database)
  - **Spy** — records calls for later inspection
- **Dependency Injection (DI)** — passing dependencies via constructor instead of creating them inside the class
- **NSubstitute** syntax:
  - `Substitute.For<IService>()` — create a mock
  - `service.Method(arg).Returns(value)` — set up return value
  - `service.Received().Method(arg)` — verify call

## Setup

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

## Tasks

### Task 1 — Order Service with Mocked Repository

Create the following interfaces and classes in `Lab2.Core`:

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
    // Constructor injection of all three dependencies
    // Implement: PlaceOrder, CancelOrder, GetOrderHistory
}
```

Write tests for `OrderService` that:

1. Mock `IOrderRepository` to return predefined orders
2. Mock `IPaymentGateway` to simulate successful/failed payments
3. Verify `INotificationService.SendOrderConfirmation` is called with correct parameters
4. Test that `CancelOrder` throws when order is already shipped
5. Use `[Theory]` with `[InlineData]` for multiple payment scenarios

**Example test:**

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

> **Note:** Include at least 2 tests that verify call order, argument capture, or `DidNotReceive()` patterns.

**Minimum test count:** 10 tests

### Task 2 — Weather Forecast Service

Create:

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
    // Uses IWeatherApiClient and ICacheService
    // Implement: GetForecastAsync (checks cache first, then API)
    // Cache key format: "weather:{city}:{days}"
    // Cache expiration: 30 minutes
}
```

Write tests that:

1. Verify cache is checked before calling API
2. Verify API result is stored in cache
3. When cache exists, API is never called
4. Handle API exceptions gracefully (return cached data or throw custom exception)
5. Test async methods properly with `async Task` test methods

**Minimum test count:** 5 tests

## Grading

| Criteria |
|----------|
| Task 1 — Order service tests |
| Task 2 — Weather service tests |
| Correct use of NSubstitute mock/stub/verify |
| Clean dependency injection |

## Submission

- Solution with `Lab2.Core` and `Lab2.Tests` projects
- All dependencies injected via constructor (no `new` inside service classes)
- Minimum 15 total tests

## References

- [NSubstitute Documentation](https://nsubstitute.github.io/help/getting-started/)
- [NSubstitute — Checking received calls](https://nsubstitute.github.io/help/received-calls/)
- [Shouldly Documentation](https://docs.shouldly.org/)
