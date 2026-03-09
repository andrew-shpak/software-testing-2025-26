# Lecture 2: Unit Testing and Mocking

## Learning Objectives

By the end of this lecture, students will be able to:

- Explain the principles and value of unit testing
- Structure tests using the Arrange-Act-Assert (AAA) pattern
- Write parameterized tests with `[Fact]` and `[Theory]`
- Understand test doubles: stubs, mocks, fakes, and spies
- Use NSubstitute to isolate dependencies in unit tests
- Apply best practices for naming, organizing, and maintaining tests

---

## 1. Recap: What is Unit Testing?

### 1.1 Definition

A **unit test** verifies the smallest testable piece of software — typically a single method or function — **in isolation** from its dependencies.

```
┌──────────────────────────────────────────────┐
│  Unit Test                                   │
│                                              │
│   Input ──► [Method Under Test] ──► Output   │
│                     │                        │
│              (dependencies are               │
│               mocked/stubbed)                │
│                                              │
│   Assert: output matches expectation         │
└──────────────────────────────────────────────┘
```

### 1.2 F.I.R.S.T. Principles

Good unit tests follow the **F.I.R.S.T.** principles:

| Principle | Description |
|---|---|
| **Fast** | Run in milliseconds; the full suite in seconds |
| **Isolated** | No dependencies on other tests, databases, network, file system |
| **Repeatable** | Same result every time, in any environment |
| **Self-validating** | Pass or fail automatically — no manual inspection |
| **Timely** | Written close in time to the production code |

### 1.3 What Makes a Good Unit Test?

```
Good Unit Test                      Bad Unit Test
───────────────                     ─────────────
✓ Tests one behavior                ✗ Tests multiple things
✓ Descriptive name                  ✗ Named "Test1", "TestMethod"
✓ Independent of other tests        ✗ Depends on test execution order
✓ Fast (milliseconds)               ✗ Slow (calls DB, network)
✓ Deterministic                     ✗ Flaky (depends on time, random)
✓ Easy to read and understand       ✗ Complex setup, unclear intent
```

---

## 2. Anatomy of a Unit Test

### 2.1 The AAA Pattern

Every well-structured test follows **Arrange-Act-Assert**:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange — set up preconditions and inputs
    var service = new PriceCalculator();
    var basePrice = 100m;

    // Act — execute the behavior under test
    var result = service.CalculateWithTax(basePrice, taxRate: 0.2m);

    // Assert — verify the expected outcome
    result.ShouldBe(120m);
}
```

**Guidelines:**
- Each section should be clearly identifiable (comments are optional once you're used to the pattern)
- Prefer a **single Act** — one method call per test
- Prefer **focused assertions** — assert one logical concept (can be multiple `ShouldBe` calls if they verify the same concept)

### 2.2 Test Naming Conventions

Test names should describe the **behavior**, not the implementation:

```
MethodName_Scenario_ExpectedBehavior
```

| Good | Bad |
|---|---|
| `CalculateTotal_EmptyCart_ReturnsZero` | `TestCalculateTotal` |
| `Login_InvalidPassword_ThrowsAuthException` | `LoginTest2` |
| `IsEligible_AgeBelow18_ReturnsFalse` | `CheckAge` |

The name should read like a specification: *"When I call CalculateTotal on an empty cart, it returns zero."*

### 2.3 `[Fact]` vs. `[Theory]`

#### `[Fact]` — Single Test Case

Use when a test has fixed inputs:

```csharp
[Fact]
public void Add_TwoPositiveNumbers_ReturnsSum()
{
    var calc = new Calculator();
    calc.Add(2, 3).ShouldBe(5);
}
```

#### `[Theory]` — Parameterized Test Cases

Use when the same logic should be tested with multiple inputs:

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

#### Other Data Sources for `[Theory]`

```csharp
// MemberData — use a method or property for complex test data
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

## 3. Shouldly: Expressive Assertions

### 3.1 Why Shouldly?

Compare the error messages:

