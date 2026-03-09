# Lecture 7: CI/CD and Test Management

## Learning Objectives

By the end of this lecture, students will be able to:

- Define Continuous Integration, Continuous Delivery, and Continuous Deployment
- Explain why CI/CD is essential for sustainable software testing
- Write GitHub Actions workflows to build, test, and analyze .NET projects
- Configure code coverage collection and reporting in CI pipelines
- Set up branch protection rules and quality gates
- Describe test management concepts: test plans, defect lifecycle, and metrics
- Identify and manage flaky tests in CI environments
- Use Docker and Testcontainers in GitHub Actions for integration tests

---

## 1. What is CI/CD and Why It Matters for Testing

### 1.1 The Problem Without CI/CD

Imagine a team of five developers working on the same project. Each developer works on a separate branch for days or weeks. When they finally try to merge:

```
Developer A ──────────────────────► Merge ──┐
Developer B ──────────────────────► Merge ──┤
Developer C ──────────────────────► Merge ──┼──► "Integration Hell"
Developer D ──────────────────────► Merge ──┤    (conflicts, broken tests,
Developer E ──────────────────────► Merge ──┘     incompatible changes)
```

Common symptoms:
- Merge conflicts that take days to resolve
- Tests that pass locally but fail when combined
- "It works on my machine" syndrome
- No one knows if the main branch is deployable
- Manual testing becomes a bottleneck

### 1.2 The CI/CD Solution

CI/CD automates the process of integrating code, running tests, and delivering software:

```
Developer pushes code
        │
        ▼
┌─────────────────────────────────────────────────────────┐
│  CI/CD Pipeline (automated)                             │
│                                                         │
│  ┌───────┐   ┌──────┐   ┌─────────┐   ┌────────────┐  │
│  │ Build │──►│ Test │──►│ Analyze │──►│   Deploy   │  │
│  └───────┘   └──────┘   └─────────┘   └────────────┘  │
│                                                         │
│  Minutes, not days                                      │
└─────────────────────────────────────────────────────────┘
        │
        ▼
Fast feedback: pass ✓ or fail ✗
```

### 1.3 Key Benefits for Testing

| Benefit | Description |
|---|---|
| **Fast feedback** | Developers know within minutes if their change breaks something |
| **Consistent environment** | Tests run in the same environment every time |
| **Automated regression** | Every change is tested against the full test suite |
| **Quality visibility** | Coverage reports, test trends, and metrics are always available |
| **Confidence to refactor** | Comprehensive automated tests make refactoring safe |
| **Shift-left testing** | Defects are caught early, when they are cheapest to fix |

> **Discussion (5 min):** Have you ever experienced "integration hell"? How long did it take to resolve? How could CI/CD have helped?

---

## 2. CI/CD Concepts

### 2.1 Continuous Integration (CI)

**Continuous Integration** is the practice of merging all developers' working copies to a shared mainline **frequently** — at least once per day.

#### Core Principles

1. **Maintain a single source repository** — all code in one place (e.g., Git)
2. **Automate the build** — one command to build the entire project
3. **Make the build self-testing** — automated tests run with every build
4. **Every commit triggers a build** — no manual intervention needed
5. **Keep the build fast** — ideally under 10 minutes
6. **Fix broken builds immediately** — a broken build is the team's top priority
7. **Everyone can see the results** — build status is visible to the whole team

#### What CI Looks Like in Practice

```
Developer workflow with CI:

1. Pull latest from main          git pull origin main
2. Make changes                   (write code + tests)
3. Run tests locally              dotnet test
4. Commit and push                git push origin feature-branch
5. CI server runs automatically   Build → Test → Report
6. Review results                 Green ✓ → create PR
                                  Red ✗ → fix and push again
```

### 2.2 Continuous Delivery vs. Continuous Deployment

These terms are often confused. Here is the distinction:

```
Continuous Integration
        │
        ▼
┌──────────────────┐
│  Code committed  │
│  Build + Test    │
└────────┬─────────┘
         │
         ▼
┌──────────────────┐     ┌──────────────────────────────────────┐
│ Continuous       │     │ Continuous Deployment                │
│ Delivery         │     │                                      │
│                  │     │ Every change that passes all stages  │
│ Every change is  │     │ is automatically deployed to         │
│ deployable, but  │     │ production — no manual approval.     │
│ deployment is a  │     │                                      │
│ manual decision. │     │ Requires: high test confidence,      │
│                  │     │ feature flags, monitoring.           │
└──────────────────┘     └──────────────────────────────────────┘
```

| Aspect | Continuous Delivery | Continuous Deployment |
|---|---|---|
| **Deployment trigger** | Manual approval | Automatic |
| **Risk tolerance** | Lower | Higher (mitigated by automation) |
| **Test confidence required** | High | Very high |
| **Release frequency** | On-demand (daily/weekly) | Every passing commit |
| **Common in** | Enterprise, regulated industries | SaaS, web applications |

### 2.3 CI/CD Pipeline Anatomy

A typical CI/CD pipeline has four main stages:

```
┌─────────────────────────────────────────────────────────────────┐
│                        CI/CD Pipeline                           │
│                                                                 │
│  ┌─────────┐  ┌──────────┐  ┌───────────┐  ┌───────────────┐  │
│  │  BUILD   │  │   TEST   │  │  ANALYZE  │  │    DEPLOY     │  │
│  │         │  │          │  │           │  │               │  │
│  │ Restore  │  │ Unit     │  │ Coverage  │  │ Staging       │  │
│  │ Compile  │  │ Integr.  │  │ Linting   │  │ Production    │  │
│  │ Publish  │  │ E2E      │  │ Security  │  │ Smoke tests   │  │
│  └────┬────┘  └────┬─────┘  └─────┬─────┘  └───────┬───────┘  │
│       │            │              │                 │           │
│       ▼            ▼              ▼                 ▼           │
│    Artifact     Results +      Reports          Running        │
│    (.dll)       Reports        (HTML)           application     │
└─────────────────────────────────────────────────────────────────┘
```

| Stage | Purpose | Tools (in this course) |
|---|---|---|
| **Build** | Compile code, restore dependencies | `dotnet restore`, `dotnet build` |
| **Test** | Run automated tests at all levels | `dotnet test`, xUnit v3 |
| **Analyze** | Measure quality metrics | Coverlet, ReportGenerator, Roslyn analyzers |
| **Deploy** | Release to an environment | GitHub Actions deploy steps, Docker |

