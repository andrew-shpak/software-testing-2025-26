# Lecture 1: Introduction to Software Testing

## Learning Objectives

By the end of this lecture, students will be able to:

- Define software testing and explain its role in software development
- Distinguish between errors, defects, and failures
- Explain the fundamental principles of testing
- Describe the psychology of testing and its impact on team dynamics
- Understand the software testing lifecycle
- Write and run a basic xUnit v3 test in C#

---

## 1. Why Software Testing Matters

### 1.1 The Cost of Software Failures

Software defects have real-world consequences. Here are some well-known examples:

| Incident | Year | Impact |
|---|---|---|
| Ariane 5 rocket explosion | 1996 | $370 million loss due to integer overflow |
| Therac-25 radiation machine | 1985-87 | 6 patients received massive overdoses; 3 deaths |
| Knight Capital trading glitch | 2012 | $440 million lost in 45 minutes |
| Crowdstrike update | 2024 | ~8.5 million Windows machines crashed worldwide |
| Toyota unintended acceleration | 2009-11 | 89 deaths attributed to software issues |

> **Discussion (5 min):** Can you think of a software bug that affected you personally? What was the impact?

### 1.2 The Cost of Defects Over Time

The cost of finding and fixing a defect increases dramatically the later it is discovered:

```
Cost multiplier (relative to requirements phase):

Requirements:   █  (1x)
Design:         ███  (3-6x)
Coding:         ██████  (10x)
Testing:        ████████████  (15-40x)
Production:     ████████████████████████████████  (30-100x)
```

This is known as the **Cost of Quality** principle — investing in early testing and prevention is far cheaper than fixing defects after release.

### 1.3 Testing vs. Not Testing

| Without Testing | With Testing |
|---|---|
| Unpredictable release quality | Measurable quality metrics |
| Customer-discovered bugs | Bugs caught before release |
| Expensive hotfixes | Planned defect resolution |
| Loss of customer trust | Confidence in releases |
| Uncontrolled technical debt | Managed code health |

---

## 2. What is Software Testing?

### 2.1 Definitions

**Software Testing** (ISTQB definition):
> The process consisting of all lifecycle activities, both static and dynamic, concerned with planning, preparation, and evaluation of a software product and related work products to determine that they satisfy specified requirements, to demonstrate that they are fit for purpose, and to detect defects.

In simpler terms, testing is a **systematic activity** to:
1. **Verify** — does the software meet its specification? ("Are we building the product right?")
2. **Validate** — does the software meet the user's actual needs? ("Are we building the right product?")

### 2.2 Testing vs. Debugging

These are often confused but are distinct activities:

| Testing | Debugging |
|---|---|
| Finds failures (symptoms) | Finds and fixes the root cause (defect) |
| Performed by testers (and developers) | Performed by developers |
| Systematic, planned activity | Reactive, investigative activity |
| Can be automated | Mostly manual, analytical process |

**Flow:** Testing reveals a *failure* → Debugging locates the *defect* → Developer applies a *fix* → Testing *confirms* the fix.

### 2.3 Error, Defect, Failure

Understanding the distinction between these three terms is fundamental:

```
Human makes      Defect exists        User encounters
a mistake         in code              a problem
    │                 │                     │
    ▼                 ▼                     ▼
  ERROR ──────►    DEFECT ──────►      FAILURE
 (Mistake)        (Bug/Fault)        (Incorrect behavior)

 "I wrote >       "if (x > 0)"        "App crashes for
  instead          should be           negative input"
  of >="          "if (x >= 0)"
```

- **Error (Mistake):** A human action that produces an incorrect result (e.g., misunderstanding a requirement)
- **Defect (Bug/Fault):** A flaw in the code or documentation that may cause a failure
- **Failure:** The observable incorrect behavior when a defect is executed

> **Note:** Not every defect leads to a failure. A defect in code that is never executed will never cause a failure.

---

## 3. Seven Principles of Software Testing

These principles (from ISTQB syllabus) guide testing practice:

### Principle 1: Testing Shows the Presence of Defects, Not Their Absence

Testing can show that defects are present, but cannot prove that there are no defects. Testing reduces the probability of undiscovered defects, but even if no defects are found, it does not prove correctness.

> *"Program testing can be used to show the presence of bugs, but never to show their absence."*
> — Edsger W. Dijkstra

### Principle 2: Exhaustive Testing is Impossible

Testing everything (all combinations of inputs, preconditions, and paths) is not feasible except for trivial cases.

**Example:** A simple login form with:
- Username: up to 50 characters (printable ASCII = ~95 options per character)
- Password: up to 30 characters