```
// xUnit built-in assert
Assert.Equal(5, result);
// Output: Assert.Equal() Failure. Expected: 5, Actual: 4

// Shouldly
result.ShouldBe(5);
// Output: result should be 5 but was 4
```

Shouldly produces **human-readable error messages** that include the variable name.

### 3.2 Common Shouldly Assertions

```csharp
// Equality
result.ShouldBe(42);
name.ShouldBe("Alice");

// Boolean
isValid.ShouldBeTrue();
isEmpty.ShouldBeFalse();

// Null
order.ShouldNotBeNull();
deletedItem.ShouldBeNull();

// Numeric comparisons
temperature.ShouldBeGreaterThan(0);
discount.ShouldBeLessThanOrEqualTo(100);
balance.ShouldBePositive();
diff.ShouldBeNegative();

// Collections
items.ShouldNotBeEmpty();
items.Count.ShouldBe(3);
items.ShouldContain("Apple");
items.ShouldAllBe(i => i.Price > 0);
items.ShouldBeInOrder();

// Strings
email.ShouldContain("@");
name.ShouldStartWith("Dr.");
code.ShouldMatch(@"^[A-Z]{3}-\d{4}$"); // regex

// Type checking
shape.ShouldBeOfType<Circle>();
animal.ShouldBeAssignableTo<IMammal>();

// Exceptions
Should.Throw<ArgumentNullException>(() => service.Process(null!));
var ex = Should.Throw<InvalidOperationException>(
    () => account.Withdraw(1000));
ex.Message.ShouldContain("insufficient funds");

// Async exceptions
await Should.ThrowAsync<TimeoutException>(
    () => service.FetchDataAsync());

// Approximate equality (for floating point)
result.ShouldBe(3.14, tolerance: 0.01);

// Time-based
elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
```

---

## 4. Test Doubles

### 4.1 Why Test Doubles?

In real applications, classes depend on other classes. Unit tests need to **isolate** the class under test from its dependencies:

```
Production Code:                   Unit Test:
┌──────────┐                       ┌──────────┐
│  Order   │──► IPaymentGateway    │  Order   │──► Fake/Mock
│ Service  │──► IEmailSender       │ Service  │──► Fake/Mock
│          │──► IOrderRepository   │          │──► Fake/Mock
└──────────┘                       └──────────┘
     │                                  │
Uses real services                 Uses test doubles
(DB, email, payment)               (fast, controlled, isolated)
```

### 4.2 Types of Test Doubles

| Type | Purpose | Example |
|---|---|---|
| **Dummy** | Fills a parameter; never actually used | `new object()` passed to satisfy a signature |
| **Stub** | Returns predetermined values | Always returns `true` for `IsAvailable()` |
| **Spy** | Records calls for later verification | Records that `SendEmail()` was called twice |
| **Mock** | Pre-programmed with expectations | Expects `Save()` to be called exactly once |
| **Fake** | Working implementation (simplified) | In-memory database instead of real DB |

In practice, the term **"mock"** is often used loosely to mean any test double. NSubstitute creates substitutes that can act as stubs, mocks, and spies.

### 4.3 When to Use Test Doubles

Use test doubles when the dependency:
- Is **slow** (database, network, file system)
- Is **non-deterministic** (current time, random numbers, external APIs)
- Has **side effects** (sending emails, charging credit cards)
- Is **hard to set up** (complex initialization)
- **Doesn't exist yet** (team is building it in parallel)

---

## 5. Mocking with NSubstitute

### 5.1 Why NSubstitute?

NSubstitute uses a **clean, fluent syntax** with no "magic strings" or complex setup:

```csharp
// NSubstitute — clean and readable
var calculator = Substitute.For<ICalculator>();
calculator.Add(1, 2).Returns(3);

// Compare with other frameworks (more verbose):
// var mock = new Mock<ICalculator>();
// mock.Setup(c => c.Add(1, 2)).Returns(3);
// var calculator = mock.Object;
```

### 5.2 Setup

```bash
dotnet add package NSubstitute
```

```csharp
using NSubstitute;
```

### 5.3 Creating Substitutes