> **Discussion (5 min):** Which stage do you think fails most often in real projects? Why?

---

## 3. GitHub Actions Fundamentals

### 3.1 What is GitHub Actions?

GitHub Actions is a CI/CD platform built into GitHub. It allows you to automate workflows directly from your repository.

Key concepts:

```
┌─────────────────────────────────────────────────────┐
│  Workflow (.github/workflows/*.yml)                 │
│                                                     │
│  Triggered by: push, pull_request, schedule, etc.   │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │  Job: build-and-test                          │  │
│  │  runs-on: ubuntu-latest                       │  │
│  │                                               │  │
│  │  ┌─────────┐ ┌─────────┐ ┌────────────────┐  │  │
│  │  │ Step 1  │ │ Step 2  │ │    Step 3      │  │  │
│  │  │Checkout │►│ Setup   │►│  Build & Test  │  │  │
│  │  │  code   │ │  .NET   │ │                │  │  │
│  │  └─────────┘ └─────────┘ └────────────────┘  │  │
│  └───────────────────────────────────────────────┘  │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │  Job: deploy (depends on build-and-test)      │  │
│  │  ...                                          │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

| Concept | Description |
|---|---|
| **Workflow** | A YAML file that defines an automated process |
| **Event/Trigger** | What starts the workflow (push, PR, schedule) |
| **Job** | A set of steps that run on the same runner |
| **Step** | A single task — either a shell command or an action |
| **Action** | A reusable unit of code (e.g., `actions/checkout@v6`) |
| **Runner** | The virtual machine that executes the job |

### 3.2 Workflow File Structure

Workflow files live in `.github/workflows/` and use YAML syntax:

```yaml
# .github/workflows/ci.yml

name: CI Pipeline                  # Display name in GitHub UI

on:                                # Triggers
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:                              # One or more jobs
  build-and-test:                  # Job identifier
    runs-on: ubuntu-latest         # Runner OS

    steps:                         # Sequential steps
      - name: Checkout code
        uses: actions/checkout@v6  # Use a pre-built action

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run tests
        run: dotnet test --no-build --configuration Release --verbosity normal
```

### 3.3 Triggers (Events)

GitHub Actions supports many trigger events. The most common for CI:

```yaml
on:
  # Trigger on push to specific branches
  push:
    branches: [ main, develop ]
    paths-ignore:
      - '**.md'              # Don't trigger on documentation changes
      - 'docs/**'

  # Trigger on pull requests
  pull_request:
    branches: [ main ]
    types: [ opened, synchronize, reopened ]

  # Scheduled trigger (cron syntax)
  schedule:
    - cron: '0 6 * * 1'     # Every Monday at 6:00 AM UTC

  # Manual trigger from GitHub UI
  workflow_dispatch:
    inputs:
      environment:
        description: 'Target environment'
        required: true
        default: 'staging'
        type: choice
        options:
          - staging
          - production
```

#### Cron Syntax Reference

```
┌───────── minute (0-59)
│ ┌─────── hour (0-23)
│ │ ┌───── day of month (1-31)
│ │ │ ┌─── month (1-12)
│ │ │ │ ┌─ day of week (0-6, Sunday=0)
│ │ │ │ │
* * * * *

Examples:
'0 6 * * 1'        Every Monday at 6:00 AM
'0 0 * * *'        Every day at midnight
'*/15 * * * *'     Every 15 minutes
'0 8 1 * *'        First day of every month at 8:00 AM
```

### 3.4 Jobs and Steps

Jobs run in parallel by default. Use `needs` to create dependencies:

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet build

  unit-tests:
    needs: build                   # Runs after 'build' completes
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet test --filter "Category=Unit"

  integration-tests:
    needs: build                   # Runs after 'build', parallel with unit-tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet test --filter "Category=Integration"

  deploy:
    needs: [unit-tests, integration-tests]  # Runs after BOTH test jobs pass
    runs-on: ubuntu-latest
    steps:
      - run: echo "Deploying..."
```

This creates the following execution graph:

```
              ┌──────────────┐
              │    build     │
              └──────┬───────┘
                     │
            ┌────────┴────────┐
            ▼                 ▼
   ┌──────────────┐  ┌────────────────────┐
   │  unit-tests  │  │ integration-tests  │
   └──────┬───────┘  └────────┬───────────┘
          │                   │
          └────────┬──────────┘
                   ▼
            ┌────────────┐
            │   deploy   │
            └────────────┘
```

### 3.5 Matrix Builds

Matrix builds let you test across multiple configurations simultaneously:

```yaml
jobs:
  test:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet-version: ['9.0.x', '10.0.x']
      fail-fast: false           # Continue other matrix jobs if one fails

    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET ${{ matrix.dotnet-version }}
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}

      - name: Run tests
        run: dotnet test --verbosity normal
```

This creates **6 parallel jobs** (3 OS x 2 .NET versions):

```
┌──────────────────────────────────────────────────────────┐
│  Matrix: 3 OS × 2 .NET versions = 6 jobs                │
│                                                          │
│  ubuntu  + .NET 9   │  windows + .NET 9   │  macos + .NET 9 │
│  ubuntu  + .NET 10  │  windows + .NET 10  │  macos + .NET 10│
└──────────────────────────────────────────────────────────┘
```

You can also exclude specific combinations:

```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet-version: ['9.0.x', '10.0.x']
    exclude:
      - os: macos-latest
        dotnet-version: '9.0.x'    # Skip .NET 10 on macOS
```

### 3.6 Caching NuGet Packages

Caching speeds up builds by reusing downloaded dependencies:

```yaml
steps:
  - uses: actions/checkout@v6

  - name: Setup .NET
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: '10.0.x'

  - name: Cache NuGet packages
    uses: actions/cache@v5
    with:
      path: ~/.nuget/packages
      key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
      restore-keys: |
        ${{ runner.os }}-nuget-

  - name: Restore dependencies
    run: dotnet restore

  - name: Build
    run: dotnet build --no-restore
```

How caching works:

