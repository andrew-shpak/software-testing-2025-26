# Lab 15 — Individual Project Defense: Presentations

## Objective

Present and defend your individual testing project to the instructor.

## Defense Schedule

Each student has **15-20 minutes**:

| Phase | Duration | Activity |
|---|---|---|
| Presentation | 5-7 min | Show architecture, tests, coverage, k6 results |
| Live Demo | 5-7 min | Run tests and k6 live |
| Q&A | 5 min | Answer questions about your testing decisions |

## Before Your Turn

1. Ensure Docker is running (for Testcontainers)
2. Pull your latest code: `git pull`
3. Verify tests pass: `dotnet test`
4. Have your API ready to start for k6 demo
5. Have coverage report generated

## Presentation Tips

- Start with a **brief** overview of your domain (1-2 min max) — the focus is on testing, not the app itself
- Show the **test pyramid** of your project: how many unit / integration / database / performance tests
- Highlight one interesting test that catches a non-obvious bug
- Show the coverage report and explain any uncovered areas
- Present k6 results: what load could your API handle?

## Evaluation Criteria

See Lab 13 for detailed grading breakdown:

| Criteria | Weight |
|---|---|
| Unit tests (quality, NSubstitute) | 20% |
| Integration tests (WebApplicationFactory) | 20% |
| Database tests (Testcontainers) | 20% |
| k6 performance tests | 15% |
| Code coverage ≥ 80% | 10% |
| Presentation and defense | 15% |

## After Defense

- Instructor provides feedback and grade
- No further submissions required after successful defense
