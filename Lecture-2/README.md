# Лекція 2: Модульне тестування та мокування

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Пояснити принципи та цінність модульного тестування
- Структурувати тести за патерном Arrange-Act-Assert (AAA)
- Писати параметризовані тести з `[Fact]` та `[Theory]`
- Розуміти тестові дублери: стаби, моки, фейки та шпигуни
- Використовувати NSubstitute для ізоляції залежностей у модульних тестах
- Застосовувати найкращі практики іменування, організації та підтримки тестів

---

## 1. Підсумок: Що таке модульне тестування?

### 1.1 Визначення

**Модульний тест** перевіряє найменшу тестовану частину ПЗ — зазвичай окремий метод або функцію — **ізольовано** від залежностей.

```
┌──────────────────────────────────────────────┐
│  Модульний тест                              │
│                                              │
│   Вхід ──► [Метод, що тестується] ──► Вихід  │
│                     │                        │
│              (залежності замінені            │
│               моками/стабами)               │
│                                              │
│   Перевірка: вихід відповідає очікуванню     │
└──────────────────────────────────────────────┘
```

### 1.2 Принципи F.I.R.S.T.

Хороші модульні тести дотримуються принципів **F.I.R.S.T.**:

| Принцип | Опис |
|---|---|
| **Fast (Швидкі)** | Виконуються за мілісекунди; весь набір — за секунди |
| **Isolated (Ізольовані)** | Без залежностей від інших тестів, баз даних, мережі, файлової системи |
| **Repeatable (Повторювані)** | Однаковий результат кожного разу, в будь-якому середовищі |
| **Self-validating (Самоперевірні)** | Проходять або не проходять автоматично — без ручної перевірки |
| **Timely (Своєчасні)** | Написані близько за часом до продакшн-коду |

### 1.3 Що робить модульний тест хорошим?

```
Хороший модульний тест             Поганий модульний тест
───────────────────                ─────────────────────
✓ Тестує одну поведінку            ✗ Тестує кілька речей
✓ Описова назва                    ✗ Називається "Test1", "TestMethod"
✓ Незалежний від інших тестів       ✗ Залежить від порядку виконання тестів
✓ Швидкий (мілісекунди)            ✗ Повільний (викликає БД, мережу)
✓ Детерміністичний                 ✗ Нестабільний (залежить від часу, випадковості)
✓ Легкий для читання та розуміння  ✗ Складне налаштування, нечіткий намір
```

---

## 2. Анатомія модульного тесту

### 2.1 Патерн AAA

Кожен добре структурований тест дотримується **Arrange-Act-Assert**:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange — налаштування передумов та вхідних даних
    var service = new PriceCalculator();
    var basePrice = 100m;

    // Act — виконання поведінки, що тестується
    var result = service.CalculateWithTax(basePrice, taxRate: 0.2m);

    // Assert — перевірка очікуваного результату
    result.ShouldBe(120m);
}
```

**Рекомендації:**
- Кожна секція повинна бути чітко ідентифікована (коментарі необов'язкові, коли ви звикли до патерну)
- Надавайте перевагу **одному Act** — один виклик методу на тест
- Надавайте перевагу **сфокусованим перевіркам** — перевіряйте одну логічну концепцію (може бути кілька викликів `ShouldBe`, якщо вони перевіряють ту саму концепцію)

### 2.2 Конвенції іменування тестів

Назви тестів повинні описувати **поведінку**, а не реалізацію:

```
 MethodName_Scenario_ExpectedBehavior