```
First run:                           Subsequent runs:
┌─────────────────────┐              ┌─────────────────────┐
│ Cache miss          │              │ Cache hit            │
│                     │              │                      │
│ dotnet restore      │              │ Restore from cache   │
│ (downloads ~200MB)  │              │ (~3 seconds)         │
│ Takes: ~45 seconds  │              │                      │
│                     │              │ dotnet restore       │
│ Save to cache       │              │ (nothing to do)      │
└─────────────────────┘              └──────────────────────┘
```

The cache key uses `hashFiles('**/*.csproj')` so the cache is invalidated whenever project dependencies change.

### 3.7 Artifacts

Artifacts let you persist data from a workflow run — such as test results, coverage reports, or build outputs:

```yaml
steps:
  - name: Run tests with results
    run: dotnet test --logger "trx;LogFileName=test-results.trx" --results-directory ./test-results

  - name: Upload test results
    uses: actions/upload-artifact@v4
    if: always()                    # Upload even if tests fail
    with:
      name: test-results
      path: ./test-results/*.trx
      retention-days: 30            # Keep for 30 days

  - name: Upload build output
    uses: actions/upload-artifact@v4
    with:
      name: build-artifacts
      path: |
        src/**/bin/Release/**
        !src/**/obj/**
```

Artifacts can be downloaded from the GitHub Actions UI or consumed by later jobs:

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet publish -c Release -o ./publish
      - uses: actions/upload-artifact@v4
        with:
          name: app
          path: ./publish

  deploy:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: app
          path: ./app
      - run: echo "Deploying from ./app"
```

> **Discussion (5 min):** What artifacts would be most useful for your team to keep from each CI run? How long should they be retained?

---

## 4. Running .NET Tests in GitHub Actions

### 4.1 Basic .NET Test Workflow

Here is a complete, production-ready workflow for a .NET project:

```yaml
# .github/workflows/dotnet-ci.yml

name: .NET CI

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '9.0.x'
  DOTNET_NOLOGO: true                  # Suppress .NET welcome message
  DOTNET_CLI_TELEMETRY_OPTOUT: true    # Disable telemetry

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET ${{ env.DOTNET_VERSION }}
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v5
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run tests
        run: >
          dotnet test
          --no-build
          --configuration Release
          --verbosity normal
          --logger "trx;LogFileName=test-results.trx"
          --results-directory ./test-results

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./test-results/**/*.trx
```

### 4.2 Understanding Test Output in CI

When tests run in CI, the output appears in the workflow logs:

```
Run dotnet test --no-build --configuration Release --verbosity normal

  Determining projects to restore...
  All projects are up-to-date for restore.
  Calculator -> /home/runner/work/project/src/Calculator/bin/Release/net10.0/Calculator.dll
  Calculator.Tests -> /home/runner/work/project/tests/Calculator.Tests/bin/Release/net10.0/Calculator.Tests.dll

  Starting test execution...
  Passed   Add_TwoPositiveNumbers_ReturnsCorrectSum [3ms]
  Passed   Subtract_LargerFromSmaller_ReturnsNegative [< 1ms]
  Passed   Divide_ByZero_ThrowsDivideByZeroException [1ms]
  Passed   IsEven_GivenNumber_ReturnsExpectedResult(number: 2, expected: True) [< 1ms]
  Passed   IsEven_GivenNumber_ReturnsExpectedResult(number: 3, expected: False) [< 1ms]

  Test Run Successful.
  Total tests: 5
       Passed: 5
  Total time: 1.234 Seconds
```

### 4.3 Filtering Tests by Category

You can run different test categories in separate jobs using traits:

```csharp
// In your test code, use Traits to categorize tests
[Fact]
[Trait("Category", "Unit")]
public void Add_TwoNumbers_ReturnsSum() { ... }

[Fact]
[Trait("Category", "Integration")]
public async Task GetOrder_ExistingId_ReturnsOrderAsync() { ... }
```

```yaml
# In your workflow
jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test --filter "Category=Unit" --verbosity normal

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test --filter "Category=Integration" --verbosity normal
```

---

## 5. Code Coverage in CI

### 5.1 Why Measure Coverage in CI?

Code coverage measured locally is useful, but measuring it in CI provides:

- **Consistent baseline** — everyone's coverage is measured the same way
- **Trend tracking** — see coverage change over time
- **Quality gates** — block merges if coverage drops below a threshold
- **Visibility** — coverage reports available to the whole team

### 5.2 Coverlet + ReportGenerator in GitHub Actions

#### Step 1: Add Coverlet to Your Test Project

```bash
dotnet add <TestProject> package coverlet.collector
```

#### Step 2: Workflow with Coverage Collection

```yaml
# .github/workflows/ci-with-coverage.yml

name: CI with Coverage

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '9.0.x'

jobs:
  build-test-coverage:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v5
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Run tests with coverage
        run: >
          dotnet test
          --no-build
          --configuration Release
          --verbosity normal
          --collect:"XPlat Code Coverage"
          --results-directory ./coverage

      - name: Install ReportGenerator
        run: dotnet tool install --global dotnet-reportgenerator-globaltool

      - name: Generate coverage report
        run: >
          reportgenerator
          -reports:./coverage/**/coverage.cobertura.xml
          -targetdir:./coverage/report
          -reporttypes:"Html;TextSummary;Cobertura"

      - name: Display coverage summary
        run: cat ./coverage/report/Summary.txt

      - name: Upload coverage report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./coverage/report/
          retention-days: 14
```

### 5.3 Coverage Summary Output

The `Summary.txt` output looks like this in the CI logs:

```
Summary
  Generated on: 3/9/2026
  Parser:       Cobertura
  Assemblies:   2
  Classes:      8
  Files:        8
  Line coverage:    87.3%
  Branch coverage:  72.1%
  Method coverage:  91.4%

  +-----------------------+--------+--------+--------+
  | Assembly              | Line   | Branch | Method |
  +-----------------------+--------+--------+--------+
  | Calculator            | 92.5%  | 80.0%  | 95.0%  |
  | EStore                | 82.1%  | 64.2%  | 87.8%  |
  +-----------------------+--------+--------+--------+
```

### 5.4 Publishing Coverage as a PR Comment

You can add coverage results directly to pull request comments for easy review:

```yaml
      - name: Add coverage PR comment
        uses: marocchino/sticky-pull-request-comment@v2
        if: github.event_name == 'pull_request'
        with:
          recreate: true
          path: ./coverage/report/Summary.txt
