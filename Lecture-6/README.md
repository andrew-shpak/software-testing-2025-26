# Лекція 6: Техніки проєктування тестів та покриття коду

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Explain why systematic test design is superior to ad-hoc testing
- Apply black-box test design techniques: equivalence partitioning, boundary value analysis, decision tables, state transition testing, and pairwise testing
- Apply white-box test design techniques: statement, branch, condition, MC/DC, and path coverage
- Set up Coverlet and ReportGenerator to measure and visualize code coverage in .NET
- Interpret coverage reports and understand what coverage measures and what it does not
- Choose the appropriate test design technique for a given testing scenario
- Integrate coverage thresholds into a CI pipeline

---

## 1. Чому систематичне проєктування тестів має значення

### 1.1 Ad-Hoc тестування vs. Систематичне тестування

When developers write tests without a method, they tend to test only the cases they thought of while coding — the "happy path" and a few obvious errors. This is **ad-hoc testing**, and it leaves large gaps.

| Ad-Hoc Testing | Systematic Testing |
|---|---|
| Based on intuition and experience | Based on defined techniques and rules |
| Coverage is unknown and inconsistent | Coverage is measurable and deliberate |
| Easy to miss boundary conditions | Boundaries are explicitly targeted |
| Hard to justify "enough testing" | Provides rationale for test case selection |
| Duplicate effort — same areas tested repeatedly | Minimizes redundancy, maximizes fault detection |
| Results vary by tester | Results are reproducible and reviewable |

### 1.2 Фундаментальна проблема

Recall from Lecture 1: **exhaustive testing is impossible**. Even a simple function with two `int` parameters has 2^32 x 2^32 = 2^64 possible input combinations — roughly 18.4 quintillion test cases.

Test design techniques solve this problem by providing **systematic strategies** to select a small but effective subset of test cases that maximizes the chance of finding defects.

```
All possible inputs:  ████████████████████████████████████████████████
                      (billions or more)

Ad-hoc selection:     █  █       █            █    █
                      (random, gaps, overlaps)

Systematic selection: █ █ █ █ █ █ █ █ █ █ █ █ █ █ █
                      (structured, representative, boundary-focused)
```

### 1.3 Дві родини технік

Test design techniques fall into two broad categories:

```
Test Design Techniques
├── Black-Box (Specification-Based)
│   ├── Equivalence Partitioning
│   ├── Boundary Value Analysis
│   ├── Decision Table Testing
│   ├── State Transition Testing
│   └── Pairwise / Combinatorial Testing
│
└── White-Box (Structure-Based)
    ├── Statement Coverage
    ├── Branch / Decision Coverage
    ├── Condition Coverage
    ├── MC/DC Coverage
    └── Path Coverage
```

- **Black-box techniques** derive tests from the **specification** — what the system should do — without looking at the code.
- **White-box techniques** derive tests from the **code structure** — ensuring that specific code elements are exercised.

Both are needed. Black-box techniques test *what* the software does; white-box techniques ensure *how much* of the code is exercised.

> **Discussion (5 min):** Think about a function you wrote recently. How did you decide which test cases to write? Did you use any systematic approach, or was it ad-hoc?

---

## 2. Техніки проєктування тестів методом чорної скриньки

Техніки чорної скриньки розглядають систему, що тестується, як непрозору коробку: ви знаєте вхідні дані та очікувані результати, але не внутрішню реалізацію.

### 2.1 Еквівалентне розбиття (EP)

#### Ідея

Inputs to a system can be divided into groups (partitions) where all values in a group are expected to be treated the same way. If one value in a partition works correctly, we assume all values in that partition will work correctly.

This lets us replace millions of test cases with one representative per partition.

#### Як застосовувати

1. Identify the input domain
2. Divide inputs into **valid** and **invalid** equivalence partitions
3. Select one representative value from each partition
4. Write a test case for each representative

#### Приклад: Ціноутворення квитків за віком

**Specification:** A cinema charges different prices based on age:
- Under 5: Free
- 5-12: Child ticket ($8)
- 13-17: Youth ticket ($12)
- 18-64: Adult ticket ($16)
- 65+: Senior ticket ($10)
- Negative age: Invalid (throw exception)

```
Equivalence Partitions:

Invalid     Valid Partitions
  │    ┌───────┬───────┬───────┬───────┬──────────┐
  │    │ < 0   │ 0-4   │ 5-12  │ 13-17 │ 18-64    │ 65+
  │    │Invalid│ Free  │ Child │ Youth │ Adult    │ Senior
  ▼    └───────┴───────┴───────┴───────┴──────────┘
──────┼───┼───────┼───────┼───────┼────────────┼────────►
     -1   0   2   5   8  13  15  18    40     65   80
          ▲       ▲       ▲       ▲          ▲       ▲
        representatives (one per partition)
```

```csharp
// TicketPricing/PriceCalculator.cs
namespace TicketPricing;

public class PriceCalculator
{
    public decimal GetTicketPrice(int age)
    {
        return age switch
        {
            < 0 => throw new ArgumentOutOfRangeException(
                       nameof(age), "Age cannot be negative."),
            < 5 => 0m,
            < 13 => 8m,
            < 18 => 12m,
            < 65 => 16m,
            _ => 10m,
        };
    }
}
```

```csharp
// TicketPricing.Tests/PriceCalculatorTests.cs
using Shouldly;

namespace TicketPricing.Tests;

public class PriceCalculatorTests
{
    private readonly PriceCalculator _sut = new();

    // --- Equivalence Partitioning: one representative per partition ---

    [Fact]
    public void GetTicketPrice_NegativeAge_ThrowsException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetTicketPrice(-1));
    }

    [Theory]
    [InlineData(2, 0)]      // Free (age 0-4)
    [InlineData(8, 8)]      // Child (age 5-12)
    [InlineData(15, 12)]    // Youth (age 13-17)
    [InlineData(40, 16)]    // Adult (age 18-64)
    [InlineData(80, 10)]    // Senior (age 65+)
    public void GetTicketPrice_ValidAge_ReturnsExpectedPrice(
        int age, decimal expectedPrice)
    {
        _sut.GetTicketPrice(age).ShouldBe(expectedPrice);
    }
}
```

**Number of test cases:** 6 (one per partition) instead of testing every possible age.

### 2.2 Аналіз граничних значень (BVA)

#### Ідея

Defects tend to cluster at the **boundaries** between equivalence partitions. Boundary value analysis focuses on the edges — the minimum, maximum, and values just inside and just outside each boundary.

#### Двозначний vs. Тризначний BVA

| Approach | Values tested at each boundary | ISTQB standard |
|---|---|---|
| **Two-value BVA** | boundary value, boundary + 1 | Yes (minimum) |
| **Three-value BVA** | boundary - 1, boundary, boundary + 1 | Yes (thorough) |