```

| Добре | Погано |
|---|---|
| `CalculateTotal_EmptyCart_ReturnsZero` | `TestCalculateTotal` |
| `Login_InvalidPassword_ThrowsAuthException` | `LoginTest2` |
| `IsEligible_AgeBelow18_ReturnsFalse` | `CheckAge` |

Назва повинна читатися як специфікація: *"Коли я викликаю CalculateTotal на порожньому кошику, він повертає нуль."*

### 2.3 `[Fact]` vs. `[Theory]`

#### `[Fact]` — Один тестовий випадок

Використовується, коли тест має фіксовані вхідні дані:

```csharp
[Fact]
public void Add_TwoPositiveNumbers_ReturnsSum()
{
    var calc = new Calculator();
    calc.Add(2, 3).ShouldBe(5);
}
```

#### `[Theory]` — Параметризовані тестові випадки

Використовується, коли та сама логіка повинна бути протестована з різними вхідними даними:

```csharp
[Theory]
[InlineData(2, 3, 5)]
[InlineData(0, 0, 0)]
[InlineData(-1, 1, 0)]
[InlineData(int.MaxValue, 0, int.MaxValue)]
public void Add_VariousInputs_ReturnsExpectedSum(
    int a, int b, int expected)
{
    var calc = new Calculator();
    calc.Add(a, b).ShouldBe(expected);
}
```

#### Інші джерела даних для `[Theory]`

```csharp
// MemberData — використовуйте метод або властивість для складних тестових даних
[Theory]
[MemberData(nameof(GetDiscountTestCases))]
public void ApplyDiscount_VariousCases_ReturnsExpected(
    decimal price, int percent, decimal expected)
{
    var result = PriceCalculator.ApplyDiscount(price, percent);
    result.ShouldBe(expected);
}

public static IEnumerable<object[]> GetDiscountTestCases()
{
    yield return [100m, 10, 90m];
    yield return [200m, 25, 150m];
    yield return [50m,  0,  50m];
    yield return [100m, 100, 0m];
}
```

---

## 3. Shouldly: Виразні перевірки

### 3.1 Чому Shouldly?

Порівняйте повідомлення про помилки:

```
// Вбудована перевірка xUnit
Assert.Equal(5, result);
// Вивід: Assert.Equal() Failure. Expected: 5, Actual: 4

// Shouldly
result.ShouldBe(5);
// Вивід: result should be 5 but was 4
```

Shouldly створює **людиночитабельні повідомлення про помилки**, що включають назву змінної.

### 3.2 Поширені перевірки Shouldly

```csharp
// Рівність
result.ShouldBe(42);
name.ShouldBe("Alice");

// Логічні значення
isValid.ShouldBeTrue();
isEmpty.ShouldBeFalse();

// Null
order.ShouldNotBeNull();
deletedItem.ShouldBeNull();

// Числові порівняння
temperature.ShouldBeGreaterThan(0);
discount.ShouldBeLessThanOrEqualTo(100);
balance.ShouldBePositive();
diff.ShouldBeNegative();

// Колекції
items.ShouldNotBeEmpty();
items.Count.ShouldBe(3);
items.ShouldContain("Apple");
items.ShouldAllBe(i => i.Price > 0);
items.ShouldBeInOrder();

// Рядки
email.ShouldContain("@");
name.ShouldStartWith("Dr.");
code.ShouldMatch(@"^[A-Z]{3}-\d{4}$"); // regex

// Перевірка типу
shape.ShouldBeOfType<Circle>();
animal.ShouldBeAssignableTo<IMammal>();

// Винятки
Should.Throw<ArgumentNullException>(() => service.Process(null!));
var ex = Should.Throw<InvalidOperationException>(
    () => account.Withdraw(1000));
ex.Message.ShouldContain("insufficient funds");

// Асинхронні винятки
await Should.ThrowAsync<TimeoutException>(
    () => service.FetchDataAsync());

// Приблизна рівність (для чисел з плаваючою комою)
result.ShouldBe(3.14, tolerance: 0.01);

// Перевірки часу
elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
```

---

## 4. Тестові дублери

### 4.1 Навіщо потрібні тестові дублери?

У реальних додатках класи залежать від інших класів. Модульні тести повинні **ізолювати** клас, що тестується, від його залежностей:

```
Продакшн-код:                     Модульний тест:
┌──────────┐                       ┌──────────┐
│  Order   │──► IPaymentGateway    │  Order   │──► Fake/Mock
│ Service  │──► IEmailSender       │ Service  │──► Fake/Mock
│          │──► IOrderRepository   │          │──► Fake/Mock
└──────────┘                       └──────────┘
     │                                  │