```

Another approach uses a dedicated coverage action:

```yaml
      - name: Code coverage report
        uses: irongut/CodeCoverageSummary@v1.3.0
        if: github.event_name == 'pull_request'
        with:
          filename: ./coverage/**/coverage.cobertura.xml
          badge: true
          format: markdown
          output: both
          thresholds: '60 80'      # Yellow at 60%, green at 80%

      - name: Add coverage to PR
        uses: marocchino/sticky-pull-request-comment@v2
        if: github.event_name == 'pull_request'
        with:
          recreate: true
          path: code-coverage-results.md
```

### 5.5 Enforcing Minimum Coverage

You can fail the build if coverage drops below a threshold:

```yaml
      - name: Check coverage threshold
        run: |
          COVERAGE=$(grep -oP 'Line coverage:\s+\K[\d.]+' ./coverage/report/Summary.txt)
          echo "Line coverage: ${COVERAGE}%"
          THRESHOLD=80
          if (( $(echo "$COVERAGE < $THRESHOLD" | bc -l) )); then
            echo "::error::Coverage ${COVERAGE}% is below the threshold of ${THRESHOLD}%"
            exit 1
          fi
          echo "Coverage ${COVERAGE}% meets the threshold of ${THRESHOLD}%"
```

> **Discussion (5 min):** What is a reasonable coverage threshold for a project? Should it be the same for all modules? What are the risks of setting it too high or too low?

---

## 6. Branch Protection Rules and Required Checks

### 6.1 What Are Branch Protection Rules?

Branch protection rules prevent direct pushes to important branches (like `main`) and require certain conditions before code can be merged.

```
Without protection:                 With protection:

Anyone can push                     Push blocked unless:
directly to main                    ✓ CI pipeline passes
                                    ✓ Code review approved
           │                        ✓ Coverage threshold met
           ▼                        ✓ No merge conflicts
    Risky, unreviewed
    code in production                     │
                                           ▼
                                    Safe, reviewed, tested
                                    code in production
```

### 6.2 Configuring Branch Protection in GitHub

Navigate to: **Repository Settings > Branches > Add branch protection rule**

Key settings:

| Setting | Purpose | Recommendation |
|---|---|---|
| **Require a pull request before merging** | No direct pushes to protected branch | Always enable for `main` |
| **Require approvals** | Code review by at least N reviewers | 1-2 approvals minimum |
| **Dismiss stale approvals** | Re-review required after new commits | Enable |
| **Require status checks to pass** | CI must pass before merge | Enable; select your CI job |
| **Require branches to be up to date** | Branch must be current with base | Enable for critical branches |
| **Require conversation resolution** | All PR comments must be resolved | Recommended |
| **Include administrators** | Rules apply to admins too | Recommended for production |

### 6.3 Required Status Checks

When you enable "Require status checks to pass," you select which GitHub Actions jobs must succeed:

```yaml
# The job name becomes the status check name
jobs:
  build-and-test:          # ← This name appears in branch protection settings
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet test
```

```
Pull Request:

  ✓ build-and-test         Passed
  ✓ coverage-check         Passed — 85% coverage
  ✗ integration-tests      Failed — 2 tests failed    ← Blocks merge
  ○ deploy-staging         Skipped (waiting on tests)

  [Merge pull request]  ← Button is disabled until all checks pass