#### Приклад: Продовження ціноутворення квитків

Boundaries are at ages: 0, 4/5, 12/13, 17/18, 64/65.

```
Three-value BVA at the boundary between Child (5-12) and Youth (13-17):

     Child partition          Youth partition
    ─────────────────┼─────────────────────
                 11  12  13  14
                  ▲   ▲   ▲   ▲
                 BVA test points
```

```csharp
public class PriceCalculatorBoundaryTests
{
    private readonly PriceCalculator _sut = new();

    // --- Boundary: Free / Child at age 4 and 5 ---

    [Theory]
    [InlineData(4, 0)]     // last Free age
    [InlineData(5, 8)]     // first Child age
    public void GetTicketPrice_FreeChildBoundary_ReturnsCorrectPrice(
        int age, decimal expected)
    {
        _sut.GetTicketPrice(age).ShouldBe(expected);
    }

    // --- Boundary: Child / Youth at age 12 and 13 ---

    [Theory]
    [InlineData(12, 8)]    // last Child age
    [InlineData(13, 12)]   // first Youth age
    public void GetTicketPrice_ChildYouthBoundary_ReturnsCorrectPrice(
        int age, decimal expected)
    {
        _sut.GetTicketPrice(age).ShouldBe(expected);
    }

    // --- Boundary: Youth / Adult at age 17 and 18 ---

    [Theory]
    [InlineData(17, 12)]   // last Youth age
    [InlineData(18, 16)]   // first Adult age
    public void GetTicketPrice_YouthAdultBoundary_ReturnsCorrectPrice(
        int age, decimal expected)
    {
        _sut.GetTicketPrice(age).ShouldBe(expected);
    }

    // --- Boundary: Adult / Senior at age 64 and 65 ---

    [Theory]
    [InlineData(64, 16)]   // last Adult age
    [InlineData(65, 10)]   // first Senior age
    public void GetTicketPrice_AdultSeniorBoundary_ReturnsCorrectPrice(
        int age, decimal expected)
    {
        _sut.GetTicketPrice(age).ShouldBe(expected);
    }

    // --- Boundary: Invalid / Free at age -1 and 0 ---

    [Fact]
    public void GetTicketPrice_MinusOne_ThrowsException()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetTicketPrice(-1));
    }

    [Fact]
    public void GetTicketPrice_Zero_ReturnsFree()
    {
        _sut.GetTicketPrice(0).ShouldBe(0m);
    }
}
```

> **Key insight:** Equivalence partitioning and boundary value analysis are complementary. EP identifies the partitions; BVA focuses testing effort on the edges where bugs hide.

### 2.3 Тестування за таблицею рішень

#### Ідея

When system behavior depends on **combinations of conditions**, a decision table enumerates all combinations and their expected outcomes. This is especially useful for business rules with multiple interacting conditions.

#### Як побудувати таблицю рішень

1. List all conditions (inputs)
2. List all actions (outputs/behaviors)
3. Create columns for each combination of condition values
4. Fill in the expected action for each combination

#### Приклад: Система схвалення кредитів

**Specification:** A bank approves loans based on three conditions:
- Credit score: Good or Bad
- Employment: Employed or Unemployed
- Existing debt: Low or High

```
Decision Table:

                    Rule 1  Rule 2  Rule 3  Rule 4  Rule 5  Rule 6  Rule 7  Rule 8
Conditions:
  Credit Score       Good    Good    Good    Good    Bad     Bad     Bad     Bad
  Employment         Yes     Yes     No      No      Yes     Yes     No      No
  Low Debt           Yes     No      Yes     No      Yes     No      Yes     No

Actions:
  Approve Loan       Yes     Yes     Yes     No      Yes     No      No      No
  Interest Rate      Low     Medium  Medium   -      High     -       -       -
```

```csharp
// LoanApproval/LoanEvaluator.cs
namespace LoanApproval;

public enum CreditScore { Good, Bad }

public record LoanApplication(
    CreditScore CreditScore,
    bool IsEmployed,
    bool HasLowDebt);

public record LoanDecision(
    bool Approved,
    string? InterestRate = null,
    string? Reason = null);

public class LoanEvaluator
{
    public LoanDecision Evaluate(LoanApplication app)
    {
        return (app.CreditScore, app.IsEmployed, app.HasLowDebt) switch
        {
            (CreditScore.Good, true,  true)  => new(true, "Low"),
            (CreditScore.Good, true,  false) => new(true, "Medium"),
            (CreditScore.Good, false, true)  => new(true, "Medium"),
            (CreditScore.Good, false, false) => new(false, Reason: "Unemployed with high debt"),
            (CreditScore.Bad,  true,  true)  => new(true, "High"),
            (CreditScore.Bad,  true,  false) => new(false, Reason: "Bad credit with high debt"),
            (CreditScore.Bad,  false, true)  => new(false, Reason: "Bad credit and unemployed"),
            (CreditScore.Bad,  false, false) => new(false, Reason: "All risk factors present"),
        };
    }
}
```

```csharp
// LoanApproval.Tests/LoanEvaluatorTests.cs
using Shouldly;

namespace LoanApproval.Tests;

public class LoanEvaluatorTests
{
    private readonly LoanEvaluator _sut = new();

    // One test per decision table rule

    [Theory]
    [InlineData(CreditScore.Good, true,  true,  true,  "Low")]
    [InlineData(CreditScore.Good, true,  false, true,  "Medium")]
    [InlineData(CreditScore.Good, false, true,  true,  "Medium")]
    [InlineData(CreditScore.Good, false, false, false, null)]
    [InlineData(CreditScore.Bad,  true,  true,  true,  "High")]
    [InlineData(CreditScore.Bad,  true,  false, false, null)]
    [InlineData(CreditScore.Bad,  false, true,  false, null)]
    [InlineData(CreditScore.Bad,  false, false, false, null)]
    public void Evaluate_DecisionTableRule_ReturnsExpectedDecision(
        CreditScore credit, bool employed, bool lowDebt,
        bool expectedApproved, string? expectedRate)
    {
        // Arrange
        var application = new LoanApplication(credit, employed, lowDebt);

        // Act
        var decision = _sut.Evaluate(application);

        // Assert
        decision.Approved.ShouldBe(expectedApproved);
        if (expectedApproved)
            decision.InterestRate.ShouldBe(expectedRate);
    }
}
```

**When to use:** Decision tables are ideal when the specification contains complex business rules with multiple interacting conditions (2-4 conditions). With *n* boolean conditions, there are 2^n rules — the table grows exponentially, so it works best for a moderate number of conditions.

### 2.4 Тестування переходів станів

#### Ідея

Many systems have behavior that depends on their **current state** and the **event** that occurs. State transition testing models these as a state machine and derives tests to cover transitions.

#### Компоненти моделі станів