Використовує реальні сервіси       Використовує тестові дублери
(БД, email, платежі)               (швидко, контрольовано, ізольовано)
```

### 4.2 Типи тестових дублерів

| Тип | Призначення | Приклад |
|---|---|---|
| **Dummy** | Заповнює параметр; ніколи не використовується | `new object()` для задоволення сигнатури |
| **Stub** | Повертає заздалегідь визначені значення | Завжди повертає `true` для `IsAvailable()` |
| **Spy** | Записує виклики для подальшої перевірки | Записує, що `SendEmail()` був викликаний двічі |
| **Mock** | Запрограмований з очікуваннями | Очікує, що `Save()` буде викликаний рівно один раз |
| **Fake** | Робоча реалізація (спрощена) | In-memory база даних замість реальної БД |

На практиці термін **"мок"** часто використовується вільно для позначення будь-якого тестового дублера. NSubstitute створює замінники, які можуть діяти як стаби, моки та шпигуни.

### 4.3 Коли використовувати тестові дублери

Використовуйте тестові дублери, коли залежність:
- **Повільна** (база даних, мережа, файлова система)
- **Недетерміністична** (поточний час, випадкові числа, зовнішні API)
- Має **побічні ефекти** (відправка листів, списання з кредитних карт)
- **Складна у налаштуванні** (складна ініціалізація)
- **Ще не існує** (команда створює її паралельно)

---

## 5. Мокування з NSubstitute

### 5.1 Чому NSubstitute?

NSubstitute використовує **чистий, текучий синтаксис** без "магічних рядків" чи складного налаштування:

```csharp
// NSubstitute — чистий та читабельний
var calculator = Substitute.For<ICalculator>();
calculator.Add(1, 2).Returns(3);

// Порівняння з іншими фреймворками (більш багатослівні):
// var mock = new Mock<ICalculator>();
// mock.Setup(c => c.Add(1, 2)).Returns(3);
// var calculator = mock.Object;
```

### 5.2 Налаштування

```bash
dotnet add package NSubstitute
```

```csharp
using NSubstitute;
```

### 5.3 Створення замінників

```csharp
// Створення замінника для інтерфейсу
var emailSender = Substitute.For<IEmailSender>();

// Створення замінника для абстрактного класу
var logger = Substitute.For<AbstractLogger>();

// Створення замінника для кількох інтерфейсів
var combo = Substitute.For<IEmailSender, IDisposable>();
```

### 5.4 Повний приклад: Сервіс обробки замовлень

#### Продакшн-код

```csharp
// Інтерфейси
namespace EStore;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task<int> SaveAsync(Order order);
}

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(string cardToken, decimal amount);
}

public interface IEmailSender
{
    Task SendOrderConfirmationAsync(string email, int orderId, decimal total);
}

public record Order(
    int Id,
    string CustomerEmail,
    string CardToken,
    decimal Total,
    OrderStatus Status);

public enum OrderStatus { Pending, Paid, Failed, Cancelled }

public record PaymentResult(bool Success, string TransactionId, string? Error = null);
```

```csharp
// Сервіс, що тестується
namespace EStore;

public class OrderProcessingService(
    IOrderRepository repository,
    IPaymentGateway paymentGateway,
    IEmailSender emailSender)
{
    public async Task<OrderResult> ProcessOrderAsync(int orderId)
    {
        var order = await repository.GetByIdAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found.");

        if (order.Status != OrderStatus.Pending)
            throw new InvalidOperationException(
                $"Order {orderId} is not in Pending status.");

        // Списання оплати
        var payment = await paymentGateway.ChargeAsync(
            order.CardToken, order.Total);

        if (!payment.Success)
        {
            var failedOrder = order with { Status = OrderStatus.Failed };
            await repository.SaveAsync(failedOrder);
            return new OrderResult(false, $"Payment failed: {payment.Error}");
        }

        // Оновлення статусу замовлення
        var paidOrder = order with { Status = OrderStatus.Paid };
        await repository.SaveAsync(paidOrder);

        // Відправка підтверджувального листа
        await emailSender.SendOrderConfirmationAsync(
            order.CustomerEmail, order.Id, order.Total);

        return new OrderResult(true, $"Order processed. Transaction: {payment.TransactionId}");
    }
}

public record OrderResult(bool Success, string Message);
```

#### Тестовий клас з NSubstitute

```csharp
using NSubstitute;
using Shouldly;

namespace EStore.Tests;

public class OrderProcessingServiceTests
{
    // Залежності (замінники)
    private readonly IOrderRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEmailSender _emailSender;