Total combinations: 95^50 × 95^30 ≈ 10^158 — more than the atoms in the observable universe (~10^80).

Instead of exhaustive testing, we use **risk analysis** and **test design techniques** to prioritize.

### Principle 3: Early Testing Saves Time and Money

Testing activities should start as early as possible in the software development lifecycle. Reviewing requirements and designs is also a form of testing (static testing).

### Principle 4: Defects Cluster Together

A small number of modules usually contain most of the defects. This is similar to the Pareto principle (80/20 rule): roughly 80% of defects are found in 20% of modules.

This insight helps focus testing effort on the most defect-prone areas.

### Principle 5: The Pesticide Paradox

If the same tests are repeated over and over, eventually they will no longer find new defects — like pesticide that insects become immune to.

**Countermeasure:** Regularly review and revise test cases. Add new tests for new functionality and changed code.

### Principle 6: Testing is Context-Dependent

Testing is done differently in different contexts. For example:
- Safety-critical software (medical devices, aviation) requires rigorous, formal testing
- An internal tool may need less formal testing
- A mobile game has different quality priorities than a banking application

### Principle 7: Absence-of-Errors Fallacy

Finding and fixing many defects does not help if the system built is unusable or does not fulfill the users' needs. A perfectly bug-free product that nobody wants is still a failure.

> **Discussion (10 min):** For each principle, think of a practical example from your experience. Have you seen any of these principles violated?

---

## 4. Software Development Lifecycle and Testing

### 4.1 Testing in Different SDLC Models

Testing is not a phase — it is an activity that accompanies every development phase.

#### Waterfall Model

```
Requirements ──► Design ──► Implementation ──► Testing ──► Deployment ──► Maintenance
                                                  │
                                          (testing happens late,
                                           defects found late)
```

Testing comes at the end, which means defects are found late and are expensive to fix.

#### V-Model

The V-Model pairs each development phase with a corresponding testing phase:

```
Requirements ─────────────────────────── Acceptance Testing
    │                                           │
    Design ──────────────────────── System Testing
        │                                   │
        Detailed Design ─────── Integration Testing
            │                           │
            Implementation ──── Unit Testing
```

Each level of testing validates the corresponding development phase. This model makes the relationship between development and testing explicit.

#### Agile / Iterative Models

```
┌─────────────────────────────────────────┐
│  Sprint / Iteration                     │
│                                         │
│  Plan → Develop → Test → Review → Demo  │
│    ▲                              │     │
│    └──────────────────────────────┘     │
└─────────────────────────────────────────┘
        Repeat every 1-4 weeks
```

In Agile, testing is **continuous** and happens throughout every iteration:
- Developers write unit tests alongside code (TDD)
- Testers collaborate with developers daily
- Automated regression tests run on every commit
- Exploratory testing for each new feature

### 4.2 Testing Activities in the SDLC

Regardless of the model, testing involves these key activities:

1. **Test Planning** — Define scope, approach, resources, schedule
2. **Test Analysis** — Analyze the test basis (requirements, design) to identify test conditions
3. **Test Design** — Create test cases and test data from test conditions
4. **Test Implementation** — Prepare test environment, create test scripts
5. **Test Execution** — Run tests, compare results, log defects
6. **Test Completion** — Archive test artifacts, analyze lessons learned

---

## 5. Roles in Software Testing

### 5.1 Tester vs. Developer

| Aspect | Developer | Tester |
|---|---|---|
| Primary goal | Build the software | Break the software (find defects) |
| Perspective | Constructive ("How do I make it work?") | Destructive ("How can I make it fail?") |
| Bias | Confirmation bias toward their code | Seeks disconfirmation |
| Testing scope | Unit and component tests | All levels of testing |

### 5.2 The Whole-Team Approach

In modern software development, quality is everyone's responsibility:

- **Developers** write unit tests, perform code reviews
- **Testers/QA Engineers** design test strategies, create test plans, perform exploratory testing
- **Product Owners** define acceptance criteria
- **DevOps** maintains CI/CD pipelines and test environments
- **The whole team** participates in quality discussions

---

## 6. Psychology of Testing

### 6.1 Cognitive Biases in Testing

Testing is a human activity and subject to cognitive biases:

- **Confirmation bias:** Tendency to look for evidence that confirms our beliefs. Developers may unconsciously test only the "happy path"
- **Anchoring:** Being overly influenced by the first piece of information received
- **Automation bias:** Over-trusting automated test results without questioning them

### 6.2 Independence of Testing