```

### 6.4 Rulesets (Modern Approach)

GitHub now offers **Rulesets** as a more flexible alternative to branch protection rules. Rulesets support:

- Targeting multiple branches with patterns (e.g., `release/*`)
- Organization-level rules that apply across repositories
- Tag protection
- Bypass lists for specific users or teams

```
Repository Settings > Rules > Rulesets > New ruleset

Target: branches matching "main", "release/*"
Rules:
  ✓ Require pull request
  ✓ Require status checks: "build-and-test"
  ✓ Block force pushes
  ✓ Require linear history
```

---

## 7. Test Management Concepts

### 7.1 Test Plans and Test Strategies

A **test strategy** defines the overall approach to testing for a project. A **test plan** is a concrete document that describes what, how, when, and who.

#### Test Strategy Components

```
┌─────────────────────────────────────────────────────────┐
│  Test Strategy                                          │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │   Scope &   │  │  Test Levels │  │    Tools &    │  │
│  │  Objectives │  │  & Types     │  │ Infrastructure│  │
│  └─────────────┘  └──────────────┘  └───────────────┘  │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  Entry/Exit │  │   Risk       │  │  Roles &      │  │
│  │  Criteria   │  │   Analysis   │  │  Responsibilities│
│  └─────────────┘  └──────────────┘  └───────────────┘  │
└─────────────────────────────────────────────────────────┘
```

#### Test Plan Structure (IEEE 829 Based)

| Section | Content |
|---|---|
| **Test plan identifier** | Unique ID and version |
| **Introduction** | Purpose and scope of testing |
| **Test items** | What software/features are being tested |
| **Features to be tested** | Specific features in scope |
| **Features not to be tested** | Excluded features (with justification) |
| **Approach** | Testing techniques and methods |
| **Pass/fail criteria** | What constitutes a pass or fail |
| **Test environment** | Hardware, software, network requirements |
| **Schedule** | Timeline and milestones |
| **Risks and contingencies** | Known risks and mitigation plans |

### 7.2 Test Case Management

A **test case** is a set of conditions and expected results used to verify a specific aspect of the system.

#### Test Case Template

```
┌────────────────────────────────────────────────────────────────┐
│ Test Case ID:     TC-LOGIN-001                                 │
│ Title:            Valid user can log in with correct credentials│
│ Priority:         High                                         │
│ Preconditions:    User account exists; user is not locked out  │
│                                                                │
│ Steps:                                                         │
│   1. Navigate to /login                                        │
│   2. Enter valid email: user@example.com                       │
│   3. Enter valid password: CorrectPassword123!                 │
│   4. Click "Sign In" button                                   │
│                                                                │
│ Expected Result:  User is redirected to /dashboard             │
│                   Welcome message displays user's name         │
│                                                                │
│ Actual Result:    (filled during execution)                    │
│ Status:           Pass / Fail / Blocked / Skipped              │
│ Tested By:        (tester name)                                │
│ Date:             (execution date)                             │
└────────────────────────────────────────────────────────────────┘
```

#### Automated vs. Manual Test Cases

| Characteristic | Automate | Keep Manual |
|---|---|---|
| Regression tests | Yes | No |
| Smoke tests | Yes | No |
| Data-driven tests | Yes | No |
| Exploratory testing | No | Yes |
| Usability testing | No | Yes |
| Tests run once | No | Yes |
| Complex UI workflows | Sometimes | Sometimes |

### 7.3 Defect Lifecycle

A defect (bug) follows a lifecycle from discovery to resolution:

```
┌──────┐    ┌────────┐    ┌──────────┐    ┌────────┐    ┌────────┐
│ New  │───►│  Open  │───►│ Assigned │───►│ Fixed  │───►│ Closed │
└──────┘    └────┬───┘    └────┬─────┘    └───┬────┘    └────────┘
                 │             │              │              ▲
                 │             │              ▼              │
                 │             │         ┌─────────┐        │
                 │             │         │Verified │────────┘
                 │             │         └────┬────┘
                 │             │              │
                 │             ▼              ▼
                 │      ┌───────────┐   ┌──────────┐
                 │      │ Deferred  │   │ Reopened │
                 │      └───────────┘   └──────────┘
                 │
                 ▼
           ┌───────────┐
           │ Rejected  │  (not a bug / duplicate / by design)
           └───────────┘
```

#### Defect Report Fields

| Field | Description | Example |
|---|---|---|
| **ID** | Unique identifier | BUG-1234 |
| **Title** | Short, descriptive summary | "Login fails for emails with '+' character" |
| **Severity** | Technical impact | Critical / Major / Minor / Trivial |
| **Priority** | Business urgency | P1 (urgent) / P2 (high) / P3 (normal) / P4 (low) |
| **Steps to reproduce** | Exact steps to trigger the bug | 1. Go to /login 2. Enter user+tag@mail.com... |
| **Expected result** | What should happen | User is logged in successfully |
| **Actual result** | What actually happens | Error: "Invalid email format" |
| **Environment** | Where it was found | Chrome 120, Ubuntu 22.04, .NET 10 |
| **Attachments** | Screenshots, logs, videos | screenshot.png, error.log |

> **Discussion (5 min):** What is the difference between severity and priority? Can a trivial bug have high priority? Can a critical bug have low priority? Give examples.

### 7.4 Test Reporting and Metrics

#### Key Testing Metrics

| Metric | Formula | What It Tells You |
|---|---|---|
| **Test pass rate** | Passed / Total tests | Overall quality signal |
| **Defect density** | Defects / KLOC (thousands of lines of code) | Code quality by module |
| **Defect detection rate** | Defects found in testing / Total defects | Testing effectiveness |
| **Test coverage** | Lines covered / Total lines | How much code is exercised |
| **Mean time to detect (MTTD)** | Average time from introduction to discovery | Speed of feedback |
| **Mean time to resolve (MTTR)** | Average time from detection to fix | Team responsiveness |
| **Escaped defects** | Defects found in production | Testing gaps |
| **Flaky test rate** | Flaky tests / Total tests | CI reliability |

#### Test Dashboard Example

```
┌──────────────────────────────────────────────────────────┐
│  Test Dashboard — Sprint 14                              │
│                                                          │
│  Test Execution          Coverage           Defects      │
│  ┌──────────────┐        ┌────────────┐     ┌─────────┐ │
│  │ Total:  342  │        │ Line: 87%  │     │ Open: 12│ │
│  │ Pass:   331  │        │ Branch:72% │     │ Fixed: 8│ │
│  │ Fail:     8  │        │ Method:91% │     │ New:   5│ │
│  │ Skip:     3  │        └────────────┘     └─────────┘ │
│  │ Rate: 96.8%  │                                        │
│  └──────────────┘        Build Health                    │
│                          ┌────────────────────────────┐  │
│  Flaky Tests: 4 (1.2%)   │ Last 30 builds: 27 green  │  │
│                          │                 3 red      │  │
│                          │ Success rate: 90%          │  │
│                          └────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

### 7.5 Risk-Based Testing

Not all features carry the same risk. Risk-based testing prioritizes testing effort based on the **likelihood** and **impact** of failure.

#### Risk Matrix

```
                    Impact
                Low      Medium     High
           ┌─────────┬──────────┬──────────┐
  High     │ Medium  │   High   │ Critical │
           │         │          │          │
Likelihood ├─────────┼──────────┼──────────┤
  Medium   │  Low    │  Medium  │   High   │
           │         │          │          │
           ├─────────┼──────────┼──────────┤
  Low      │  Low    │   Low    │  Medium  │
           │         │          │          │
           └─────────┴──────────┴──────────┘
```

| Risk Level | Testing Approach |
|---|---|
| **Critical** | Extensive automated tests, manual exploratory testing, performance testing |
| **High** | Comprehensive automated tests, targeted manual testing |
| **Medium** | Standard automated test coverage |
| **Low** | Basic smoke tests, rely on automated regression |

#### Example: E-Commerce Application

| Feature | Likelihood of Failure | Impact of Failure | Risk | Testing Effort |
|---|---|---|---|---|
| Payment processing | Medium | Critical | **Critical** | Full coverage + E2E + load tests |
| User registration | Low | High | **Medium** | Unit + integration tests |
| Product search | Medium | Medium | **Medium** | Unit + basic E2E |
| Admin dashboard | Low | Low | **Low** | Basic smoke tests |

> **Discussion (10 min):** For a healthcare appointment booking system, how would you classify the risk of these features: booking an appointment, canceling an appointment, viewing medical records, changing the UI theme? How would this affect your testing strategy?

---

## 8. Quality Gates

### 8.1 What is a Quality Gate?

A **quality gate** is a set of conditions that must be met before code can progress to the next stage (e.g., from development to staging, or from staging to production).

```
Code change
     │
     ▼
┌──────────────────┐     ┌──────────────────┐     ┌──────────────┐
│  Gate 1: Merge   │     │  Gate 2: Deploy  │     │  Gate 3:     │
│  to main         │     │  to staging      │     │  Production  │
│                  │     │                  │     │              │
│  ✓ Tests pass    │     │  ✓ Gate 1 passed │     │  ✓ Gate 2    │
│  ✓ Coverage ≥80% │────►│  ✓ E2E tests     │────►│  ✓ Smoke     │
│  ✓ No critical   │     │  ✓ Performance   │     │    tests     │
│    issues        │     │    benchmarks    │     │  ✓ Manual    │
│  ✓ Code review   │     │  ✓ Security scan │     │    approval  │
│    approved      │     │                  │     │              │
└──────────────────┘     └──────────────────┘     └──────────────┘
```

### 8.2 Common Quality Gate Criteria

| Gate | Criteria | Implementation |
|---|---|---|
| **PR merge** | All tests pass | GitHub Actions required status checks |
| **PR merge** | Code coverage above threshold | Coverlet + coverage check step |
| **PR merge** | No new critical/high issues | Static analysis (Roslyn, SonarCloud) |
| **PR merge** | Code review approved | Branch protection: require approvals |
| **Staging deploy** | All integration tests pass | E2E test job in pipeline |
| **Staging deploy** | Performance within acceptable range | Benchmark comparison step |
| **Production deploy** | Manual approval by designated reviewer | GitHub Environments with reviewers |
| **Production deploy** | Smoke tests pass on staging | Automated smoke test job |

### 8.3 Implementing Quality Gates in GitHub Actions

#### Using GitHub Environments for Deployment Approval

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - run: dotnet test

  deploy-staging:
    needs: test
    runs-on: ubuntu-latest
    environment: staging               # Links to GitHub environment
    steps:
      - run: echo "Deploying to staging..."

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production            # Requires manual approval
    steps:
      - run: echo "Deploying to production..."
```

Configure approval in: **Repository Settings > Environments > production > Required reviewers**

---

## 9. Flaky Test Detection and Management

### 9.1 What Are Flaky Tests?

A **flaky test** is a test that sometimes passes and sometimes fails without any change to the code. Flaky tests are one of the biggest problems in CI/CD.

```
Same code, same test, different results:

Run 1:  ✓ Pass
Run 2:  ✗ Fail    ← No code changed!
Run 3:  ✓ Pass
Run 4:  ✓ Pass
Run 5:  ✗ Fail    ← Flaky!
```

### 9.2 Common Causes of Flaky Tests

| Cause | Example | Fix |
|---|---|---|
| **Timing/race conditions** | `await Task.Delay(100)` in test | Use proper synchronization, not delays |
| **Shared state** | Tests share a database record | Isolate test data; reset state per test |
| **Test order dependency** | Test B relies on Test A running first | Make each test independent |
| **Time-dependent logic** | Test checks `DateTime.Now` | Inject a clock abstraction; mock time |
| **External dependencies** | Test calls a real API | Mock external services |
| **Resource exhaustion** | Port already in use | Use dynamic ports; clean up resources |
| **Floating-point precision** | `0.1 + 0.2 == 0.3` | Use tolerance: `result.ShouldBe(0.3, 0.001)` |
| **Random data** | Test uses `Random` without seed | Use deterministic test data |

### 9.3 Detecting Flaky Tests

#### Strategy 1: Retry on Failure

Configure test retries to identify flaky tests. In xUnit v3, you can use the `[Retry]` attribute (available via extensions) or configure retries at the CI level:

```yaml
# Retry the entire test step
- name: Run tests
  run: dotnet test --verbosity normal
  continue-on-error: false

# Or use a retry action
- name: Run tests with retry
  uses: nick-fields/retry@v3
  with:
    max_attempts: 3
    timeout_minutes: 10
    command: dotnet test --verbosity normal
```

#### Strategy 2: Track Test Results Over Time

```yaml
- name: Run tests with TRX output
  run: >
    dotnet test
    --logger "trx;LogFileName=results.trx"
    --results-directory ./test-results

# Upload results for trend analysis
- name: Upload test results
  uses: actions/upload-artifact@v4
  if: always()
  with:
    name: test-results-${{ github.run_number }}
    path: ./test-results/
```

### 9.4 Managing Flaky Tests

A flaky test management process:

```
Test fails intermittently
         │
         ▼
┌──────────────────────┐
│ 1. Identify as flaky │──► Log in issue tracker with "flaky" label
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ 2. Quarantine        │──► Move to a separate test suite (optional)
└──────────┬───────────┘     Don't block the pipeline
           │
           ▼
┌──────────────────────┐
│ 3. Investigate root  │──► Find the cause (timing, state, resources)
│    cause             │
└──────────┬───────────┘
           │
           ▼
┌──────────────────────┐
│ 4. Fix or remove     │──► Fix the root cause; if not possible,
└──────────────────────┘     convert to manual test or remove
```

**Important rule:** Never ignore flaky tests. They erode trust in the CI pipeline. If developers learn to dismiss failures as "probably just a flaky test," real bugs will be missed.

> **Discussion (5 min):** Your CI pipeline has 500 tests and 3% are flaky. Every other build fails due to flaky tests. How would you handle this? Is it ever acceptable to just re-run the pipeline?

---

## 10. Environment Management

### 10.1 Common Environments

Software typically progresses through multiple environments before reaching users:

```
┌────────────┐    ┌────────────┐    ┌────────────┐    ┌──────────────┐
│   Local    │───►│    Dev     │───►│  Staging   │───►│  Production  │
│            │    │            │    │            │    │              │
│ Developer's│    │ Shared dev │    │ Production │    │ Live users   │
│ machine    │    │ environment│    │ mirror     │    │              │
│            │    │            │    │            │    │              │
│ Unit tests │    │ Integration│    │ E2E tests  │    │ Monitoring   │
│            │    │ tests      │    │ Perf tests │    │ Smoke tests  │
└────────────┘    └────────────┘    └────────────┘    └──────────────┘
```

| Environment | Purpose | Data | Testing |
|---|---|---|---|
| **Local** | Developer experimentation | Mock/in-memory | Unit tests, quick integration |
| **Dev** | Integration of all components | Synthetic test data | Integration, API tests |
| **Staging** | Pre-production validation | Production-like data (anonymized) | E2E, performance, security |
| **Production** | Live user traffic | Real data | Smoke tests, monitoring |

### 10.2 Environment Variables in GitHub Actions

```yaml
jobs:
  deploy:
    runs-on: ubuntu-latest
    environment: staging
    env:
      DATABASE_URL: ${{ secrets.STAGING_DATABASE_URL }}
      API_KEY: ${{ secrets.STAGING_API_KEY }}
    steps:
      - name: Deploy
        run: echo "Deploying to staging"
        env:
          DEPLOY_TOKEN: ${{ secrets.DEPLOY_TOKEN }}  # Step-level env var
```

### 10.3 GitHub Environments

GitHub Environments provide:
- **Secrets scoped to an environment** (different secrets for staging vs. production)
- **Protection rules** (required reviewers, wait timers)
- **Deployment history** (track what was deployed when)

```yaml
jobs:
  deploy-staging:
    runs-on: ubuntu-latest
    environment:
      name: staging
      url: https://staging.myapp.com    # Shows in the GitHub UI

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment:
      name: production
      url: https://myapp.com
```

---

## 11. Docker in CI for Integration Tests

### 11.1 Why Docker in CI?

Integration tests often need real infrastructure — databases, message queues, caches. Docker provides lightweight, disposable instances of these services.

```
Without Docker:                      With Docker:

Install SQL Server on runner ✗       Start container in seconds ✓
Manage state between runs ✗          Fresh instance per run ✓
Different versions conflict ✗        Any version, isolated ✓
Works differently on CI vs local ✗   Same container everywhere ✓
```

### 11.2 Docker Services in GitHub Actions

GitHub Actions can run Docker containers as services alongside your tests:

```yaml
jobs:
  integration-tests:
    runs-on: ubuntu-latest

    services:
      sqlserver:
        image: mcr.microsoft.com/mssql/server:2022-latest
        env:
          ACCEPT_EULA: Y
          MSSQL_SA_PASSWORD: YourStr0ng!Password
        ports:
          - 1433:1433
        options: >-
          --health-cmd "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'YourStr0ng!Password' -C -Q 'SELECT 1'"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: testuser
          POSTGRES_PASSWORD: testpassword
          POSTGRES_DB: testdb
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run integration tests
        run: dotnet test --filter "Category=Integration" --verbosity normal
        env:
          ConnectionStrings__SqlServer: "Server=localhost,1433;Database=testdb;User Id=sa;Password=YourStr0ng!Password;TrustServerCertificate=true"
          ConnectionStrings__Postgres: "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpassword"
```

### 11.3 Testcontainers in GitHub Actions

Testcontainers starts Docker containers from within your test code. This works in GitHub Actions because the `ubuntu-latest` runner has Docker pre-installed.

```csharp
// Example test using Testcontainers for SQL Server
using Testcontainers.MsSql;

public class DatabaseIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task CanConnectToDatabase()
    {
        var connectionString = _sqlContainer.GetConnectionString();
        // Use connectionString to run your integration test...
    }
}
```

The corresponding workflow is simpler because Testcontainers manages Docker itself:

```yaml
jobs:
  integration-tests:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      # Docker is already available on ubuntu-latest
      # Testcontainers will manage containers automatically

      - name: Run integration tests
        run: dotnet test --filter "Category=Integration" --verbosity normal
```

> **Discussion (5 min):** What are the trade-offs between using GitHub Actions services vs. Testcontainers? When would you prefer one approach over the other?

---

## 12. Complete CI/CD Pipeline Example

Here is a comprehensive, production-ready GitHub Actions workflow that combines all the concepts from this lecture:

```yaml
# .github/workflows/ci-cd.yml

name: CI/CD Pipeline

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

env:
  DOTNET_VERSION: '9.0.x'
  DOTNET_NOLOGO: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  # ─────────────────────────────────────────────
  # Stage 1: Build
  # ─────────────────────────────────────────────
  build:
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v5
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

  # ─────────────────────────────────────────────
  # Stage 2: Unit Tests + Coverage
  # ─────────────────────────────────────────────
  unit-tests:
    needs: build
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v5
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Run unit tests with coverage
        run: >
          dotnet test
          --filter "Category=Unit"
          --configuration Release
          --verbosity normal
          --collect:"XPlat Code Coverage"
          --results-directory ./coverage
          --logger "trx;LogFileName=unit-test-results.trx"

      - name: Install ReportGenerator
        run: dotnet tool install --global dotnet-reportgenerator-globaltool

      - name: Generate coverage report
        run: >
          reportgenerator
          -reports:./coverage/**/coverage.cobertura.xml
          -targetdir:./coverage/report
          -reporttypes:"Html;TextSummary;Cobertura"

      - name: Display coverage summary
        run: cat ./coverage/report/Summary.txt

      - name: Upload coverage report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./coverage/report/

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: unit-test-results
          path: ./coverage/**/*.trx

  # ─────────────────────────────────────────────
  # Stage 3: Integration Tests
  # ─────────────────────────────────────────────
  integration-tests:
    needs: build
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: testuser
          POSTGRES_PASSWORD: testpassword
          POSTGRES_DB: testdb
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Cache NuGet packages
        uses: actions/cache@v5
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Run integration tests
        run: >
          dotnet test
          --filter "Category=Integration"
          --configuration Release
          --verbosity normal
          --logger "trx;LogFileName=integration-test-results.trx"
          --results-directory ./test-results
        env:
          ConnectionStrings__Postgres: "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpassword"

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: integration-test-results
          path: ./test-results/**/*.trx

  # ─────────────────────────────────────────────
  # Stage 4: Deploy to Staging
  # ─────────────────────────────────────────────
  deploy-staging:
    needs: [unit-tests, integration-tests]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main' && github.event_name == 'push'
    environment:
      name: staging
      url: https://staging.myapp.com

    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Publish application
        run: dotnet publish --configuration Release --output ./publish

      - name: Deploy to staging
        run: echo "Deploy to staging environment"
        # In a real project, this would deploy to Azure, AWS, etc.

  # ─────────────────────────────────────────────
  # Stage 5: Deploy to Production (manual approval)
  # ─────────────────────────────────────────────
  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment:
      name: production
      url: https://myapp.com

    steps:
      - name: Deploy to production
        run: echo "Deploy to production environment"
