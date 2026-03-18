# Lab6.Api.Tests

Модульні (unit) тести для Lab6 Orders API.

## Технології

- **xUnit v3** — тестовий фреймворк
- **Shouldly** — бібліотека assertions
- **NSubstitute** — мокінг залежностей
- **EF Core InMemory** — in-memory база даних для тестів

## Структура

```
Lab6.Api.Tests/
├── Unit/
│   └── Services/
│       └── OrderServiceTests.cs
├── Integration/
│   ├── Controllers/
│   └── Database/
└── Fixtures/
    └── CustomWebApplicationFactory.cs
```

## Конвенція іменування тестів

```
MethodName_Scenario_ExpectedBehavior
```

Приклади:
- `GetAllAsync_ReturnsAllOrders`
- `CreateAsync_ValidData_ReturnsCreatedOrder`
- `DeleteAsync_NonExistentId_ReturnsFalse`

## Запуск

```bash
# Всі тести
dotnet test

# Конкретний тест
dotnet test --filter "FullyQualifiedName~OrderServiceTests"

# З покриттям коду
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

## Структура тесту (AAA)

```csharp
[Fact]
public void CalculateTotal_EmptyCart_ReturnsZero()
{
    // Arrange
    var service = new PriceCalculator();

    // Act
    var result = service.CalculateTotal(new List<CartItem>());

    // Assert
    result.ShouldBe(0m);
}
```