Different levels of independence in testing:

```
Low independence                              High independence
      │                                              │
      ▼                                              ▼
 Developer     Developer     Tester from      Tester from     External
 tests own     tests         the same         a different     test
 code          colleague's   team             organization    organization
               code
```

Greater independence helps find more defects (fewer blind spots), but may slow communication. A balance is needed.

### 6.3 Communication and Feedback

How defects are communicated matters:

**Bad:** *"Your code is broken. The login doesn't work."*

**Good:** *"I found an issue in the login module. When entering a valid email with a '+' character (e.g., user+tag@example.com), the validation rejects it. Steps to reproduce: ..."*

Effective defect reports are:
- **Objective** — focus on facts, not blame
- **Specific** — include steps to reproduce
- **Constructive** — suggest impact and priority

> **Discussion (5 min):** Why is it difficult to give and receive feedback about defects? How can teams create a culture where finding bugs is celebrated?

---

## 7. Introduction to Test Automation with xUnit v3

### 7.1 Why Automate Tests?

| Manual Testing | Automated Testing |
|---|---|
| Slow, repetitive | Fast, repeatable |
| Prone to human error | Consistent execution |
| Hard to scale | Easy to scale |
| Good for exploratory testing | Good for regression testing |

Automated tests serve as **living documentation** of expected behavior and provide a **safety net** for refactoring.

### 7.2 Your First xUnit v3 Test

#### Project Setup

```bash
# Create a class library for the code under test
dotnet new classlib -n Calculator
cd Calculator

# Create a test project
cd ..
dotnet new classlib -n Calculator.Tests
cd Calculator.Tests

# Add xUnit v3 and reference the project under test
dotnet add package xunit.v3
dotnet add package Shouldly
dotnet add reference ../Calculator/Calculator.csproj
```

#### Code Under Test

```csharp
// Calculator/MathService.cs
namespace Calculator;

public class MathService
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public double Divide(int dividend, int divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("Cannot divide by zero.");

        return (double)dividend / divisor;
    }

    public bool IsEven(int number) => number % 2 == 0;
}
```

#### Test Class

```csharp
// Calculator.Tests/MathServiceTests.cs
using Shouldly;

namespace Calculator.Tests;

public class MathServiceTests
{
    private readonly MathService _sut = new(); // SUT = System Under Test

    // --- Arrange-Act-Assert Pattern ---

    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsCorrectSum()
    {
        // Arrange
        int a = 3, b = 5;

        // Act
        var result = _sut.Add(a, b);

        // Assert
        result.ShouldBe(8);
    }

    [Fact]
    public void Subtract_LargerFromSmaller_ReturnsNegative()
    {
        // Arrange
        int a = 3, b = 10;

        // Act
        var result = _sut.Subtract(a, b);

        // Assert
        result.ShouldBeNegative();
        result.ShouldBe(-7);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        // Arrange & Act & Assert
        var exception = Should.Throw<DivideByZeroException>(
            () => _sut.Divide(10, 0)
        );

        exception.Message.ShouldContain("Cannot divide by zero");
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(-4, true)]
    [InlineData(-7, false)]
    public void IsEven_GivenNumber_ReturnsExpectedResult(
        int number, bool expected)
    {
        // Act
        var result = _sut.IsEven(number);

        // Assert
        result.ShouldBe(expected);
    }
}
```

### 7.3 Understanding the Test Structure

#### The AAA Pattern

Every well-structured test follows the **Arrange-Act-Assert** pattern:

```
┌──────────────────────────────────┐
│  ARRANGE                         │
│  Set up the test preconditions   │
│  - Create objects                │
│  - Prepare test data             │
│  - Configure dependencies        │
├──────────────────────────────────┤
│  ACT                             │
│  Execute the behavior under test │
│  - Call the method               │
│  - Trigger the action            │
├──────────────────────────────────┤
│  ASSERT                          │
│  Verify the expected outcome     │
│  - Check return values           │
│  - Verify state changes          │
│  - Confirm exceptions thrown     │
└──────────────────────────────────┘
```

#### xUnit v3 Key Concepts

| Concept | Description | Example |
|---|---|---|
| `[Fact]` | A test method that takes no parameters | Single test case |
| `[Theory]` | A parameterized test method | Multiple test cases with different data |
| `[InlineData]` | Provides inline parameters to a `[Theory]` | `[InlineData(2, true)]` |
| `ShouldBe()` | Shouldly assertion for equality | `result.ShouldBe(42)` |
| `Should.Throw<T>()` | Shouldly assertion for exceptions | `Should.Throw<ArgumentException>(...)` |