```
┌───────────┐   event [condition] / action   ┌───────────┐
│  State A  │ ─────────────────────────────► │  State B  │
└───────────┘                                └───────────┘
```

- **States:** the possible conditions the system can be in
- **Transitions:** changes from one state to another
- **Events:** triggers that cause transitions
- **Guards:** conditions that must be true for a transition to fire
- **Actions:** operations performed during a transition

#### Приклад: Життєвий цикл замовлення

```
                    ┌──────────────────────────────────────────────┐
                    │                                              │
                    ▼                                              │
 ┌─────────┐  place   ┌─────────┐  pay     ┌──────┐  ship   ┌────────┐
 │  Draft   │────────►│ Pending  │────────►│ Paid  │───────►│ Shipped │
 └─────────┘         └─────────┘         └──────┘        └────────┘
                          │                   │                │
                    cancel│             cancel│          deliver│
                          ▼                   ▼                ▼
                    ┌───────────┐       ┌───────────┐   ┌───────────┐
                    │ Cancelled │       │ Cancelled │   │ Delivered │
                    └───────────┘       └───────────┘   └───────────┘
```

**State Transition Table:**

| Current State | Event | Next State | Action |
|---|---|---|---|
| Draft | Place | Pending | Create order record |
| Pending | Pay | Paid | Record payment |
| Pending | Cancel | Cancelled | Release items |
| Paid | Ship | Shipped | Generate tracking |
| Paid | Cancel | Cancelled | Issue refund |
| Shipped | Deliver | Delivered | Confirm delivery |

```csharp
// OrderLifecycle/Order.cs
namespace OrderLifecycle;

public enum OrderState { Draft, Pending, Paid, Shipped, Delivered, Cancelled }

public class Order
{
    public OrderState State { get; private set; } = OrderState.Draft;

    public void Place()
    {
        if (State != OrderState.Draft)
            throw new InvalidOperationException(
                $"Cannot place order in {State} state.");
        State = OrderState.Pending;
    }

    public void Pay()
    {
        if (State != OrderState.Pending)
            throw new InvalidOperationException(
                $"Cannot pay for order in {State} state.");
        State = OrderState.Paid;
    }

    public void Ship()
    {
        if (State != OrderState.Paid)
            throw new InvalidOperationException(
                $"Cannot ship order in {State} state.");
        State = OrderState.Shipped;
    }

    public void Deliver()
    {
        if (State != OrderState.Shipped)
            throw new InvalidOperationException(
                $"Cannot deliver order in {State} state.");
        State = OrderState.Delivered;
    }

    public void Cancel()
    {
        if (State != OrderState.Pending && State != OrderState.Paid)
            throw new InvalidOperationException(
                $"Cannot cancel order in {State} state.");
        State = OrderState.Cancelled;
    }
}
```

```csharp
// OrderLifecycle.Tests/OrderStateTransitionTests.cs
using Shouldly;

namespace OrderLifecycle.Tests;

public class OrderStateTransitionTests
{
    // --- Valid Transitions (from state transition table) ---

    [Fact]
    public void Place_FromDraft_TransitionsToPending()
    {
        var order = new Order();
        order.Place();
        order.State.ShouldBe(OrderState.Pending);
    }

    [Fact]
    public void Pay_FromPending_TransitionsToPaid()
    {
        var order = new Order();
        order.Place();
        order.Pay();
        order.State.ShouldBe(OrderState.Paid);
    }

    [Fact]
    public void Ship_FromPaid_TransitionsToShipped()
    {
        var order = new Order();
        order.Place();
        order.Pay();
        order.Ship();
        order.State.ShouldBe(OrderState.Shipped);
    }

    [Fact]
    public void Deliver_FromShipped_TransitionsToDelivered()
    {
        var order = new Order();
        order.Place();
        order.Pay();
        order.Ship();
        order.Deliver();
        order.State.ShouldBe(OrderState.Delivered);
    }

    [Fact]
    public void Cancel_FromPending_TransitionsToCancelled()
    {
        var order = new Order();
        order.Place();
        order.Cancel();
        order.State.ShouldBe(OrderState.Cancelled);
    }

    [Fact]
    public void Cancel_FromPaid_TransitionsToCancelled()
    {
        var order = new Order();
        order.Place();
        order.Pay();
        order.Cancel();
        order.State.ShouldBe(OrderState.Cancelled);
    }

    // --- Invalid Transitions (negative testing) ---

    [Fact]
    public void Pay_FromDraft_ThrowsException()
    {
        var order = new Order();
        Should.Throw<InvalidOperationException>(() => order.Pay());
    }

    [Fact]
    public void Ship_FromPending_ThrowsException()
    {
        var order = new Order();
        order.Place();
        Should.Throw<InvalidOperationException>(() => order.Ship());
    }

    [Fact]
    public void Cancel_FromDelivered_ThrowsException()
    {
        var order = new Order();
        order.Place();
        order.Pay();
        order.Ship();
        order.Deliver();
        Should.Throw<InvalidOperationException>(() => order.Cancel());
    }

    // --- Full Path: Happy Path ---

    [Fact]
    public void FullLifecycle_DraftToDelivered_AllTransitionsSucceed()
    {
        var order = new Order();
        order.State.ShouldBe(OrderState.Draft);

        order.Place();
        order.State.ShouldBe(OrderState.Pending);

        order.Pay();
        order.State.ShouldBe(OrderState.Paid);

        order.Ship();
        order.State.ShouldBe(OrderState.Shipped);

        order.Deliver();
        order.State.ShouldBe(OrderState.Delivered);
    }
}
```

#### Рівні покриття для тестування переходів станів

| Level | What it covers | Minimum tests |
|---|---|---|
| **All states** | Every state is visited at least once | Few tests |
| **All transitions (0-switch)** | Every valid transition is exercised | One test per transition |
| **All transition pairs (1-switch)** | Every pair of consecutive transitions | More tests |
| **Invalid transitions** | Every event from every state where it is not allowed | Many negative tests |

### 2.5 Попарне (комбінаторне) тестування

#### Проблема

When a system has multiple input parameters, testing all combinations grows exponentially:

| Parameters | Values each | All combinations |
|---|---|---|
| 3 | 3 | 27 |
| 4 | 3 | 81 |
| 5 | 4 | 1,024 |
| 10 | 3 | 59,049 |

#### Ключове спостереження

Most defects are triggered by the interaction of **two** parameters (pairwise), not three or more. Studies by Kuhn, Wallace, and Gallo (NIST) found that:
- 70% of defects involve a single parameter
- 90% of defects are triggered by pairwise interactions
- 98% of defects are triggered by interactions of three or fewer parameters

#### Як працює попарне тестування

Instead of testing all combinations, pairwise testing generates a **minimal set of test cases** that covers every pair of parameter values at least once.

**Example:** A search function has three parameters:
- **Category:** Books, Electronics, Clothing
- **Sort By:** Price, Rating, Date
- **In Stock:** Yes, No

