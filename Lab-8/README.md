# Лабораторна 8 — CI/CD з GitHub Actions: міграції, якість гілок, тести та навантаження

## Мета

Побудувати повноцінний CI/CD-конвеєр на GitHub Actions для ASP.NET Core API з Entity Framework Core. Конвеєр має автоматично перевіряти імена гілок, виконувати міграції БД, запускати збірку з інтеграційними тестами на Testcontainers та проводити performance-тести з k6.

**Тривалість:** 60 хвилин

## Передумови

- GitHub-акаунт та публічний (або приватний з увімкненими Actions) репозиторій
- Встановлений .NET 10 SDK локально — щоб перевіряти зміни перед push
- Встановлений та запущений Docker (для локального прогону Testcontainers перед push)
- Виконана Лабораторна 6 — для цієї лабораторної використовується той самий API (`Lectures.Api`) з PostgreSQL та EF Core
- Базове знайомство з YAML
- Базове знайомство з Git (гілки, pull request, merge)
- Повний доступ до налаштувань репозиторію (Settings → Actions, Settings → Branches) — без цього неможливо перевірити захист гілок

## Ключові концепції

| Концепція | Опис |
|-----------|------|
| **Workflow** | YAML-файл у `.github/workflows/*.yml`, що описує автоматизований процес. Триггерується подіями в репозиторії. |
| **Job** | Набір кроків, що виконуються на одному раннері. Jobs можуть виконуватись паралельно або послідовно (через `needs`). |
| **Step** | Одна операція всередині job: shell-команда (`run`) або переви­користовувана дія (`uses`). |
| **Trigger** | Подія, що запускає workflow: `push`, `pull_request`, `workflow_dispatch`, `schedule`. |
| **Runner** | Віртуальна машина, що виконує job. `ubuntu-latest` має попередньо встановлений Docker, .NET SDK та GitHub CLI. |
| **Testcontainers in CI** | Бібліотека, що запускає Docker-контейнери з коду тесту. На `ubuntu-latest` працює, бо Docker вже встановлений. |
| **Artifact** | Файл або директорія, що зберігається після завершення job (результати тестів, SQL-скрипти, HTML-звіти). Доступний для завантаження з UI Actions. |
| **Branch protection** | Правило, що блокує push/merge у захищену гілку, доки не пройдуть обрані status checks (наприклад, `build-and-test`). |
| **Status check** | Результат job, що відображається в PR. Ім'я job стає ім'ям status check. |
| **Idempotent migration script** | SQL-скрипт, згенерований `dotnet ef migrations script --idempotent`, який можна безпечно застосовувати кілька разів на одну і ту ж БД. |
| **workflow_dispatch** | Триггер, що дозволяє запускати workflow вручну з UI GitHub із параметрами. |

## Інструменти