#### Test Naming Conventions

Test names should describe the **behavior**, not the implementation:

```
MethodName_Scenario_ExpectedBehavior
```

| Good Name | Bad Name |
|---|---|
| `Add_TwoPositiveNumbers_ReturnsCorrectSum` | `TestAdd` |
| `Divide_ByZero_ThrowsException` | `DivideTest1` |
| `IsEven_NegativeEvenNumber_ReturnsTrue` | `Test3` |

### 7.4 Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~MathServiceTests"

# Run a specific test
dotnet test --filter "Add_TwoPositiveNumbers_ReturnsCorrectSum"
```

Example output:
```
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5
```

---

## 8. Static Testing

### 8.1 What is Static Testing?

Static testing examines code and documents **without executing** them. It includes:

- **Reviews** — peer reviews, walkthroughs, inspections of code, requirements, design
- **Static Analysis** — automated tools that analyze source code for potential defects

### 8.2 Types of Reviews

| Type | Formality | Led By | Goal |
|---|---|---|---|
| Informal review | Low | Author/peer | Quick feedback |
| Walkthrough | Low-Medium | Author | Education, understanding |
| Technical review | Medium | Moderator | Find defects, share knowledge |
| Inspection | High | Trained moderator | Find defects systematically |

### 8.3 Static Analysis Tools

Static analysis tools can detect:
- Code style violations
- Potential null reference exceptions
- Unused variables and dead code
- Security vulnerabilities
- Code complexity metrics

In C#, common tools include:
- **Roslyn Analyzers** (built into .NET SDK)
- **SonarQube / SonarCloud**
- **ReSharper / Rider inspections**

```bash
# Enable .NET analyzers in your project file
# <PropertyGroup>
#   <EnableNETAnalyzers>true</EnableNETAnalyzers>
#   <AnalysisLevel>latest</AnalysisLevel>
# </PropertyGroup>
```

---

## 9. Quality Characteristics

### 9.1 ISO 25010 Quality Model

Software quality is multi-dimensional. The ISO 25010 standard defines eight quality characteristics:

```
                    Software Quality
                         │
    ┌────────┬───────┬───┴───┬────────┬──────────┬───────────┬──────────┐
    │        │       │       │        │          │           │          │
Functional  Performance  Compat-  Usability  Reliability  Security  Maintain-  Port-
Suitability Efficiency   ibility                                     ability    ability
```

| Characteristic | Description | Example Test |
|---|---|---|
| **Functional suitability** | Does it do what it should? | Does login accept valid credentials? |
| **Performance efficiency** | How fast/resource-efficient? | Does the page load in < 2 seconds? |
| **Compatibility** | Works with other systems? | Does it work in Chrome, Firefox, Safari? |
| **Usability** | Easy to use? | Can a new user complete checkout in < 3 min? |
| **Reliability** | Works consistently? | Does it handle 10,000 concurrent users? |
| **Security** | Protected from threats? | Is SQL injection prevented? |
| **Maintainability** | Easy to modify? | Can a new developer understand the code? |
| **Portability** | Runs on different platforms? | Does it work on Windows and Linux? |

---

## 10. Summary

### Key Takeaways

1. Testing is essential because software defects have real costs — financial, reputational, and sometimes human
2. Testing shows the presence of defects, not their absence
3. Finding defects early saves time and money
4. Testing involves systematic activities: planning, analysis, design, execution, and completion
5. Both static and dynamic testing contribute to software quality
6. Automated tests provide a safety net and living documentation
7. The AAA pattern (Arrange-Act-Assert) is the standard structure for unit tests

### Preview of Next Lecture

In **Lecture 2: Unit Testing and Mocking**, we will:
- Deep dive into xUnit v3 with `[Fact]` and `[Theory]`
- Master Shouldly assertions for readable test output
- Learn test doubles: stubs, mocks, fakes, and spies
- Use NSubstitute to isolate dependencies in unit tests
- Apply best practices for test naming, organization, and edge cases

---

## References and Further Reading

- **ISTQB Foundation Level Syllabus** (v4.0, 2023) — Chapters 1-2
  - https://www.istqb.org/certifications/certified-tester-foundation-level
- **"Lessons Learned in Software Testing"** — C. Kaner, J. Bach, B. Pettichord (Wiley, 2001)
- **"The Art of Software Testing"** — G. Myers, C. Sandler, T. Badgett (Wiley, 3rd edition, 2011)
- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **ISO/IEC 25010:2023** — Systems and software Quality Requirements and Evaluation (SQuaRE)