All combinations = 3 x 3 x 2 = 18 test cases.

Pairwise set (covers all pairs):

| Test | Category | Sort By | In Stock |
|---|---|---|---|
| 1 | Books | Price | Yes |
| 2 | Books | Rating | No |
| 3 | Books | Date | Yes |
| 4 | Electronics | Price | No |
| 5 | Electronics | Rating | Yes |
| 6 | Electronics | Date | No |
| 7 | Clothing | Price | Yes |
| 8 | Clothing | Rating | No |
| 9 | Clothing | Date | Yes |

**9 test cases** instead of 18 — and every pair of values appears at least once. For larger parameter spaces, the reduction is dramatic.

#### Інструменти для генерації попарних тестів

- **PICT** (Microsoft, open-source): command-line tool for pairwise generation
- **AllPairs** by James Bach
- Online generators (e.g., pairwise.org)

```
# PICT model file (search.pict)
Category: Books, Electronics, Clothing
SortBy:   Price, Rating, Date
InStock:  Yes, No

# Run: pict search.pict
# Output: minimal pairwise test set
```

```csharp
// Generated pairwise tests
public class SearchPairwiseTests
{
    [Theory]
    [InlineData("Books",       "Price",  true)]
    [InlineData("Books",       "Rating", false)]
    [InlineData("Books",       "Date",   true)]
    [InlineData("Electronics", "Price",  false)]
    [InlineData("Electronics", "Rating", true)]
    [InlineData("Electronics", "Date",   false)]
    [InlineData("Clothing",    "Price",  true)]
    [InlineData("Clothing",    "Rating", false)]
    [InlineData("Clothing",    "Date",   true)]
    public void Search_PairwiseCombination_ReturnsResults(
        string category, string sortBy, bool inStock)
    {
        // Arrange
        var searchService = new SearchService();

        // Act
        var results = searchService.Search(category, sortBy, inStock);

        // Assert
        results.ShouldNotBeNull();
    }
}
```

> **Discussion (10 min):** For a web form with 5 dropdown fields, each with 4 options, the total combinations are 4^5 = 1,024. How many pairwise test cases do you think would be needed? (Answer: typically around 16-20.)

---

## 3. Техніки проєктування тестів методом білої скриньки

Техніки білої скриньки використовують знання внутрішньої структури коду для проєктування тестів. Мета — забезпечити, щоб конкретні структурні елементи були задіяні під час тестування.

### 3.1 Граф потоку управління (CFG)

Before discussing coverage criteria, we need to understand control flow graphs — visual representations of all paths through a function.

```csharp
public string ClassifyTriangle(int a, int b, int c)  // Node 1: Entry
{
    if (a <= 0 || b <= 0 || c <= 0)                   // Node 2: Condition
        return "Invalid";                              // Node 3

    if (a + b <= c || a + c <= b || b + c <= a)        // Node 4: Condition
        return "Not a triangle";                       // Node 5

    if (a == b && b == c)                              // Node 6: Condition
        return "Equilateral";                          // Node 7

    if (a == b || b == c || a == c)                    // Node 8: Condition
        return "Isosceles";                            // Node 9

    return "Scalene";                                  // Node 10
}                                                      // Node 11: Exit
```

```
Control Flow Graph:

        [1: Entry]
            │
            ▼
     ┌──[2: a<=0||b<=0||c<=0]──┐
     │ true                    │ false
     ▼                         ▼
  [3: "Invalid"]      [4: not-a-triangle?]──┐
     │                │ false               │ true
     │                ▼                     ▼
     │        [6: a==b && b==c]──┐    [5: "Not a triangle"]
     │        │ false            │ true     │
     │        ▼                  ▼          │
     │   [8: a==b||b==c||a==c] [7: "Equilateral"]
     │    │ false    │ true      │          │
     │    ▼          ▼           │          │
     │ [10:"Scalene"] [9:"Isosceles"]      │
     │    │          │           │          │
     │    └────┬─────┘───────┬──┘──────────┘
     │         │             │
     └─────────┴─────────────┘
                    │
                    ▼
              [11: Exit]
```

### 3.2 Покриття операторів

#### Визначення

**Statement coverage** measures the percentage of executable statements that are exercised by the test suite.

```
                    Statements executed by tests
Statement Coverage = ─────────────────────────── x 100%
                     Total executable statements
```

#### Приклад

```csharp
public decimal CalculateDiscount(decimal price, bool isMember, int quantity)
{
    decimal discount = 0;                    // Statement 1

    if (isMember)                            // Statement 2 (branch)
    {
        discount = 0.10m;                    // Statement 3
    }

    if (quantity > 10)                       // Statement 4 (branch)
    {
        discount += 0.05m;                   // Statement 5
    }

    return price * (1 - discount);           // Statement 6
}
```

**One test case for 100% statement coverage:**

```csharp
[Fact]
public void CalculateDiscount_MemberWithBulk_AppliesBothDiscounts()
{
    // isMember = true, quantity = 20
    // Executes: S1, S2(true), S3, S4(true), S5, S6
    var result = _sut.CalculateDiscount(100m, true, 20);
    result.ShouldBe(85m); // 100 * (1 - 0.15)
}
```

This single test executes all 6 statements — 100% statement coverage. But it misses the case where `isMember` is false, or where `quantity <= 10`. **Statement coverage is the weakest structural criterion.**

### 3.3 Branch (Decision) Coverage

#### Визначення

**Branch coverage** measures the percentage of branches (decision outcomes) that are exercised. Every `if`, `else`, `switch case`, loop entry/exit counts as a decision.

```
                 Branches executed by tests
Branch Coverage = ────────────────────────── x 100%
                   Total branches
```

#### Приклад (same code)

The code has 4 branches:
- `if (isMember)` — true branch and false branch
- `if (quantity > 10)` — true branch and false branch

**Minimum tests for 100% branch coverage:**

```csharp
[Fact]
public void CalculateDiscount_MemberWithBulk_AppliesBothDiscounts()
{
    // isMember=true (T), quantity=20 (T)
    // Covers: Branch 1 True, Branch 2 True
    var result = _sut.CalculateDiscount(100m, true, 20);
    result.ShouldBe(85m);
}

[Fact]
public void CalculateDiscount_NonMemberSmallOrder_NoDiscount()
{
    // isMember=false (F), quantity=5 (F)
    // Covers: Branch 1 False, Branch 2 False
    var result = _sut.CalculateDiscount(100m, false, 5);
    result.ShouldBe(100m);
}
```

Two test cases achieve 100% branch coverage. Note that 100% branch coverage **implies** 100% statement coverage, but not vice versa.