```csharp
// Create a substitute for an interface
var emailSender = Substitute.For<IEmailSender>();

// Create a substitute for an abstract class
var logger = Substitute.For<AbstractLogger>();

// Create a substitute for multiple interfaces
var combo = Substitute.For<IEmailSender, IDisposable>();
```

### 5.4 Full Example: Order Processing Service

#### Production Code

```csharp
// Interfaces
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
// Service under test
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

        // Charge payment
        var payment = await paymentGateway.ChargeAsync(
            order.CardToken, order.Total);

        if (!payment.Success)
        {
            var failedOrder = order with { Status = OrderStatus.Failed };
            await repository.SaveAsync(failedOrder);
            return new OrderResult(false, $"Payment failed: {payment.Error}");
        }

        // Update order status
        var paidOrder = order with { Status = OrderStatus.Paid };
        await repository.SaveAsync(paidOrder);

        // Send confirmation email
        await emailSender.SendOrderConfirmationAsync(
            order.CustomerEmail, order.Id, order.Total);

        return new OrderResult(true, $"Order processed. Transaction: {payment.TransactionId}");
    }
}

public record OrderResult(bool Success, string Message);
```

#### Test Class with NSubstitute

```csharp
using NSubstitute;
using Shouldly;

namespace EStore.Tests;

public class OrderProcessingServiceTests
{
    // Dependencies (substitutes)
    private readonly IOrderRepository _repository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEmailSender _emailSender;

    // System under test
    private readonly OrderProcessingService _sut;

    public OrderProcessingServiceTests()
    {
        _repository = Substitute.For<IOrderRepository>();
        _paymentGateway = Substitute.For<IPaymentGateway>();
        _emailSender = Substitute.For<IEmailSender>();

        _sut = new OrderProcessingService(
            _repository, _paymentGateway, _emailSender);
    }

    // ── Helper ──────────────────────────────────────────

    private static Order CreatePendingOrder(int id = 1) =>
        new(id, "customer@example.com", "tok_visa", 99.99m, OrderStatus.Pending);

    // ── Stubbing: Controlling Return Values ─────────────

    [Fact]
    public async Task ProcessOrder_SuccessfulPayment_ReturnSuccessAsync()
    {
        // Arrange — stub the repository to return a pending order
        var order = CreatePendingOrder();
        _repository.GetByIdAsync(1).Returns(order);

        // Stub the payment gateway to succeed
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

    // ── Verifying Calls (Mock Behavior) ─────────────────

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

        // Assert — verify that email was sent with correct parameters
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

        // Assert — verify email was NOT sent
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

        // Assert — verify the order was saved with Failed status
        await _repository.Received(1).SaveAsync(
            Arg.Is<Order>(o => o.Status == OrderStatus.Failed));
    }

    // ── Exception Testing ───────────────────────────────

    [Fact]
    public async Task ProcessOrder_OrderNotFound_ThrowsKeyNotFoundExceptionAsync()
    {
        // Arrange — repository returns null
        _repository.GetByIdAsync(999).Returns((Order?)null);

        // Act & Assert
        var ex = await Should.ThrowAsync<KeyNotFoundException>(
            () => _sut.ProcessOrderAsync(999));

        ex.Message.ShouldContain("999");
    }

    [Fact]
    public async Task ProcessOrder_OrderNotPending_ThrowsInvalidOperationAsync()
    {
        // Arrange — order is already paid
        var paidOrder = new Order(1, "a@b.com", "tok", 50m, OrderStatus.Paid);
        _repository.GetByIdAsync(1).Returns(paidOrder);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ProcessOrderAsync(1));

        // Verify no payment was attempted
        await _paymentGateway.DidNotReceive()
            .ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>());
    }
}
```

---

## 6. NSubstitute Deep Dive

### 6.1 Argument Matchers

Argument matchers let you set up returns or verify calls regardless of specific argument values:

```csharp
// Match any value of a type
_repository.GetByIdAsync(Arg.Any<int>()).Returns(someOrder);

// Match with a condition
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Is<decimal>(d => d > 0))
    .Returns(successResult);

// Match a specific argument for verification
await _emailSender.Received()
    .SendOrderConfirmationAsync(
        Arg.Is<string>(e => e.Contains("@")),  // any valid email
        Arg.Any<int>(),                         // any order ID
        Arg.Is<decimal>(t => t > 0));           // positive total
```

### 6.2 Returning Multiple Values

```csharp
// Return different values on consecutive calls
_repository.GetByIdAsync(1)
    .Returns(
        new Order(1, "a@b.com", "tok", 50m, OrderStatus.Pending),   // 1st call
        new Order(1, "a@b.com", "tok", 50m, OrderStatus.Paid));     // 2nd call

// Return based on arguments using a function
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
    .Returns(callInfo =>
    {
        var amount = callInfo.ArgAt<decimal>(1);
        return amount > 1000
            ? new PaymentResult(false, "", "Amount exceeds limit")
            : new PaymentResult(true, $"txn_{amount}");
    });
```

### 6.3 Throwing Exceptions

```csharp
// Simulate infrastructure failures
_repository.GetByIdAsync(Arg.Any<int>())
    .ThrowsAsync(new TimeoutException("Database connection timed out"));

// Throw on specific conditions
_paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Is<decimal>(d => d <= 0))
    .ThrowsAsync(new ArgumentException("Amount must be positive"));
```

### 6.4 Capturing Arguments with `Arg.Do`

```csharp
[Fact]
public async Task ProcessOrder_Success_SavesOrderWithCorrectDataAsync()
{
    // Arrange
    Order? savedOrder = null;

    _repository.GetByIdAsync(1).Returns(CreatePendingOrder());
    _paymentGateway.ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>())
        .Returns(new PaymentResult(true, "txn_789"));

    // Capture the argument passed to SaveAsync
    await _repository.SaveAsync(Arg.Do<Order>(o => savedOrder = o));

    // Act
    await _sut.ProcessOrderAsync(1);

    // Assert — inspect the captured argument
    savedOrder.ShouldNotBeNull();
    savedOrder.Status.ShouldBe(OrderStatus.Paid);
    savedOrder.CustomerEmail.ShouldBe("customer@example.com");
}
```

### 6.5 Verifying Call Order and Count

```csharp
// Verify exact number of calls
await _repository.Received(1).SaveAsync(Arg.Any<Order>());

// Verify at least N calls
await _emailSender.Received(Arg.Any<int>())
    .SendOrderConfirmationAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>());

// Verify no calls were made
await _paymentGateway.DidNotReceive()
    .ChargeAsync(Arg.Any<string>(), Arg.Any<decimal>());

// Verify call order (NSubstitute 5.x+)
Received.InOrder(async () =>
{
    await _paymentGateway.ChargeAsync("tok_visa", 99.99m);
    await _repository.SaveAsync(Arg.Any<Order>());
    await _emailSender.SendOrderConfirmationAsync(
        Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>());
});
```

---

## 7. Test Organization and Best Practices

### 7.1 Project Structure

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

**Convention:** Mirror the source project structure in the test project. Name test files `{ClassName}Tests.cs`.

### 7.2 Test Class Organization

Group tests logically within a class:

```csharp
public class ShoppingCartTests
{
    // ── Adding Items ────────────────────────────────
    [Fact] public void AddItem_ValidProduct_AddsToCart() { ... }
    [Fact] public void AddItem_DuplicateProduct_IncreasesQuantity() { ... }
    [Fact] public void AddItem_InvalidPrice_ThrowsException() { ... }

    // ── Removing Items ──────────────────────────────
    [Fact] public void RemoveItem_ExistingItem_RemovesFromCart() { ... }
    [Fact] public void RemoveItem_NonExistent_ReturnsFalse() { ... }

    // ── Calculating Totals ──────────────────────────
    [Fact] public void GetTotal_MultipleItems_ReturnsSumOfPriceTimesQty() { ... }
    [Fact] public void GetTotal_EmptyCart_ReturnsZero() { ... }
}
```

### 7.3 Common Anti-Patterns

