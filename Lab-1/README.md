# Lab 1 — Unit Testing: Fundamentals

## Objective

Learn the basics of unit testing in C#: writing test cases with xUnit v3, using assertions, structuring tests with the Arrange-Act-Assert (AAA) pattern, and achieving meaningful code coverage.

## Prerequisites

- .NET 8+ SDK installed
- Basic C# knowledge (classes, methods, generics)
- IDE: Visual Studio / Rider / VS Code with C# extension

## Tools

- Language: C#
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Assertions: `Xunit.Assert` / [Shouldly](https://docs.shouldly.org/)

## Key Concepts

- **Unit test** — a test that verifies a single unit of behavior in isolation
- **AAA pattern** — Arrange (set up), Act (execute), Assert (verify)
- **`[Fact]`** — marks a test method with no parameters
- **`[Theory]`** — marks a data-driven test with `[InlineData]`, `[MemberData]`, or `[ClassData]`
- **Code coverage** — percentage of code executed during tests

## Setup

```bash
dotnet new sln -n Lab1
dotnet new classlib -n Lab1.Core
dotnet new classlib -n Lab1.Tests
dotnet sln add Lab1.Core Lab1.Tests
dotnet add Lab1.Tests reference Lab1.Core
dotnet add Lab1.Tests package xunit.v3
dotnet add Lab1.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab1.Tests package Shouldly
```

Run tests:

```bash
dotnet test --verbosity normal
dotnet test --collect:"XPlat Code Coverage"
```

## Tasks

### Task 1 — Calculator

Create a `Calculator` class in `Lab1.Core` with methods:

- `double Add(double a, double b)`
- `double Subtract(double a, double b)`
- `double Multiply(double a, double b)`
- `double Divide(double a, double b)` — throws `DivideByZeroException` when `b == 0`

Write tests in `CalculatorTests.cs` covering:

1. Normal cases for each operation
2. Negative numbers
3. Floating-point precision (`0.1 + 0.2`)
4. Division by zero throws `DivideByZeroException`

**Example test structure:**

```csharp
public class CalculatorTests
{
    private readonly Calculator _sut = new();

    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        double a = 2, b = 3;

        // Act
        var result = _sut.Add(a, b);

        // Assert
        result.ShouldBe(5);
    }

    [Theory]
    [InlineData(10, 5, 2)]
    [InlineData(-10, 2, -5)]
    [InlineData(0, 1, 0)]
    public void Divide_ValidInputs_ReturnsQuotient(double a, double b, double expected)
    {
        _sut.Divide(a, b).ShouldBe(expected);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        Should.Throw<DivideByZeroException>(() => _sut.Divide(10, 0));
    }
}
```

**Minimum test count:** 12 tests (3 per operation)

### Task 2 — String Utilities

Create a `StringUtils` static class with methods:

- `string Capitalize(string input)` — capitalizes first letter of each word
- `string Reverse(string input)` — reverses the string
- `bool IsPalindrome(string input)` — case-insensitive palindrome check
- `string Truncate(string input, int maxLength)` — truncates and appends `"..."` if needed

Write tests covering:

1. Normal inputs
2. `null` and empty strings (should throw `ArgumentNullException` or return empty)
3. Single character strings
4. Unicode and whitespace handling

**Expected behaviors:**

| Method | Input | Expected Output |
|--------|-------|-----------------|
| `Capitalize` | `"hello world"` | `"Hello World"` |
| `Capitalize` | `"HELLO"` | `"Hello"` |
| `Reverse` | `"abcd"` | `"dcba"` |
| `IsPalindrome` | `"Racecar"` | `true` |
| `IsPalindrome` | `"Hello"` | `false` |
| `Truncate` | `"Hello World", 5` | `"Hello..."` |
| `Truncate` | `"Hi", 10` | `"Hi"` |

**Hint:** Use `[Theory]` with `[InlineData]` for table-driven tests.

**Minimum test count:** 16 tests (4 per method)

### Task 3 — Collection Processor

Create a `CollectionProcessor<T>` class with methods:

- `IEnumerable<T> RemoveDuplicates(IEnumerable<T> items)`
- `IEnumerable<IEnumerable<T>> Chunk(IEnumerable<T> items, int size)`
- `IDictionary<TKey, List<T>> GroupBy<TKey>(IEnumerable<T> items, Func<T, TKey> keySelector)`

Write tests covering:

1. Normal cases with various types (`int`, `string`)
2. Empty collections
3. Single element collections
4. Invalid inputs (`null` collection, chunk size ≤ 0) throw `ArgumentException`

**Expected behaviors:**

| Method | Input | Expected Output |
|--------|-------|-----------------|
| `RemoveDuplicates` | `[1, 2, 2, 3, 3, 3]` | `[1, 2, 3]` |
| `Chunk` | `[1, 2, 3, 4, 5], 2` | `[[1,2], [3,4], [5]]` |
| `GroupBy` | `[{Name:"A", Age:20}, {Name:"B", Age:20}]`, key=Age | `{20: [A, B]}` |

**Hint:** Use `[MemberData]` for complex test data that cannot be expressed as `[InlineData]`.

**Minimum test count:** 12 tests (4 per method)

## Grading

| Criteria |
|----------|
| Task 1 — Calculator tests |
| Task 2 — String utility tests |
| Task 3 — Collection processor tests |
| Test quality (AAA pattern, descriptive names, `[Theory]` usage) |
| Code coverage ≥ 90% |

## Submission

- Solution with `Lab1.Core` and `Lab1.Tests` projects
- Run `dotnet test --collect:"XPlat Code Coverage"` and include coverage report
- Minimum 40 total tests across all tasks

## References

- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline)
- [Shouldly Documentation](https://docs.shouldly.org/)
- [Unit Testing Best Practices — Microsoft](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