```
Coverage Hierarchy:

Path Coverage (strongest)
    │
    ├── implies ──► MC/DC Coverage
    │                   │
    │                   ├── implies ──► Condition Coverage
    │                   │
    │                   └── implies ──► Branch Coverage
    │                                       │
    │                                       └── implies ──► Statement Coverage (weakest)
    │
    └── (Path coverage implies all of the above)
```

### 3.4 Condition Coverage

#### Визначення

**Condition coverage** measures whether each individual boolean sub-expression (atomic condition) in a decision has been evaluated to both `true` and `false`.

#### Приклад

```csharp
if (age >= 18 && hasConsent)    // Two atomic conditions: (age >= 18), (hasConsent)
{
    AllowAccess();
}
```

For 100% condition coverage, each atomic condition must be both true and false:

| Test | age >= 18 | hasConsent | Decision |
|---|---|---|---|
| 1 | true (age=20) | false | false |
| 2 | false (age=16) | true | false |

Both atomic conditions have been true and false, so condition coverage = 100%. But notice: the decision was **never true** — `AllowAccess()` was never called. This shows that **condition coverage does not imply branch coverage**.

To address this, we often use **condition/decision coverage** — requiring both condition coverage and branch/decision coverage.

### 3.5 MC/DC (Modified Condition/Decision Coverage)

#### Визначення

MC/DC requires that:
1. Every entry and exit point is invoked
2. Every decision takes every possible outcome (branch coverage)
3. Every condition in a decision takes every possible outcome (condition coverage)
4. Each condition is shown to **independently affect** the decision outcome

Point 4 is the key: for each condition, there must be two test cases where that condition changes while all other conditions remain the same, and the decision outcome changes.

#### Why MC/DC Matters

MC/DC is required by **DO-178C** (avionics software safety standard) for Level A (catastrophic failure consequences). It provides strong confidence that each condition genuinely contributes to the logic.

#### Приклад

```csharp
if (engineRunning && fuelAboveMinimum && noWarningLights)
{
    AllowTakeoff();
}
```

Three conditions: A = `engineRunning`, B = `fuelAboveMinimum`, C = `noWarningLights`.

For MC/DC, we need to show each condition independently affects the outcome:

| Test | A | B | C | Decision | Demonstrates |
|---|---|---|---|---|---|
| 1 | T | T | T | T | (baseline - all true) |
| 2 | **F** | T | T | **F** | A independently affects decision |
| 3 | T | **F** | T | **F** | B independently affects decision |
| 4 | T | T | **F** | **F** | C independently affects decision |

Only **4 test cases** for 3 conditions (N+1 in the best case), compared to 8 for exhaustive testing of all combinations. For decisions with many conditions, this is a significant reduction.

### 3.6 Path Coverage

#### Визначення

**Path coverage** requires that every possible path through the function is exercised. A path is a unique sequence of statements from entry to exit.

#### Приклад

```csharp
public decimal CalculateDiscount(decimal price, bool isMember, int quantity)
{
    decimal discount = 0;

    if (isMember)           // Decision 1
        discount = 0.10m;

    if (quantity > 10)      // Decision 2
        discount += 0.05m;

    return price * (1 - discount);
}
```

**Paths through the code:**

| Path | Decision 1 | Decision 2 | Discount |
|---|---|---|---|
| Path 1 | false | false | 0% |
| Path 2 | false | true | 5% |
| Path 3 | true | false | 10% |
| Path 4 | true | true | 15% |

```csharp
[Theory]
[InlineData(false, 5,  100)]     // Path 1: no discounts
[InlineData(false, 20, 95)]      // Path 2: bulk only
[InlineData(true,  5,  90)]      // Path 3: member only
[InlineData(true,  20, 85)]      // Path 4: both discounts
public void CalculateDiscount_AllPaths_ReturnsExpectedAmount(
    bool isMember, int quantity, decimal expected)
{
    _sut.CalculateDiscount(100m, isMember, quantity).ShouldBe(expected);
}
```

#### Проблема with Path Coverage

For code with loops, path coverage can be **infinite**:

```csharp
while (hasMoreItems)   // Loop can execute 0, 1, 2, ... N times
{
    ProcessItem();     // Each iteration count is a different path
}
```

This makes 100% path coverage impractical for most real-world code. It is mainly useful for critical, loop-free functions.

### 3.7 Comparison of White-Box Coverage Criteria

| Criterion | Strength | Required Tests | Practical? | Used In |
|---|---|---|---|---|
| Statement | Weakest | Fewest | Yes | Minimum standard |
| Branch | Moderate | More | Yes | Industry standard |
| Condition | Moderate | Moderate | Yes | With branch coverage |
| MC/DC | Strong | N+1 per decision | Yes | Avionics (DO-178C) |
| Path | Strongest | Can be infinite | Limited | Critical functions |

> **Discussion (10 min):** A safety-critical medical device has a function with 5 boolean conditions in one decision. How many test cases would exhaustive testing need? (32) How many for MC/DC? (6) Why does this matter in regulated industries?

---

## 4. Code Coverage in Practice

### 4.1 What Code Coverage Measures

Code coverage is a **metric** that indicates how much of your code is exercised by your test suite. Common metrics include:

| Metric | Measures | Typical tool output |
|---|---|---|
| Line coverage | Lines of code executed | 85% of lines executed |
| Branch coverage | Decision outcomes taken | 72% of branches executed |
| Method coverage | Methods entered | 95% of methods called |

### 4.2 What Code Coverage Does NOT Measure

Coverage is a **necessary but insufficient** indicator of test quality. High coverage does not mean good tests.

```csharp
// This test achieves 100% line coverage of the Add method:
[Fact]
public void Add_Executes_WithoutError()
{
    var result = _sut.Add(2, 3);
    // No assertion! The test "covers" the code but verifies nothing.
}
```

**Coverage does not tell you:**
- Whether your assertions are correct or meaningful
- Whether you are testing the right thing
- Whether important edge cases are covered
- Whether the specification is correct
- Whether non-functional requirements (performance, security) are met

```
                        What coverage tells you:
                    ┌─────────────────────────────────┐
                    │  "These lines were executed      │
                    │   during testing."               │
                    └─────────────────────────────────┘

                    What coverage does NOT tell you:
                    ┌─────────────────────────────────┐
                    │  "These lines are correct."      │
                    │  "All important behaviors         │
                    │   are verified."                  │
                    │  "The software is bug-free."      │
                    └─────────────────────────────────┘
```

> **Important:** Low coverage is a reliable signal — it tells you that untested code exists. But high coverage is an unreliable signal — it does not guarantee that the code is well-tested.

### 4.3 Coverlet: Collecting Coverage in .NET

**Coverlet** is the standard code coverage tool for .NET. It instruments your assemblies and records which lines and branches are executed during testing.

#### Setup

```bash
# Add the Coverlet collector to your test project
cd YourSolution.Tests
dotnet add package coverlet.collector
```

