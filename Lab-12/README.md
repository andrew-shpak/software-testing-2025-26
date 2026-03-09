# Lab 12 — CI/CD Pipelines for Testing

## Objective

Set up automated testing pipelines using GitHub Actions. Configure test execution, code coverage reporting, and quality gates that run on every push and pull request.

**Duration:** 60 minutes

## Prerequisites

Before starting this lab, make sure you have:

- A GitHub account with a repository you can push to
- .NET 10 SDK (or later) installed locally
- A working .NET solution with tests from any previous lab
- Basic familiarity with YAML syntax
- Understanding of Git branching (creating branches, pull requests)
- `git` CLI installed and configured

Recommended background:

- [GitHub Actions Quickstart](https://docs.github.com/en/actions/quickstart) -- read this if you have never used GitHub Actions before
- Completed at least one lab with passing xUnit tests (e.g., Lab 3 or Lab 5)

## Key Concepts

### What Is CI/CD?

**Continuous Integration (CI)** is the practice of automatically building, testing, and validating code every time a developer pushes changes. **Continuous Delivery/Deployment (CD)** extends CI by automatically deploying validated code to staging or production.

In this lab, we focus on the **CI** side: running tests automatically.

### Why Automate Testing?

| Without CI | With CI |
|-----------|---------|
| "It works on my machine" | Tests run on a clean, reproducible environment |
| Tests are skipped before merging | Tests must pass to merge |
| Bugs caught late (after merge) | Bugs caught early (on PR) |
| Coverage unknown | Coverage tracked and enforced |
| Manual effort to verify quality | Automated quality gates |

### GitHub Actions Terminology

| Term | Meaning |
|------|---------|
| **Workflow** | A YAML file in `.github/workflows/` that defines automation |
| **Job** | A set of steps that run on the same runner |
| **Step** | A single command or action within a job |
| **Runner** | A virtual machine that executes the job (`ubuntu-latest`, `windows-latest`, etc.) |
| **Action** | A reusable unit of code (e.g., `actions/checkout@v6`) |
| **Trigger** | An event that starts the workflow (`push`, `pull_request`, `schedule`, etc.) |
| **Artifact** | A file produced by a workflow and uploaded for later access |
| **Matrix** | A strategy that runs the same job with different configurations |

### How Coverage Works

**Coverlet** instruments your .NET assemblies to track which lines of code are executed during tests. After tests complete, it generates a coverage report showing:

- **Line coverage**: percentage of code lines executed
- **Branch coverage**: percentage of conditional branches taken
- **Method coverage**: percentage of methods called

```
+--------------------------------------------------+
| Module       | Line   | Branch | Method           |
+--------------------------------------------------+
| MyApp.Core   | 87.3%  | 72.1%  | 95.0%            |
| MyApp.Web    | 63.5%  | 48.2%  | 80.0%            |
+--------------------------------------------------+
| Total        | 78.4%  | 62.8%  | 89.5%            |
+--------------------------------------------------+
```

## Tools

- CI/CD: [GitHub Actions](https://docs.github.com/en/actions)
- Coverage: [Coverlet](https://github.com/coverlet-coverage/coverlet) + [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- Code Quality: [SonarCloud](https://sonarcloud.io/) (optional)

## Setup

Create a GitHub repository and push a .NET solution with tests from any previous lab.

### Initial Repository Structure

Your repository should look roughly like this before starting:

```
my-lab-project/
├── .github/
│   └── workflows/
│       └── ci.yml          <-- you will create this
├── src/
│   └── MyApp/
│       └── ...
├── tests/
│   └── MyApp.Tests/
│       └── ...
├── MyApp.sln
└── README.md
```

### Adding Coverage Packages

Make sure your test project includes the Coverlet collector:

```bash
dotnet add tests/MyApp.Tests package coverlet.collector
```

## Tasks

### Task 1 — Basic CI Pipeline

Create `.github/workflows/ci.yml`:

1. Trigger on `push` to `main` and on all `pull_request` events
2. Use `ubuntu-latest` runner
3. Steps:
   - Checkout code
   - Setup .NET SDK
   - Restore dependencies
   - Build the solution
   - Run all tests with `dotnet test`
4. Ensure the pipeline fails if any test fails

Example structure:

```yaml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
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
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

> **Hint:** The `--no-restore` and `--no-build` flags avoid redundant work. Each step builds on the previous one: restore downloads packages, build compiles the code, test runs the compiled tests. Without these flags, each step would repeat all previous work.

#### Verifying Your Pipeline

After pushing `ci.yml`, go to your GitHub repository and click the **Actions** tab. You should see your workflow running. A green check mark means all steps passed. A red X means something failed -- click into it to see the logs.

### Task 2 — Code Coverage Gate

Extend the pipeline to:

1. Collect coverage with Coverlet:
   ```yaml
   - name: Test with coverage
     run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
   ```
2. Generate HTML report with ReportGenerator
3. Upload coverage report as GitHub Actions artifact
4. Add a coverage threshold — fail the pipeline if coverage drops below 80%
5. Configure the pipeline to also run on `pull_request` events

#### Complete Coverage Pipeline Example

```yaml
- name: Test with coverage
  run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

- name: Install ReportGenerator
  run: dotnet tool install --global dotnet-reportgenerator-globaltool

- name: Generate coverage report
  run: |
    reportgenerator \
      -reports:./coverage/**/coverage.cobertura.xml \
      -targetdir:./coverage/report \
      -reporttypes:"Html;TextSummary;Badges"

- name: Display coverage summary
  run: cat ./coverage/report/Summary.txt

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: ./coverage/report/

- name: Check coverage threshold
  run: |
    COVERAGE=$(grep -oP 'Line coverage: \K[\d.]+' ./coverage/report/Summary.txt)
    echo "Line coverage: $COVERAGE%"
    if (( $(echo "$COVERAGE < 80" | bc -l) )); then
      echo "::error::Coverage $COVERAGE% is below the 80% threshold"
      exit 1
    fi
```

> **Hint:** The coverage threshold step parses the coverage percentage from the text summary report and fails if it is below 80%. You can adjust this threshold to match your project's needs. In practice, teams often start with a lower threshold and increase it over time.

#### Coverage Output Formats

| Format | Use Case |
|--------|----------|
| `Cobertura` | Machine-readable XML; input for ReportGenerator and other tools |
| `Html` | Human-readable report with file-by-file drill-down |
| `TextSummary` | Quick text summary for CI logs |
| `Badges` | SVG badges for README display |
| `lcov` | Compatible with many code quality platforms |

### Task 3 — Matrix Testing

Configure the pipeline to test across multiple configurations:

```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet-version: ['9.0.x', '10.0.x']
```

1. Run tests on all OS + .NET version combinations
2. Ensure all matrix jobs must pass for the pipeline to succeed

#### Full Matrix Job Example

```yaml
jobs:
  test:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet-version: ['9.0.x', '10.0.x']
    steps:
      - uses: actions/checkout@v6
      - name: Setup .NET ${{ matrix.dotnet-version }}
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal

  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Check formatting
        run: dotnet format --verify-no-changes --verbosity diagnostic
```

#### Matrix Behavior Reference

| Setting | Effect |
|---------|--------|
| `fail-fast: true` (default) | Cancel all matrix jobs if any one fails |
| `fail-fast: false` | Run all matrix jobs even if some fail |
| `exclude` | Remove specific combinations from the matrix |
| `include` | Add specific combinations to the matrix |

> **Hint:** Set `fail-fast: false` during development so you can see which specific OS/version combinations fail. In production pipelines, `fail-fast: true` saves CI minutes by stopping early.

## Grading

| Criteria |
|----------|
| Task 1 — Basic CI pipeline |
| Task 2 — Coverage gate |
| Task 3 — Matrix testing |

## Submission

- GitHub repository URL with working pipeline

## References

- [GitHub Actions Documentation](https://docs.github.com/en/actions) -- official reference for all GitHub Actions features
- [GitHub Actions Workflow Syntax](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions) -- YAML syntax reference
- [actions/checkout](https://github.com/actions/checkout) -- checkout action used in every workflow
- [actions/setup-dotnet](https://github.com/actions/setup-dotnet) -- .NET SDK setup action
- [actions/upload-artifact](https://github.com/actions/upload-artifact) -- uploading build artifacts
- [dorny/test-reporter](https://github.com/dorny/test-reporter) -- publishing test results on PRs
- [Coverlet GitHub Repository](https://github.com/coverlet-coverage/coverlet) -- .NET code coverage library
- [Coverlet Documentation: Integration with MSBuild](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md) -- advanced Coverlet configuration
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator) -- converting coverage files to HTML reports
- [GitHub Branch Protection Rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-a-branch-protection-rule) -- configuring quality gates
- [dotnet test CLI Reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) -- all flags and options for `dotnet test`
- [dotnet format CLI Reference](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) -- code style enforcement tool
