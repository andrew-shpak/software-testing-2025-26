# Lab 13 — Individual Project Defense / Захист індивідуального проекту

## Objective

Present and defend your individual testing project. Demonstrate your ability to apply the testing techniques learned throughout the course to a real-world application.

## Project Requirements

### Application Under Test

Choose or build a small ASP.NET Core Web API application that includes:

- At least **3 API endpoints** (CRUD operations)
- A **database layer** using Entity Framework Core (any provider)
- At least **1 external service dependency** (e.g., email, payment, notification)
- **Input validation** on at least 2 endpoints
- **Business logic** with non-trivial rules (not just pass-through CRUD)

You may use a project from a previous course, a personal project, or build a new one. Example domains:

| Domain | Endpoints | Business Logic |
|---|---|---|
| Task Manager | tasks CRUD, assign user, change status | Status transitions, due date validation |
| E-Commerce | products, orders, cart | Inventory check, discount rules, order totals |
| Library System | books, members, loans | Loan limits, overdue penalties, availability |
| Booking System | rooms, reservations, users | Date conflicts, capacity checks, cancellation policy |

### Required Test Types

Your project must include all of the following:

| Test Type | Minimum Count | Tools |
|---|---|---|
| **Unit tests** | 15 | xUnit v3, Shouldly, NSubstitute |
| **Integration tests** (API) | 8 | WebApplicationFactory |
| **Database tests** | 6 | Testcontainers (SQL Server or PostgreSQL) |
| **Performance tests** | 3 scenarios | k6 |

**Total minimum: 32 tests** (unit + integration + database) + 3 k6 scenarios

### Test Quality Expectations

- Tests follow **AAA pattern** (Arrange-Act-Assert)
- Test names use `MethodName_Scenario_ExpectedBehavior` convention
- **No test interdependencies** — each test runs independently
- Mocking is used appropriately (external services, not internal logic)
- Code coverage **≥ 80%** (measured with Coverlet)

### k6 Performance Scenarios

Include at least 3 k6 scripts:

1. **Smoke test** — 1-2 VUs, 30 seconds, verify baseline
2. **Load test** — 10-50 VUs, 1-2 minutes, verify performance under normal load
3. **Stress test** — ramp up to breaking point

Each script must include:
- `checks` for response status and content
- `thresholds` for response time (e.g., p95 < 500ms)

## Project Structure

```
MyProject/
├── src/
│   └── MyProject.Api/
│       ├── Controllers/
│       ├── Services/
│       ├── Repositories/
│       ├── Models/
│       └── Program.cs
├── tests/
│   └── MyProject.Tests/
│       ├── Unit/
│       ├── Integration/
│       └── Database/
├── k6/
│   ├── smoke-test.js
│   ├── load-test.js
│   └── stress-test.js
└── README.md          ← project description + how to run
```

## Defense Procedure

### Duration: 15-20 minutes per student

### Part 1 — Presentation (5-7 min)

1. Briefly describe your application domain and architecture
2. Show project structure and test organization
3. Present code coverage report
4. Show k6 performance test results

### Part 2 — Live Demo (5-7 min)

1. Run all tests: `dotnet test --verbosity normal`
2. Run one k6 scenario live
3. Show coverage report: `dotnet test --collect:"XPlat Code Coverage"`

### Part 3 — Q&A (5 min)

Expect questions such as:
- "Why did you choose to mock X instead of using a real implementation?"
- "What would happen if you removed this test — what bug could slip through?"
- "How would you test this edge case?"
- "What does this k6 threshold mean and why did you set it to that value?"
- "Why is this test an integration test rather than a unit test?"

## Grading

| Criteria | Weight |
|---|---|
| Unit tests (quality, coverage, NSubstitute usage) | 20% |
| Integration tests (WebApplicationFactory, HTTP assertions) | 20% |
| Database tests (Testcontainers, real DB behavior) | 20% |
| k6 performance tests (scenarios, checks, thresholds) | 15% |
| Code coverage ≥ 80% | 10% |
| Presentation and defense quality | 15% |

## Submission

- Push your project to a **GitHub repository**
- Include a `README.md` with:
  - Project description
  - How to build and run (`dotnet build`, `dotnet run`)
  - How to run tests (`dotnet test`)
  - How to run k6 tests
  - How to generate coverage report
- Submit the repository URL before your defense session

## Timeline

| Week | Activity |
|---|---|
| Lab 13 | Project topic selection + initial setup |
| Lab 14 | Implementation and testing (work session) |
| Lab 15 | Defense presentations |