This adds the NuGet package that integrates with `dotnet test` via a data collector.

#### Collecting Coverage

```bash
# Run tests with coverage collection (outputs Cobertura XML)
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

This produces a file at:
```
./coverage/<guid>/coverage.cobertura.xml
```

#### Coverage Output Formats

Coverlet supports multiple output formats:

| Format | Use Case |
|---|---|
| `cobertura` | Default; widely supported by CI tools and report generators |
| `opencover` | Alternative XML format; compatible with many tools |
| `lcov` | Used by some IDE extensions and web-based tools |
| `json` | Programmatic access to coverage data |

To specify a format explicitly:

```bash
dotnet test --collect:"XPlat Code Coverage" \
  --results-directory ./coverage \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

#### Excluding Code from Coverage

Not all code needs coverage measurement. You can exclude generated code, configuration, and boilerplate:

```bash
# Exclude specific namespaces or classes via runsettings
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*]*.Migrations.*"
```

Or use the `[ExcludeFromCodeCoverage]` attribute in C#:

```csharp
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(string[] args) { /* ... */ }
}
```

### 4.4 ReportGenerator: Visualizing Coverage

Raw XML coverage files are not human-readable. **ReportGenerator** converts them into visual HTML reports.

#### Setup

```bash
# Install ReportGenerator as a global .NET tool (one-time)
dotnet tool install --global dotnet-reportgenerator-globaltool
```

#### Generating Reports

```bash
# Generate an HTML report from Cobertura XML
reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:"Html;TextSummary"

# View the text summary in the terminal
cat ./coverage/report/Summary.txt

# Open the full HTML report in a browser
open ./coverage/report/index.html    # macOS
# xdg-open ./coverage/report/index.html  # Linux
# start ./coverage/report/index.html     # Windows
```

#### Full Workflow: Test, Collect, Report

Here is the complete sequence from running tests to viewing the report:

```bash
# Step 1: Clean previous results
rm -rf ./coverage

# Step 2: Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Step 3: Generate HTML report
reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:"Html;TextSummary"

# Step 4: View summary
cat ./coverage/report/Summary.txt

# Step 5: Open detailed report
open ./coverage/report/index.html
```

### 4.5 Interpreting Coverage Reports

A typical ReportGenerator HTML report shows:

```
Coverage Summary
═══════════════════════════════════════════════════
Assembly          Line Coverage    Branch Coverage
───────────────────────────────────────────────────
MyApp.Core        87.3%           72.1%
MyApp.Services    92.5%           85.4%
MyApp.Api         45.2%           30.0%
───────────────────────────────────────────────────
Total             78.6%           65.8%
═══════════════════════════════════════════════════
```

The HTML report provides file-by-file drill-down with color-coded source lines:

```
Line highlighting in the report:

  ██  Green   = line was executed by tests
  ██  Red     = line was NOT executed by tests
  ██  Yellow  = line was partially covered (some branches taken, others not)
```

**What to look for:**

1. **Red lines in critical code** — business logic, validation, error handling that is never tested
2. **Yellow lines** — decisions where only one branch is taken (e.g., `if` block tested but `else` never executed)
3. **Overall trends** — is coverage improving over time or declining?
4. **Coverage by module** — are some areas significantly under-tested?

### 4.6 Coverage Thresholds and CI Integration

#### Setting Thresholds

You can enforce minimum coverage thresholds to prevent coverage from dropping:

```bash
# Fail the build if line coverage drops below 80%
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Threshold=80
```

#### Recommended Thresholds

| Context | Line Coverage | Branch Coverage |
|---|---|---|
| New project (starting out) | 60% | 40% |
| Established project | 80% | 60% |
| Critical business logic | 90%+ | 80%+ |
| Safety-critical systems | 100% statement + MC/DC | Required by standard |

**Important:** Treat thresholds as a **floor, not a ceiling**. The goal is not to hit 80% — the goal is to write good tests that happen to achieve high coverage.

#### GitHub Actions Integration

```yaml
# .github/workflows/test.yml
name: Test with Coverage

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Test with Coverage
        run: |
          dotnet test --no-build \
            --collect:"XPlat Code Coverage" \
            --results-directory ./coverage

      - name: Generate Report
        run: |
          dotnet tool install --global dotnet-reportgenerator-globaltool
          reportgenerator \
            -reports:./coverage/**/coverage.cobertura.xml \
            -targetdir:./coverage/report \
            -reporttypes:"Html;TextSummary;Cobertura"

      - name: Print Summary
        run: cat ./coverage/report/Summary.txt

      - name: Upload Coverage Report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./coverage/report/
```

> **Discussion (5 min):** Your team achieves 95% line coverage but keeps finding bugs in production. What might be wrong? How would you improve the testing strategy beyond just increasing the coverage number?

---

## 5. Relationship Between Test Design Techniques and Coverage

### 5.1 How They Work Together

Black-box and white-box techniques are **complementary**, not competing:

```
Specification                              Code
     │                                      │
     ▼                                      ▼
Black-Box Techniques                  White-Box Techniques
     │                                      │
     ▼                                      ▼
Test cases derived from               Test cases derived from
WHAT the system should do             HOW the code is structured
     │                                      │
     └──────────── Combined ───────────────┘
                      │
                      ▼
              Comprehensive test suite
              with measurable coverage
```

#### A Practical Workflow

1. **Start with black-box techniques** — design tests from the specification using EP, BVA, decision tables, etc.
2. **Measure coverage** — run the black-box tests and check code coverage
3. **Identify gaps** — look for untested code (red/yellow lines in the report)
4. **Add white-box tests** — write targeted tests to cover the gaps
5. **Review** — ensure the new tests actually verify behavior (not just execute code)

### 5.2 Example: The Gap Between Specification and Code

Consider a function specified as: "Return the larger of two numbers."

```csharp
public int Max(int a, int b)
{
    if (a >= b)
        return a;
    else
        return b;
}
```

**Black-box tests** (from EP/BVA):

```csharp
[Theory]
[InlineData(5, 3, 5)]    // a > b
[InlineData(3, 5, 5)]    // a < b
[InlineData(4, 4, 4)]    // a == b
public void Max_TwoNumbers_ReturnsLarger(int a, int b, int expected)
{
    _sut.Max(a, b).ShouldBe(expected);
}
```

These three tests achieve 100% branch coverage of the `Max` method. But suppose the developer accidentally implemented:

```csharp
public int Max(int a, int b)
{
    if (a >= b)
        return a;
    else
        return a;  // BUG: should return b
}
```

The test `Max(3, 5, 5)` would catch this bug — the **assertion** is what catches it, not the coverage number. This reinforces: **coverage tells you what was executed, assertions tell you what was verified.**

### 5.3 Mutation Testing — A Brief Mention

**Mutation testing** evaluates the quality of your test suite by introducing small changes (mutations) to the code and checking whether your tests detect them.