```

Pipeline visualization:

```
                    ┌──────────────┐
                    │    build     │
                    └──────┬───────┘
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
     ┌──────────────┐        ┌────────────────────┐
     │  unit-tests  │        │ integration-tests  │
     │  + coverage  │        │  (with Postgres)   │
     └──────┬───────┘        └────────┬───────────┘
            │                         │
            └────────────┬────────────┘
                         ▼
               ┌──────────────────┐
               │  deploy-staging  │  (only on push to main)
               └────────┬─────────┘
                        ▼
              ┌────────────────────┐
              │ deploy-production  │  (manual approval)
              └────────────────────┘
```

---

## 13. Advanced Topics

### 13.1 Reusable Workflows

As your organization grows, you may want to share workflow logic across repositories:

```yaml
# .github/workflows/reusable-dotnet-test.yml (in a shared repository)

name: Reusable .NET Test

on:
  workflow_call:
    inputs:
      dotnet-version:
        required: false
        type: string
        default: '9.0.x'
      test-filter:
        required: false
        type: string
        default: ''

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ inputs.dotnet-version }}
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release --filter "${{ inputs.test-filter }}"
```

```yaml
# .github/workflows/ci.yml (in a consuming repository)

name: CI

on:
  push:
    branches: [ main ]

jobs:
  unit-tests:
    uses: my-org/shared-workflows/.github/workflows/reusable-dotnet-test.yml@main
    with:
      test-filter: "Category=Unit"

  integration-tests:
    uses: my-org/shared-workflows/.github/workflows/reusable-dotnet-test.yml@main
    with:
      test-filter: "Category=Integration"