    // Система, що тестується
    private readonly OrderProcessingService _sut;

    public OrderProcessingServiceTests()
    {
        _repository = Substitute.For<IOrderRepository>();
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _emailSender = Substitute.For<IEmailSender>();

        _sut = new OrderProcessingService(
            _repository, _paymentGateway, _emailSender);
    }

    // ── Допоміжний метод ─────────────────────────────────

    private static Order CreatePendingOrder(int id = 1) =>
        new(id, "customer@example.com", "tok_visa", 99.99m, OrderStatus.Pending);

    // ── Стабування: Контроль значень, що повертаються ────

    [Fact]
    public async Task ProcessOrder_SuccessfulPayment_ReturnSuccessAsync()
    {
        // Arrange — стабуємо репозиторій для повернення очікуючого замовлення
        var order = CreatePendingOrder();
        _repository.GetByIdAsync(1).Returns(order);

        // Стабуємо платіжний шлюз на успіх
        _paymentGateway.ChargeAsync("tok_visa", 99.99m)
            .Returns(new PaymentResult(true, "txn_123"));

        // Act
        var result = await _sut.ProcessOrderAsync(1);

        // Assert
        result.Success.ShouldBeTrue();
        result.Message.ShouldContain("txn_123");
    }

    [Fact]
    public async Task ProcessOrder_FailedPayment_ReturnFailureAsync()
    {
        // Arrange
        _repository.GetByIdAsync(1).Returns(CreatePendingOrder());

        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
            .Returns(new PaymentResult(false, "", "Card declined"));

        // Act
        var result = await _sut.ProcessOrderAsync(1);

        // Assert
        result.Success.ShouldBeFalse();
        result.Message.ShouldContain("Card declined");
    }

    // ── Перевірка викликів (поведінка моків) ─────────────

    [Fact]
    public async Task ProcessOrder_SuccessfulPayment_SendsConfirmationEmailAsync()
    {
        // Arrange
        var order = CreatePendingOrder();
        _repository.GetByIdAsync(1).Returns(order);
        _paymentGateway.ChargeAsync("tok_visa", 99.99m)
            .Returns(new PaymentResult(true, "txn_456"));

        // Act
        await _sut.ProcessOrderAsync(1);

        // Assert — перевіряємо, що лист відправлено з правильними параметрами
        await _emailSender.Received(1)
            .SendOrderConfirmationAsync("customer@example.com", 1, 99.99m);
    }

    [Fact]
    public async Task ProcessOrder_FailedPayment_DoesNotSendEmailAsync()
    {
        // Arrange
        _repository.GetByIdAsync(1).Returns(CreatePendingOrder());
        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
            .Returns(new PaymentResult(false, "", "Insufficient funds"));

        // Act
        await _sut.ProcessOrderAsync(1);

        // Assert — перевіряємо, що лист НЕ був відправлений
        await _emailSender.DidNotReceive()
            .SendOrderConfirmationAsync(
                Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>());
    }

    [Fact]
    public async Task ProcessOrder_FailedPayment_SavesOrderAsFailedAsync()
    {
        // Arrange
        _repository.GetByIdAsync(1).Returns(CreatePendingOrder());
        _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
            .Returns(new PaymentResult(false, "", "Error"));

        // Act
        await _sut.ProcessOrderAsync(1);

        // Assert — перевіряємо, що замовлення збережено зі статусом Failed
        await _repository.Received(1).SaveAsync(
            Arg.Is<Order>(o => o.Status == OrderStatus.Failed));
    }

    // ── Тестування винятків ──────────────────────────────

    [Fact]
    public async Task ProcessOrder_OrderNotFound_ThrowsKeyNotFoundExceptionAsync()
    {
        // Arrange — репозиторій повертає null
        _repository.GetByIdAsync(999).Returns((Order?)null);

        // Act & Assert
        var ex = await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.ProcessOrderAsync(999));