```
Original code:       if (a >= b) return a;
Mutant 1:            if (a >  b) return a;   // Changed >= to >
Mutant 2:            if (a >= b) return b;   // Changed return value
Mutant 3:            if (a <= b) return a;   // Changed >= to <=
```

- If a test fails → the mutant is **killed** (good: your tests detect the change)
- If all tests pass → the mutant **survived** (bad: your tests missed it)

The **mutation score** = killed mutants / total mutants. A high mutation score indicates that your tests are sensitive to code changes. Tools like **Stryker.NET** can automate this.

---

## 6. When to Use Which Technique

### 6.1 Decision Guide

```
                          What information do you have?
                                    │
                    ┌───────────────┴───────────────┐
                    │                               │
              Specification                    Source Code
              (requirements)                   (implementation)
                    │                               │
                    ▼                               ▼
            Black-Box Techniques           White-Box Techniques
                    │                               │
        ┌───────────┼───────────┐         ┌─────────┼──────────┐
        │           │           │         │         │          │
   Ranges/       Complex     States/     Coverage  Critical   Regulated
   boundaries    rules       lifecycle   gaps      logic      industry
        │           │           │         │         │          │
        ▼           ▼           ▼         ▼         ▼          ▼
       EP +      Decision    State      Branch   Path       MC/DC
       BVA       Tables      Trans.     Coverage Coverage   Coverage
```

### 6.2 Technique Selection Matrix

| Situation | Recommended Technique(s) |
|---|---|
| Numeric input ranges (age, price, quantity) | EP + BVA |
| Complex business rules with multiple conditions | Decision table |
| Workflow with defined states (order, account) | State transition |
| Multiple configuration parameters | Pairwise testing |
| Ensuring all code is exercised | Statement/branch coverage |
| Safety-critical code | MC/DC + path coverage |
| Verifying test quality after writing tests | Coverage measurement + mutation testing |
| API with many parameter combinations | Pairwise + EP for each parameter |

### 6.3 Combining Techniques — A Realistic Example

Consider a `ShippingCalculator` that determines shipping cost based on:
- Weight (numeric range)
- Destination (domestic/international)
- Shipping speed (standard/express/overnight)
- Member status (member/non-member)

**Strategy:**

1. **EP** on weight: underweight (invalid), light (0-1kg), medium (1-10kg), heavy (10-30kg), overweight (>30kg, invalid)
2. **BVA** on weight boundaries: 0, 0.01, 1, 1.01, 10, 10.01, 30, 30.01
3. **Decision table** for the combination of destination, speed, and member status (2 x 3 x 2 = 12 rules)
4. **Pairwise** if the number of parameters grows beyond what decision tables can handle
5. **Coverage measurement** to identify any code paths missed by the above

```csharp
public class ShippingCalculator
{
    public decimal Calculate(
        decimal weightKg,
        bool isDomestic,
        string speed,
        bool isMember)
    {
        if (weightKg <= 0)
            throw new ArgumentException("Weight must be positive.");
        if (weightKg > 30)
            throw new ArgumentException("Maximum weight is 30 kg.");

        decimal baseCost = weightKg switch
        {
            <= 1  => 5.00m,
            <= 10 => 10.00m,
            _     => 20.00m,
        };

        decimal locationMultiplier = isDomestic ? 1.0m : 2.5m;

        decimal speedMultiplier = speed switch
        {
            "standard"  => 1.0m,
            "express"   => 1.5m,
            "overnight" => 3.0m,
            _ => throw new ArgumentException($"Unknown speed: {speed}"),
        };

        decimal total = baseCost * locationMultiplier * speedMultiplier;

        if (isMember)
            total *= 0.9m; // 10% member discount

        return total;
    }
}
```

```csharp
public class ShippingCalculatorTests
{
    private readonly ShippingCalculator _sut = new();

    // --- EP: Weight categories ---

    [Theory]
    [InlineData(0.5, true,  "standard", false, 5.00)]     // light
    [InlineData(5.0, true,  "standard", false, 10.00)]    // medium
    [InlineData(20,  true,  "standard", false, 20.00)]    // heavy
    public void Calculate_WeightCategories_ReturnsExpectedBase(
        decimal weight, bool domestic, string speed, bool member,
        decimal expected)
    {
        _sut.Calculate(weight, domestic, speed, member).ShouldBe(expected);
    }

    // --- BVA: Weight boundaries ---

    [Theory]
    [InlineData(1.00,  5.00)]     // upper boundary of light
    [InlineData(1.01, 10.00)]     // lower boundary of medium
    [InlineData(10.00, 10.00)]    // upper boundary of medium
    [InlineData(10.01, 20.00)]    // lower boundary of heavy
    public void Calculate_WeightBoundaries_ReturnsExpectedBase(
        decimal weight, decimal expectedBase)
    {
        _sut.Calculate(weight, true, "standard", false).ShouldBe(expectedBase);
    }

    // --- BVA: Invalid weight ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(30.01)]
    public void Calculate_InvalidWeight_ThrowsException(decimal weight)
    {
        Should.Throw<ArgumentException>(
            () => _sut.Calculate(weight, true, "standard", false));
    }

    // --- Decision table: destination x speed x member ---

    [Theory]
    [InlineData(true,  "standard", false, 10.00)]
    [InlineData(true,  "express",  false, 15.00)]
    [InlineData(true,  "overnight",false, 30.00)]
    [InlineData(true,  "standard", true,  9.00)]
    [InlineData(false, "standard", false, 25.00)]
    [InlineData(false, "express",  true,  33.75)]
    public void Calculate_DestinationSpeedMember_ReturnsExpected(
        bool domestic, string speed, bool member, decimal expected)
    {
        // Using 5kg (medium) as base = 10.00
        _sut.Calculate(5.0m, domestic, speed, member).ShouldBe(expected);
    }
}
```

---

## 7. Practical Exercise: Applying All Techniques

### Task: Test a `GradeCalculator`

Given the following specification and implementation, apply the test design techniques from this lecture to create a comprehensive test suite.

**Specification:**

A university grading system converts numeric scores to letter grades:
- 90-100: A
- 80-89: B
- 70-79: C
- 60-69: D
- 0-59: F
- Scores outside 0-100: Invalid (throw exception)

Additional rules:
- If the student has perfect attendance (`hasAttendance = true`) and their score is within 2 points of the next grade boundary, they get bumped up
- If the student is a repeating student (`isRepeating = true`), they need 5 extra points per grade level (e.g., A requires 95 instead of 90)