- CI/CD: [GitHub Actions](https://docs.github.com/en/actions)
- Мова: C# / ASP.NET Core Web API (система, що тестується — `Lectures.Api` з Лабораторної 6)
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) + інструменти `dotnet-ef`
- База даних: PostgreSQL 18
- Інтеграційні тести: [Testcontainers для .NET](https://dotnet.testcontainers.org/) + [xUnit v3](https://xunit.net/)
- Performance-тести: [k6](https://k6.io/) (встановлюється у workflow через [`grafana/setup-k6-action`](https://github.com/grafana/setup-k6-action))

## Налаштування

Переконайтеся, що у репозиторії існує директорія `.github/workflows/`. Якщо ні — створіть її:

```bash
mkdir -p .github/workflows
```

Уся лабораторна виконується в цій директорії. Система, що тестується, — це ASP.NET Core API з Лабораторної 6 (шлях `Lecture-2/Lab6/demo`). Ви не пишете C#-код; ви пишете YAML, що запускає вже існуючий C#-код.

> **Підказка**: Кожен workflow-файл спочатку закомітьте на окрему feature-гілку, щоб переконатися, що тригери працюють. Помилки у YAML видно лише після push.

## Завдання

### Завдання 1 — Перевірка імені гілки

Створіть workflow `branch-name.yml`, який запускається на кожному `push`, окрім `main`, та провалює білд, якщо ім'я гілки не відповідає погодженій команді конвенції.

#### Вимоги

- Workflow запускається на `push` до будь-якої гілки, окрім `main`
- Один job на `ubuntu-latest`
- Один крок, що читає `GITHUB_REF_NAME` і перевіряє його bash-регулярним виразом
- Якщо регулярний вираз не збігається — крок має завершитись `exit 1` з повідомленням `::error::...`

#### Приклад каркаса

```yaml
name: Branch Name Check

on:
  push:
    branches-ignore:
      - main

jobs:
  branch-name:
    runs-on: ubuntu-latest
    steps:
      - name: Check branch name
        run: |
          BRANCH="${GITHUB_REF_NAME}"
          # TODO: додайте регулярний вираз вашої конвенції
          PATTERN="^(...)/.+$"
          if [[ ! "$BRANCH" =~ $PATTERN ]]; then
            echo "::error::Branch name '$BRANCH' does not match required pattern"
            exit 1
          fi
          echo "Branch name '$BRANCH' is valid."
```

### Завдання 2 — Збірка та тестування з Testcontainers

Створіть workflow `ci.yml`, що запускається на `push` у `main` і збирає та тестує рішення з Лабораторної 6. Інтеграційні тести використовують Testcontainers для запуску PostgreSQL.

#### Вимоги

- Workflow запускається на `push` у `main`
- Один job `build-and-test` на `ubuntu-latest`
- Кроки: `actions/checkout@v4`, `actions/setup-dotnet@v4` (.NET 10), `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build --verbosity normal`

#### Приклад каркаса

```yaml
name: CI

on:
  push:
    branches:
      - main

jobs:
  build-and-test:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: Lecture-2/Lab6/demo

    steps:
      - uses: actions/checkout@v4

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

#### Додатково — workflow `pr-tests.yml`

Продублюйте ту саму логіку з тригером `pull_request` у `main`. Це той самий job, але запускається до merge, щоб блокувати PR з провальними тестами.

```yaml
on:
  pull_request:
    branches:
      - main
```

#### Перевірка

1. Вивчіть вивід job у Actions UI: чи видно, що Testcontainers стартує PostgreSQL-контейнер під час `dotnet test`?
2. Свідомо зламайте один тест → push → job має бути червоним у PR → перевірте, що поява failed status check блокує кнопку Merge

#### Захист гілки main

Після успішного зеленого білду у **Settings → Branches → Branch protection rules** додайте правило для `main`:

- Require a pull request before merging ✓
- Require status checks to pass before merging ✓
- Обрати статус-чек `build-and-test` (з `pr-tests.yml`)

Підтвердіть, що прямий push у `main` тепер заборонений.

**Мінімум для Завдання 2**: 2 workflow-файли (`ci.yml` + `pr-tests.yml`) + скріншот налаштованого branch protection.

### Завдання 3 — Валідація міграцій EF Core

Створіть workflow `migration.yml`, що на pull request перевіряє, чи міграції EF Core можна чисто застосувати на свіжій PostgreSQL-БД, та зберігає ідемпотентний SQL-скрипт як артефакт.

#### Вимоги

- Workflow запускається на `pull_request` у `main`
- Встановлює інструменти: `dotnet tool install --global dotnet-ef`
- Перевіряє, що модель синхронізована з міграціями: `dotnet ef migrations has-pending-model-changes`
- Застосовує міграції до контейнерної БД: `dotnet ef database update`
- Генерує ідемпотентний SQL-скрипт: `dotnet ef migrations script --idempotent --output migrations.sql`
- Завантажує `migrations.sql` як артефакт

#### Чому ідемпотентний скрипт

Прод-БД оновлюють не через `dotnet ef database update`, а через SQL. Ідемпотентний скрипт можна застосувати кілька разів поспіль — він пропустить уже застосовані міграції. Це безпечно для rerun CD-пайплайну та для середовищ, де тягнуть кілька версій.

#### Приклад каркаса

```yaml
name: Migration

on:
  pull_request:
    branches:
      - main

jobs:
  validate-migrations:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: Lecture-2/Lab6/demo

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install EF Core tools
        run: dotnet tool install --global dotnet-ef

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Check for pending model changes
        run: dotnet ef migrations has-pending-model-changes --project src/Lectures.Api --startup-project src/Lectures.Api

      - name: Apply migrations
        env:
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5432;Database=lectures;Username=postgres;Password=postgres"
        run: dotnet ef database update --project src/Lectures.Api --startup-project src/Lectures.Api

      - name: Generate idempotent SQL script
        run: dotnet ef migrations script --idempotent --project src/Lectures.Api --startup-project src/Lectures.Api --output migrations.sql

      - name: Upload migration script
        uses: actions/upload-artifact@v4
        with:
          name: migration-sql
          path: Lecture-2/Lab6/demo/migrations.sql
```

#### Перевірка

1. Змініть модель EF Core (наприклад, додайте поле в сутність), але **не** додавайте нову міграцію → PR → крок `Check for pending model changes` має провалитись → ідеально, CI впіймав забуту міграцію
2. Додайте міграцію локально (`dotnet ef migrations add AddField`) → PR → workflow зелений → у вкладці Summary → Artifacts завантажте `migration-sql` і огляньте SQL

**Мінімум для Завдання 3**: 1 workflow-файл + демонстрація двох PR (один із забутою міграцією, один із застосованою).

### Завдання 4 — Performance-тестування з k6

Створіть workflow `k6.yml`, який на pull request або вручну стартує API і проганяє k6-сценарій (smoke/load/stress/spike) проти нього.

#### Вимоги

- Два тригери:
  - `pull_request` у `main`
  - `workflow_dispatch` з `inputs.test-type` як `choice` (`smoke`, `load`, `stress`, `spike`) зі значенням за замовчуванням `smoke`
- Встановити `grafana/setup-k6-action@v1`
- Запустити API у фоні: `dotnet run --project src/Lectures.Api --no-build &`
- Чекати готовності API через цикл з `curl -sf http://localhost:5067/health/ready`
- Визначити тип тесту:
  - Для `workflow_dispatch` — з `inputs.test-type`
  - Для `pull_request` — завжди `smoke` (швидкий тест, не блокує PR надовго)
- Запустити `k6 run` відповідним скриптом, зберегти `summary-export=results.json`
- Завантажити `results.json` як артефакт

#### Приклад каркаса (ключові фрагменти)

```yaml
name: k6 Performance Tests

on:
  pull_request:
    branches:
      - main

  workflow_dispatch:
    inputs:
      test-type:
        description: 'k6 test type to run'
        required: true
        default: 'smoke'
        type: choice
        options:
          - smoke
          - load
          - stress
          - spike

jobs:
  k6:
    runs-on: ubuntu-latest

    defaults:
      run:
        working-directory: Lecture-2/Lab6/demo

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Setup k6
        uses: grafana/setup-k6-action@v1

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Start API
        env:
          ASPNETCORE_URLS: http://localhost:5067
          ConnectionStrings__DefaultConnection: "Host=localhost;Port=5432;Database=lectures;Username=postgres;Password=postgres"
        run: dotnet run --project src/Lectures.Api --no-build &

      - name: Wait for API readiness
        run: |
          for i in $(seq 1 30); do
            if curl -sf http://localhost:5067/health/ready > /dev/null 2>&1; then
              echo "API is ready!"
              exit 0
            fi
            sleep 2
          done
          echo "API failed to become ready"
          exit 1

      - name: Determine test type
        id: test-type
        run: |
          if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
            echo "type=${{ inputs.test-type }}" >> "$GITHUB_OUTPUT"
          else
            echo "type=smoke" >> "$GITHUB_OUTPUT"
          fi

      - name: Run k6 ${{ steps.test-type.outputs.type }} test
        working-directory: Lecture-2/Lab6/tests/Lab6.Api.Tests.Performance
        run: k6 run -e BASE_URL=http://localhost:5067 scripts/${{ steps.test-type.outputs.type }}-test.ts --summary-export=results.json

      - name: Upload k6 results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: k6-${{ steps.test-type.outputs.type }}-results
          path: Lecture-2/Lab6/tests/Lab6.Api.Tests.Performance/results.json
```

#### Перевірка

1. Відкрийте PR, що змінює код API → workflow запускається з `test-type=smoke`
2. У вкладці Actions натисніть **Run workflow** → виберіть `load` → workflow стартує вручну
3. Завантажте артефакт `k6-smoke-results` → огляньте `results.json`

**Мінімум для Завдання 4**: 1 workflow-файл + thresholds у smoke-скрипті + запис в `REPORT.md` із обраними SLO.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Workflow перевірки імені гілки + обґрунтування конвенції |
| Завдання 2 — CI та PR workflows + налаштований branch protection |
| Завдання 3 — Workflow міграцій з services, ідемпотентним SQL-скриптом як артефактом |
| Завдання 4 — k6 workflow з двома тригерами, порогами та артефактом результатів |
| `REPORT.md` з рішеннями, обґрунтуваннями та скріншотами запусків |
| Усі workflows зелені у репозиторії принаймні на одному push/PR |

## Здача роботи

- 4 workflow-файли у `.github/workflows/`: `branch-name.yml`, `ci.yml` (+ `pr-tests.yml`), `migration.yml`, `k6.yml`
- `thresholds` у `scripts/smoke-test.ts`
- `REPORT.md` з:
  - Обраною конвенцією імен гілок та обґрунтуванням
  - Скріншотом Branch Protection
  - Посиланнями на успішні запуски workflows (Actions UI)
  - Обраними k6 SLO та обґрунтуванням
- Один merged PR, що пройшов усі checks

> **Підказка**: Для діагностики зламаного workflow — увімкніть [debug logs](https://docs.github.com/en/actions/learn-github-actions/variables#configuring-default-environment-variables-for-a-repository): додайте `ACTIONS_STEP_DEBUG=true` як secret і перезапустіть job.

## Посилання

- [GitHub Actions Documentation](https://docs.github.com/en/actions) — офіційна документація
- [Workflow syntax reference](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions) — повний довідник YAML-синтаксису
- [Events that trigger workflows](https://docs.github.com/en/actions/reference/events-that-trigger-workflows) — усі тригери та їх параметри
- [`actions/checkout`](https://github.com/actions/checkout)
- [`actions/setup-dotnet`](https://github.com/actions/setup-dotnet)
- [`actions/upload-artifact`](https://github.com/actions/upload-artifact)
- [`grafana/setup-k6-action`](https://github.com/grafana/setup-k6-action) — встановлення k6 у workflow
- [EF Core — Applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) — `dotnet ef database update`, `script --idempotent`
- [EF Core — has-pending-model-changes](https://learn.microsoft.com/en-us/ef/core/cli/dotnet#dotnet-ef-migrations-has-pending-model-changes)
- [Testcontainers для .NET](https://dotnet.testcontainers.org/) — робота з Docker із коду тесту
- [k6 — Thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/) — визначення SLO
- [Managing a branch protection rule](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-a-branch-protection-rule)
- [Conventional Commits](https://www.conventionalcommits.org/) — популярна конвенція для імен гілок та commit messages
- Лекція 6 — CI/CD та управління тестуванням (у цьому курсі)