        ex.Message.ShouldContain("999");
    }

    [Fact]
    public async Task ProcessOrder_OrderNotPending_ThrowsInvalidOperationAsync()
    {
        // Arrange — замовлення вже оплачене
        var paidOrder = new Order(1, "a@b.com", "tok", 50m, OrderStatus.Paid);
        _repository.GetByIdAsync(1).Returns(paidOrder);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ProcessOrderAsync(1));

        // Перевіряємо, що оплата не була спробована
        await _paymentGateway.DidNotReceive()
            .ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>());
    }
}
```

---

## 6. Поглиблений розгляд NSubstitute

### 6.1 Зіставлення аргументів

Зіставлення аргументів дозволяє налаштовувати повернення або перевіряти виклики незалежно від конкретних значень аргументів:

```csharp
// Зіставлення будь-якого значення типу
_repository.GetByIdAsync(Arg.Any<int>()).Returns(someOrder);

// Зіставлення з умовою
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Is<decimal>(d => d > 0))
    .Returns(successResult);

// Зіставлення конкретного аргументу для перевірки
await _emailSender.Received()
    .SendOrderConfirmationAsync(
        Arg.Is<string>(e => e.Contains("@")),  // будь-який валідний email
        Arg.Any<int>(),                         // будь-який ID замовлення
        Arg.Is<decimal>(t => t > 0));           // додатна сума
```

### 6.2 Повернення кількох значень

```csharp
// Повернення різних значень при послідовних викликах
_repository.GetByIdAsync(1)
    .Returns(
        new Order(1, "a@b.com", "tok", 50m, OrderStatus.Pending),   // 1-й виклик
        new Order(1, "a@b.com", "tok", 50m, OrderStatus.Paid));     // 2-й виклик

// Повернення на основі аргументів за допомогою функції
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
    .Returns(callInfo =>
    {
        var amount = callInfo.ArgAt<decimal>(1);
        return amount > 1000
            ? new PaymentResult(false, "", "Amount exceeds limit")
            : new PaymentResult(true, $"txn_{amount}");
    });
```

### 6.3 Генерація винятків

```csharp
// Симуляція збоїв інфраструктури
_repository.GetByIdAsync(Arg.Any<int>())
    .ThrowsAsync(new TimeoutException("Database connection timed out"));

// Генерація винятку за конкретних умов
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Is<decimal>(d => d <= 0))
    .ThrowsAsync(new ArgumentException("Amount must be positive"));
```

### 6.4 Захоплення аргументів з `Arg.Do`

```csharp
[Fact]
public async Task ProcessOrder_Success_SavesOrderWithCorrectDataAsync()
{
    // Arrange
    Order? savedOrder = null;

    _repository.GetByIdAsync(1).Returns(CreatePendingOrder());
    _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
        .Returns(new PaymentResult(true, "txn_789"));

    // Захоплення аргументу, переданого до SaveAsync
    await _repository.SaveAsync(Arg.Do<Order>(o => savedOrder = o));

    // Act
    await _sut.ProcessOrderAsync(1);

    // Assert — перевірка захопленого аргументу
    savedOrder.ShouldNotBeNull();
    savedOrder.Status.ShouldBe(OrderStatus.Paid);
    savedOrder.CustomerEmail.ShouldBe("customer@example.com");
}
```

### 6.5 Перевірка порядку та кількості викликів

```csharp
// Перевірка точної кількості викликів
await _repository.Received(1).SaveAsync(Arg.Any<Order>());

// Перевірка щонайменше N викликів
await _emailSender.Received(Arg.Any<int>())
    .SendOrderConfirmationAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>());

// Перевірка відсутності викликів
await _paymentGateway.DidNotReceive()
    .ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>());

// Перевірка порядку викликів (NSubstitute 5.x+)
Received.InOrder(async () =>
{
    await _paymentGateway.ChargeAsync("tok_visa", 99.99m);
    await _repository.SaveAsync(Arg.Any<Order>());
    await _emailSender.SendOrderConfirmationAsync(
        Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>());
});
```

---

## 7. Організація тестів та найкращі практики

### 7.1 Структура проєкту

```
src/
  EStore/
    Services/
      OrderProcessingService.cs
    Models/
      Order.cs
    Interfaces/
      IOrderRepository.cs
      IPaymentGateway.cs
      IEmailSender.cs

tests/
  EStore.Tests/
    Services/
      OrderProcessingServiceTests.cs
    Models/
      OrderTests.cs