```

### 13.2 Secrets Management

Never hard-code secrets in workflow files. Use GitHub Secrets:

```yaml
# Set secrets in: Repository Settings > Secrets and variables > Actions

steps:
  - name: Connect to database
    run: dotnet test
    env:
      DB_CONNECTION_STRING: ${{ secrets.DB_CONNECTION_STRING }}

  - name: Deploy
    run: ./deploy.sh
    env:
      DEPLOY_TOKEN: ${{ secrets.DEPLOY_TOKEN }}
```

| Secret Scope | Visibility | Use Case |
|---|---|---|
| **Repository secrets** | Available to all workflows in the repo | API keys, tokens |
| **Environment secrets** | Available only in a specific environment | Production DB credentials |
| **Organization secrets** | Shared across repos in an organization | Shared service tokens |

**Important:** Secrets are **not** passed to workflows triggered by pull requests from forks (security measure).

### 13.3 Workflow Security Best Practices

| Practice | Why |
|---|---|
| **Pin action versions with SHA** | Prevent supply-chain attacks: `uses: actions/checkout@8ade135...` |
| **Use `permissions` to limit GITHUB_TOKEN** | Principle of least privilege |
| **Don't use `pull_request_target` with checkout** | Prevents code injection from forks |
| **Audit third-party actions** | Review source before trusting |

```yaml
# Limit permissions
permissions:
  contents: read       # Only read access to code
  pull-requests: write # Can comment on PRs

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      # Pin to a specific commit SHA for security
      - uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2
