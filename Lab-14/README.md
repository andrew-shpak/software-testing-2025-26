# Lab 14 — Individual Project Defense: Work Session

## Objective

Continue working on your individual testing project. Use this lab session to implement tests, get feedback from the instructor, and prepare for defense in Lab 15.

## Session Structure

### Check-in (first 15 min)

Show the instructor:
1. Your project repository and current progress
2. Which test types you have completed
3. Any blockers or questions

### Work Time (1.5-2 hours)

Focus on completing the remaining requirements from Lab 13:

#### Checklist

- [ ] Application has at least 3 API endpoints with business logic
- [ ] **Unit tests** (≥ 15) with NSubstitute for mocking
- [ ] **Integration tests** (≥ 8) with WebApplicationFactory
- [ ] **Database tests** (≥ 6) with Testcontainers
- [ ] **k6 scripts** (3 scenarios: smoke, load, stress)
- [ ] Code coverage ≥ 80% (run and verify with Coverlet)
- [ ] All tests pass: `dotnet test --verbosity normal`
- [ ] Project `README.md` with build/run/test instructions

### Quick Commands Reference

```bash
# Run all tests
dotnet test --verbosity normal

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate coverage report
reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:"Html;TextSummary"

# View coverage summary
cat ./coverage/report/Summary.txt

# Run k6 smoke test
k6 run k6/smoke-test.js

# Run k6 load test
k6 run k6/load-test.js
```

### Review (last 15 min)

Brief check with the instructor:
- Confirm all requirements are met
- Get feedback on test quality
- Prepare for defense questions

## Common Issues and Solutions

| Issue | Solution |
|---|---|
| Testcontainers fails to start | Ensure Docker is running: `docker info` |
| Low code coverage | Check which classes/methods are uncovered in the HTML report |
| k6 connection refused | Make sure the API is running before executing k6 |
| Tests interfere with each other | Use fresh database per test class, avoid shared mutable state |
| WebApplicationFactory doesn't find Program | Add `public partial class Program { }` to `Program.cs` |

## Reminder

Your defense is in **Lab 15**. Make sure your repository is pushed and the README is complete before the session.