```

**Конвенція:** Дзеркально відтворюйте структуру вихідного проєкту в тестовому проєкті. Називайте тестові файли `{ClassName}Tests.cs`.

### 7.2 Організація тестового класу

Групуйте тести логічно всередині класу:

```csharp
public class ShoppingCartTests
{
    // ── Додавання товарів ────────────────────────────
    [Fact] public void AddItem_ValidProduct_AddsToCart() { ... }
    [Fact] public void AddItem_DuplicateProduct_IncreasesQuantity() { ... }
    [Fact] public void AddItem_InvalidPrice_ThrowsException() { ... }

    // ── Видалення товарів ────────────────────────────
    [Fact] public void RemoveItem_ExistingItem_RemovesFromCart() { ... }
    [Fact] public void RemoveItem_NonExistent_ReturnsFalse() { ... }

    // ── Розрахунок підсумків ─────────────────────────
    [Fact] public void GetTotal_MultipleItems_ReturnsSumOfPriceTimesQty() { ... }
    [Fact] public void GetTotal_EmptyCart_ReturnsZero() { ... }
}
```

### 7.3 Поширені антипатерни

| Антипатерн | Проблема | Виправлення |
|---|---|---|
| **Тестування деталей реалізації** | Тести ламаються при рефакторингу, навіть якщо поведінка не змінилась | Тестуйте публічну поведінку, а не приватні методи |
| **Надмірне мокування** | Тести знають занадто багато про внутрішню будову | Мокуйте лише прямі залежності |
| **Спільний змінний стан** | Тести впливають один на одного | Новий екземпляр на тест (xUnit створює новий екземпляр класу на кожен тест) |
| **Нестабільні тести** | Залежать від часу, мережі або порядку виконання | Використовуйте детерміністичні вхідні дані, мокуйте час |
| **Логіка в тестах** | `if/else` або цикли в тестах | Кожен тест повинен бути прямою лінією |
| **Магічні числа** | `result.ShouldBe(42)` — звідки 42? | Використовуйте іменовані константи або обчислені очікувані значення |

### 7.4 Що НЕ мокувати

```
МОКУВАТИ:                          НЕ мокувати:
─────────                          ────────────
✓ Репозиторії / бази даних         ✗ Клас, що тестується
✓ Зовнішні API / HTTP-клієнти     ✗ Прості об'єкти-значення (DTO, records)
✓ Email / сервіси сповіщень       ✗ Чисті функції без побічних ефектів
✓ Платіжні шлюзи                  ✗ Стандартну бібліотеку (List, Dictionary)
✓ Файлову систему / годинник      ✗ Все підряд (надмірне мокування)
```

**Правило:** Мокуйте те, що є **повільним**, **недетерміністичним** або має **побічні ефекти**. Не мокуйте те, що є швидким і детерміністичним.

---

## 8. Тестування граничних випадків

### 8.1 Поширені граничні випадки для розгляду

```
Категорія         Приклади
─────────         ────────
Null/Порожнє      null, "", [], 0
Межі              int.MinValue, int.MaxValue, DateTime.MinValue
Спецсимволи       Unicode, емодзі, рядки SQL-ін'єкцій, HTML
Великі вхідні дані Дуже довгі рядки, величезні колекції
Конкурентність    Одночасний доступ, стани гонки
Стан              Об'єкт використаний після утилізації, подвійна ініціалізація
```

### 8.2 Приклад: Тестування валідатора паролів

```csharp
public class PasswordValidator
{
    public ValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
            return new ValidationResult(false, ["Password is required."]);

        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters.");
        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain an uppercase letter.");
        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain a digit.");
        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            errors.Add("Password must contain a special character.");

        return new ValidationResult(errors.Count == 0, errors);
    }
}

public record ValidationResult(bool IsValid, List<string> Errors);
```

```csharp
public class PasswordValidatorTests
{
    private readonly PasswordValidator _validator = new();

    // ── Валідні паролі ──────────────────────────────

    [Theory]
    [InlineData("Str0ng!Pass")]
    [InlineData("MyP@ssw0rd")]
    [InlineData("12345678Aa!")]
    public void Validate_StrongPassword_ReturnsValid(string password)
    {
        var result = _validator.Validate(password);

        result.IsValid.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    // ── Невалідні паролі ────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NullOrEmpty_ReturnsInvalidWithRequiredError(
        string? password)
    {
        var result = _validator.Validate(password!);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Password is required.");
    }