```

---

## 14. Practical Exercise

### Task: Set Up a CI/CD Pipeline for a .NET Project

Create a GitHub Actions workflow for a .NET test project that includes:

1. **Build job**: Restore, build, cache NuGet packages
2. **Test job**: Run xUnit tests, collect coverage with Coverlet
3. **Report job**: Generate coverage report, upload as artifact
4. **Quality gate**: Fail the pipeline if coverage drops below 75%

**Starter workflow structure:**

```yaml
# .github/workflows/ci.yml
name: CI Pipeline

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    # TODO: Implement build steps with caching

  test:
    needs: build
    # TODO: Run tests with coverage collection

  quality-gate:
    needs: test
    # TODO: Check coverage threshold (75%)
    # TODO: Upload coverage report as artifact
```

**Bonus challenges:**
- Add a matrix build for .NET 9 and .NET 10
- Add a step that posts the coverage summary as a PR comment
- Add branch protection rules requiring the CI pipeline to pass

> **Discussion (15 min):** Walk through your workflow design. What would happen if the test step fails? What if coverage is at 74%? How would you debug a failing workflow?

---

## 15. Summary

### Key Takeaways

1. **CI/CD automates the feedback loop** — code changes are built, tested, and analyzed automatically on every commit, catching issues within minutes
2. **Continuous Integration** means merging frequently and running automated tests on every change; **Continuous Delivery** means every change is deployable; **Continuous Deployment** means every passing change is deployed automatically
3. **GitHub Actions** provides CI/CD directly in GitHub with workflows defined in YAML — triggers, jobs, steps, matrix builds, caching, and artifacts
4. **Code coverage in CI** (Coverlet + ReportGenerator) provides consistent quality metrics and can enforce minimum thresholds as quality gates
5. **Branch protection rules** prevent unreviewed or untested code from reaching protected branches
6. **Test management** encompasses test plans, test case management, defect lifecycle tracking, and metrics-based reporting
7. **Quality gates** define minimum conditions (test pass rate, coverage, code review) that must be met before progressing to the next stage
8. **Flaky tests** erode CI trust — detect them early, quarantine if needed, fix the root cause, never ignore them
9. **Docker in CI** (via services or Testcontainers) provides real infrastructure for integration tests in a clean, reproducible way
10. **Risk-based testing** focuses effort where failures are most likely and most impactful

### Quick Reference: Essential YAML Patterns

```yaml
# Trigger on push and PR
on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

# Cache NuGet
- uses: actions/cache@v5
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}

# Run tests with coverage
- run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Upload artifact
- uses: actions/upload-artifact@v4
  if: always()
  with:
    name: results
    path: ./coverage/

# Job dependency
jobs:
  deploy:
    needs: [build, test]

# Matrix build
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest]
    dotnet-version: ['9.0.x', '10.0.x']
```

---

## References and Further Reading

- **GitHub Actions Documentation** — https://docs.github.com/en/actions
- **GitHub Actions for .NET** — https://docs.github.com/en/actions/use-cases-and-examples/building-and-testing/building-and-testing-net
- **Coverlet Documentation** — https://github.com/coverlet-coverage/coverlet
- **ReportGenerator** — https://github.com/danielpalme/ReportGenerator
- **Testcontainers for .NET** — https://dotnet.testcontainers.org/
- **"Continuous Delivery"** — Jez Humble, David Farley (Addison-Wesley, 2010)
- **"Accelerate: Building and Scaling High Performing Technology Organizations"** — Nicole Forsgren, Jez Humble, Gene Kim (IT Revolution, 2018)
- **ISTQB Foundation Level Syllabus** (v4.0, 2023) — Chapters 5-6
- **DORA Metrics** — https://dora.dev/
- **Martin Fowler: Continuous Integration** — https://martinfowler.com/articles/continuousIntegration.html
- **GitHub Branch Protection Rules** — https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-a-branch-protection-rule