| Anti-Pattern | Problem | Fix |
|---|---|---|
| **Testing implementation details** | Tests break when refactoring, even if behavior is unchanged | Test public behavior, not private methods |
| **Over-mocking** | Tests know too much about internal wiring | Only mock direct dependencies |
| **Shared mutable state** | Tests affect each other | New instance per test (xUnit creates a new class instance per test) |
| **Flaky tests** | Depend on timing, network, or execution order | Use deterministic inputs, mock time |
| **Test logic** | `if/else` or loops in tests | Each test should be a straight line |
| **Magic numbers** | `result.ShouldBe(42)` — where does 42 come from? | Use named constants or computed expected values |

### 7.4 What NOT to Mock

```
DO mock:                           DO NOT mock:
─────────                          ────────────
✓ Repositories / databases         ✗ The class under test
✓ External APIs / HTTP clients     ✗ Simple value objects (DTOs, records)
✓ Email / notification services    ✗ Pure functions with no side effects
✓ Payment gateways                 ✗ Standard library (List, Dictionary)
✓ File system / clock              ✗ Everything (over-mocking)
```

**Rule of thumb:** Mock things that are **slow**, **non-deterministic**, or have **side effects**. Don't mock things that are fast and deterministic.

---

## 8. Testing Edge Cases

### 8.1 Common Edge Cases to Consider

```
Category          Examples
─────────         ────────
Null/Empty        null, "", [], 0
Boundaries        int.MinValue, int.MaxValue, DateTime.MinValue
Special chars     Unicode, emojis, SQL injection strings, HTML
Large inputs      Very long strings, huge collections
Concurrent        Simultaneous access, race conditions
State             Object used after disposal, double initialization
```

### 8.2 Example: Testing a Password Validator

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

    // ── Valid Passwords ─────────────────────────────

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

    // ── Invalid Passwords ───────────────────────────

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

## 9. Practical Exercise

### Task: Build and Test a `NotificationService`

Build a `NotificationService` that sends notifications through different channels based on user preferences.

**Interfaces:**

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

**Your task:**
1. Implement `NotificationService.NotifyAsync(int userId, string message)`
2. Write unit tests using NSubstitute to cover:
   - User with SMS enabled → SMS is sent
   - User with Push enabled → push notification is sent
   - User with both enabled → both are sent
   - User with neither enabled → nothing is sent, returns appropriate result
   - SMS fails → appropriate error handling
   - User not found → throws exception

> **Discussion (15 min):** What edge cases should we consider? What happens if the SMS provider is down? Should we retry? Should we send push even if SMS fails?

---

## 10. Summary

### Key Takeaways

1. **Unit tests verify isolated behavior** — use test doubles to remove dependencies
2. **AAA pattern** (Arrange-Act-Assert) gives tests a clear, consistent structure
3. **`[Fact]`** for single cases, **`[Theory]`** for parameterized tests
4. **Shouldly** provides readable assertions with clear failure messages
5. **NSubstitute** is a clean mocking framework:
   - `.Returns()` to stub values
   - `.Received()` / `.DidNotReceive()` to verify interactions
   - `Arg.Any<T>()` and `Arg.Is<T>()` for flexible argument matching
6. **Mock boundaries, not internals** — mock I/O, external services, and infrastructure
7. **Test behavior, not implementation** — tests should survive refactoring

### Preview of Next Lecture

In **Lecture 3: Integration Testing with WebApplicationFactory**, we will:
- Test ASP.NET Core APIs end-to-end using `WebApplicationFactory`
- Replace services and configuration for testing
- Test HTTP endpoints, status codes, and response bodies
- Handle authentication in integration tests

---

## References and Further Reading

- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **NSubstitute Documentation** — https://nsubstitute.github.io/help/getting-started/
- **"The Art of Unit Testing"** — Roy Osherove (Manning, 3rd edition, 2024)
- **"Unit Testing Principles, Practices, and Patterns"** — Vladimir Khorikov (Manning, 2020)
- **ISTQB Foundation Level Syllabus** (v4.0, 2023) — Chapter 2