    [Fact]
    public void Validate_TooShort_ReturnsLengthError()
    {
        var result = _validator.Validate("Ab1!");

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Password must be at least 8 characters.");
    }

    [Fact]
    public void Validate_NoUppercase_ReturnUppercaseError()
    {
        var result = _validator.Validate("lowercase1!");

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(
            "Password must contain an uppercase letter.");
    }

    [Fact]
    public void Validate_NoDigit_ReturnsDigitError()
    {
        var result = _validator.Validate("NoDigits!Here");

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain("Password must contain a digit.");
    }

    [Fact]
    public void Validate_NoSpecialChar_ReturnsSpecialCharError()
    {
        var result = _validator.Validate("NoSpecial1Here");

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(
            "Password must contain a special character.");
    }

    [Fact]
    public void Validate_MultipleViolations_ReturnsAllErrors()
    {
        var result = _validator.Validate("short");

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThan(1);
    }
}
```

---

## 9. Практична вправа

### Завдання: Створити та протестувати `NotificationService`

Створіть `NotificationService`, який відправляє сповіщення через різні канали залежно від налаштувань користувача.

**Інтерфейси:**

```csharp
public interface IUserPreferenceRepository
{
    Task<UserPreferences> GetPreferencesAsync(int userId);
}

public interface ISmsProvider
{
    Task<bool> SendSmsAsync(string phoneNumber, string message);
}

public interface IPushNotificationProvider
{
    Task<bool> SendPushAsync(string deviceToken, string title, string body);
}

public record UserPreferences(
    int UserId,
    bool SmsEnabled,
    bool PushEnabled,
    string? PhoneNumber,
    string? DeviceToken);
```

**Ваше завдання:**
1. Реалізувати `NotificationService.NotifyAsync(int userId, string message)`
2. Написати модульні тести з NSubstitute для покриття:
   - Користувач з увімкненим SMS → SMS відправлено
   - Користувач з увімкненим Push → push-сповіщення відправлено
   - Користувач з обома увімкненими → обидва відправлені
   - Користувач з обома вимкненими → нічого не відправлено, повертає відповідний результат
   - SMS не вдалося → відповідна обробка помилок
   - Користувач не знайдений → генерує виняток

> **Дискусія (15 хв):** Які граничні випадки варто розглянути? Що відбувається, якщо SMS-провайдер недоступний? Чи потрібно повторювати? Чи слід відправляти push, якщо SMS не вдалося?

---

## 10. Підсумок

### Ключові висновки

1. **Модульні тести перевіряють ізольовану поведінку** — використовуйте тестові дублери для видалення залежностей
2. **Патерн AAA** (Arrange-Act-Assert) надає тестам чітку, послідовну структуру
3. **`[Fact]`** для окремих випадків, **`[Theory]`** для параметризованих тестів
4. **Shouldly** забезпечує читабельні перевірки з чіткими повідомленнями про збої
5. **NSubstitute** — чистий фреймворк мокування:
   - `.Returns()` для стабування значень
   - `.Received()` / `.DidNotReceive()` для перевірки взаємодій
   - `Arg.Any<T>()` та `Arg.Is<T>()` для гнучкого зіставлення аргументів
6. **Мокуйте межі, а не внутрішні частини** — мокуйте I/O, зовнішні сервіси та інфраструктуру
7. **Тестуйте поведінку, а не реалізацію** — тести повинні переживати рефакторинг

### Анонс наступної лекції

У **Лекції 3: Інтеграційне тестування з WebApplicationFactory** ми:
- Тестуватимемо ASP.NET Core API наскрізно з `WebApplicationFactory`
- Замінюватимемо сервіси та конфігурацію для тестування
- Тестуватимемо HTTP-ендпоінти, коди стану та тіла відповідей
- Обробляємо аутентифікацію в інтеграційних тестах

---

## Посилання та додаткова література

- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **NSubstitute Documentation** — https://nsubstitute.github.io/help/getting-started/
- **"The Art of Unit Testing"** — Roy Osherove (Manning, 3rd edition, 2024)
- **"Unit Testing Principles, Practices, and Patterns"** — Vladimir Khorikov (Manning, 2020)
- **ISTQB Foundation Level Syllabus** (v4.0, 2023) — Розділ 2