```csharp
// GradeCalculator.cs
namespace Grading;

public class GradeCalculator
{
    public string CalculateGrade(int score, bool hasAttendance, bool isRepeating)
    {
        if (score < 0 || score > 100)
            throw new ArgumentOutOfRangeException(
                nameof(score), "Score must be between 0 and 100.");

        // Apply attendance bonus: bump up if within 2 points of next boundary
        int effectiveScore = score;
        if (hasAttendance && !isRepeating)
        {
            int[] boundaries = [60, 70, 80, 90];
            foreach (var boundary in boundaries)
            {
                if (score >= boundary - 2 && score < boundary)
                {
                    effectiveScore = boundary;
                    break;
                }
            }
        }

        // Adjust thresholds for repeating students
        int aThreshold = isRepeating ? 95 : 90;
        int bThreshold = isRepeating ? 85 : 80;
        int cThreshold = isRepeating ? 75 : 70;
        int dThreshold = isRepeating ? 65 : 60;

        return effectiveScore switch
        {
            >= var t when effectiveScore >= aThreshold => "A",
            >= var t when effectiveScore >= bThreshold => "B",
            >= var t when effectiveScore >= cThreshold => "C",
            >= var t when effectiveScore >= dThreshold => "D",
            _ => "F",
        };
    }
}
```

**Your task:**

1. **Draw the equivalence partitions** for the score input
2. **Identify boundary values** for each grade boundary
3. **Build a decision table** for the interaction of `hasAttendance` and `isRepeating`
4. **Write xUnit tests** using EP, BVA, and decision table techniques
5. **Measure coverage** with Coverlet and generate a report
6. **Identify any gaps** and add tests to address them

**Skeleton test class to get started:**

```csharp
using Shouldly;

namespace Grading.Tests;

public class GradeCalculatorTests
{
    private readonly GradeCalculator _sut = new();

    // --- Equivalence Partitioning ---
    // TODO: One test per grade partition (A, B, C, D, F, Invalid)

    // --- Boundary Value Analysis ---
    // TODO: Test at each grade boundary (59/60, 69/70, 79/80, 89/90)

    // --- Decision Table: attendance x repeating ---
    // TODO: Test all combinations of hasAttendance and isRepeating
    //       at relevant score values

    // --- Attendance Bonus Edge Cases ---
    // TODO: Score = 58 (within 2 of 60, should bump to D)
    // TODO: Score = 57 (NOT within 2 of 60, stays F)

    // --- Repeating Student Thresholds ---
    // TODO: Score = 93 (A for normal, B for repeating)
}
```

> **Discussion (15 min):** After writing your tests, run coverage. What percentage did you achieve? Are there any lines you chose not to test? Why or why not?

---

## 8. Common Pitfalls and Best Practices

### 8.1 Coverage Pitfalls

| Pitfall | Example | Remedy |
|---|---|---|
| **Chasing the number** | Writing tests just to increase coverage % | Focus on meaningful tests that verify behavior |
| **Tests without assertions** | Code is executed but nothing is verified | Every test must assert something meaningful |
| **Excluding too much** | `[ExcludeFromCodeCoverage]` on business logic | Only exclude truly untestable code (e.g., `Main()`) |
| **Ignoring branch coverage** | 100% line but 50% branch | Track both line and branch coverage |
| **One-time measurement** | Checking coverage only at release | Integrate into CI, track trends over time |

### 8.2 Test Design Pitfalls

| Pitfall | Example | Remedy |
|---|---|---|
| **Only happy path** | Testing `Add(2, 3)` but not `Add(MaxInt, 1)` | Always consider invalid, boundary, and edge cases |
| **Forgetting negative tests** | Only testing valid transitions in state machine | Test that invalid transitions throw exceptions |
| **Ignoring combinations** | Testing each parameter in isolation | Use decision tables or pairwise for interactions |
| **Too many tests per technique** | 50 equivalence partition tests for one partition | One representative per partition is enough |

### 8.3 Coverage Targets by Code Category

Not all code deserves the same level of testing attention:

```
Code Category                 Target Coverage    Test Design Focus
──────────────────────────────────────────────────────────────────
Core business logic           90%+               EP, BVA, decision tables
Data validation               85%+               EP, BVA (especially invalid)
Error handling / edge cases   80%+               Branch coverage + negative tests
API controllers (thin)        70%+               Integration tests (Lecture 3-4)
DTOs / models                 No unit tests      Tested implicitly through usage
Generated code / migrations   Exclude            Not worth testing
Configuration / startup       60%+               Integration tests
```

---

## 9. Summary

### Ключові висновки

1. **Systematic test design** is superior to ad-hoc testing — it provides rationale, repeatability, and measurable completeness
2. **Black-box techniques** derive tests from specifications:
   - **EP** reduces infinite inputs to a manageable number of partitions
   - **BVA** targets the boundaries where bugs cluster
   - **Decision tables** systematically cover combinations of conditions
   - **State transition testing** verifies lifecycle behavior
   - **Pairwise testing** efficiently handles multi-parameter systems
3. **White-box techniques** derive tests from code structure:
   - **Statement coverage** is the minimum (but weakest) criterion
   - **Branch coverage** is the practical industry standard
   - **MC/DC** is required for safety-critical systems
   - **Path coverage** is the strongest but often impractical
4. **Coverlet** collects coverage data during `dotnet test`; **ReportGenerator** turns it into readable HTML reports
5. **Coverage is a floor, not a ceiling** — high coverage does not mean good tests, but low coverage reliably indicates gaps
6. **Combine techniques** — start with black-box tests from the spec, measure coverage, then add white-box tests for gaps
7. **Integrate coverage into CI** to prevent regression and track trends over time

### Анонс наступної лекції

In **Lecture 7: CI/CD and Test Management**, we will:
- Set up GitHub Actions to run tests automatically on every push
- Configure coverage collection and reporting in CI pipelines
- Implement quality gates that block merges when tests fail or coverage drops
- Explore test management practices: test plans, traceability, and reporting
- Discuss flaky test management and test suite maintenance

---

## Посилання та додаткова література

- **ISTQB Foundation Level Syllabus** (v4.0, 2023) — Chapters 4 (Test Design Techniques)
  - https://www.istqb.org/certifications/certified-tester-foundation-level
- **"Software Testing: A Craftsman's Approach"** — Paul C. Jorgensen (CRC Press, 5th edition, 2021) — Chapters 5-9
- **"Introduction to Software Testing"** — Paul Ammann, Jeff Offutt (Cambridge University Press, 2nd edition, 2016)
- **Coverlet Documentation** — https://github.com/coverlet-coverage/coverlet
- **ReportGenerator Documentation** — https://github.com/danielpalme/ReportGenerator
- **NIST Combinatorial Testing** — https://csrc.nist.gov/projects/automated-combinatorial-testing-for-software
- **DO-178C** — Software Considerations in Airborne Systems and Equipment Certification (RTCA, 2011)
- **Pairwise Testing (PICT)** — https://github.com/Microsoft/pict
- **Stryker.NET (Mutation Testing)** — https://stryker-mutator.io/docs/stryker-net/introduction/
