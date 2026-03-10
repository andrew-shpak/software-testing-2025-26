# Лабораторна 12 — CI/CD пайплайни для тестування

## Мета

Налаштувати автоматизовані пайплайни тестування за допомогою GitHub Actions. Сконфігурувати виконання тестів, звітність про покриття коду та якісні шлюзи, що запускаються при кожному push та pull request.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що у вас є:

- Обліковий запис GitHub із репозиторієм, до якого ви можете робити push
- Локально встановлений .NET 10 SDK (або новіший)
- Працююче .NET-рішення з тестами з будь-якої попередньої лабораторної
- Базове знайомство із синтаксисом YAML
- Розуміння гілкування в Git (створення гілок, pull request-и)
- Встановлений та налаштований `git` CLI

Рекомендований досвід:

- [Швидкий старт з GitHub Actions](https://docs.github.com/en/actions/quickstart) -- прочитайте, якщо ви ніколи не використовували GitHub Actions
- Виконана хоча б одна лабораторна з працюючими тестами xUnit (наприклад, Лабораторна 3 або Лабораторна 5)

## Ключові поняття

### Що таке CI/CD?

**Неперервна інтеграція (CI)** — це практика автоматичної збірки, тестування та валідації коду щоразу, коли розробник робить push змін. **Неперервна доставка/розгортання (CD)** розширює CI шляхом автоматичного розгортання перевіреного коду на staging або production.

У цій лабораторній ми зосереджуємося на частині **CI**: автоматичному запуску тестів.

### Навіщо автоматизувати тестування?

| Без CI | З CI |
|--------|------|
| "У мене на машині працює" | Тести виконуються в чистому, відтворюваному середовищі |
| Тести пропускаються перед злиттям | Тести повинні пройти для злиття |
| Помилки виявляються пізно (після злиття) | Помилки виявляються рано (на PR) |
| Покриття невідоме | Покриття відстежується та контролюється |
| Ручна перевірка якості | Автоматизовані якісні шлюзи |

### Термінологія GitHub Actions

| Термін | Значення |
|--------|----------|
| **Workflow** | YAML-файл у `.github/workflows/`, що визначає автоматизацію |
| **Job** | Набір кроків, що виконуються на одному runner-і |
| **Step** | Одна команда або action у межах job |
| **Runner** | Віртуальна машина, що виконує job (`ubuntu-latest`, `windows-latest` тощо) |
| **Action** | Багаторазовий блок коду (наприклад, `actions/checkout@v6`) |
| **Trigger** | Подія, що запускає workflow (`push`, `pull_request`, `schedule` тощо) |
| **Artifact** | Файл, створений workflow і завантажений для подальшого доступу |
| **Matrix** | Стратегія запуску одного й того ж job з різними конфігураціями |

### Як працює покриття коду

**Coverlet** інструментує ваші .NET-збірки для відстеження, які рядки коду виконуються під час тестів. Після завершення тестів він генерує звіт про покриття, що показує:

- **Покриття рядків**: відсоток виконаних рядків коду
- **Покриття гілок**: відсоток пройдених умовних розгалужень
- **Покриття методів**: відсоток викликаних методів

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

## Інструменти

- CI/CD: [GitHub Actions](https://docs.github.com/en/actions)
- Покриття: [Coverlet](https://github.com/coverlet-coverage/coverlet) + [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- Якість коду: [SonarCloud](https://sonarcloud.io/) (опціонально)

## Налаштування

Створіть репозиторій на GitHub та завантажте .NET-рішення з тестами з будь-якої попередньої лабораторної.

### Початкова структура репозиторію

Ваш репозиторій повинен виглядати приблизно так перед початком:

```
my-lab-project/
├── .github/
│   └── workflows/
│       └── ci.yml          <-- ви створите цей файл
├── src/
│   └── MyApp/
│       └── ...
├── tests/
│   └── MyApp.Tests/
│       └── ...
├── MyApp.sln
└── README.md
```

### Додавання пакетів покриття

Переконайтеся, що ваш тестовий проект включає Coverlet collector:

```bash
dotnet add tests/MyApp.Tests package coverlet.collector
```

## Завдання

### Завдання 1 — Базовий CI-пайплайн

Створіть `.github/workflows/ci.yml`:

1. Тригер на `push` у `main` та на всі події `pull_request`
2. Використовуйте runner `ubuntu-latest`
3. Кроки:
   - Checkout коду
   - Налаштування .NET SDK
   - Відновлення залежностей
   - Збірка рішення
   - Запуск усіх тестів через `dotnet test`
4. Переконайтеся, що пайплайн завершується невдачею, якщо будь-який тест не пройшов

Приклад структури:

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

> **Підказка:** Прапорці `--no-restore` та `--no-build` уникають зайвої роботи. Кожен крок базується на попередньому: restore завантажує пакети, build компілює код, test запускає скомпільовані тести. Без цих прапорців кожен крок повторював би всю попередню роботу.

#### Перевірка вашого пайплайну

Після push файлу `ci.yml` перейдіть до вашого репозиторію на GitHub та натисніть вкладку **Actions**. Ви повинні побачити ваш workflow у виконанні. Зелена галочка означає, що всі кроки пройшли успішно. Червоний хрестик означає, що щось не вдалося — натисніть на нього, щоб переглянути логи.

### Завдання 2 — Якісний шлюз покриття коду

Розширте пайплайн для:

1. Збору покриття за допомогою Coverlet:
   ```yaml
   - name: Test with coverage
     run: dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
   ```
2. Генерації HTML-звіту за допомогою ReportGenerator
3. Завантаження звіту покриття як артефакту GitHub Actions
4. Додайте поріг покриття — пайплайн повинен завершитися невдачею, якщо покриття падає нижче 80%
5. Налаштуйте пайплайн для запуску також на події `pull_request`

#### Повний приклад пайплайну покриття

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

> **Підказка:** Крок перевірки порогу покриття аналізує відсоток покриття з текстового звіту та завершується невдачею, якщо він нижче 80%. Ви можете налаштувати цей поріг відповідно до потреб вашого проекту. На практиці команди часто починають з нижчого порогу та підвищують його з часом.

#### Формати виводу покриття

| Формат | Призначення |
|--------|-------------|
| `Cobertura` | Машиночитаний XML; вхідні дані для ReportGenerator та інших інструментів |
| `Html` | Зручний для читання звіт з деталізацією по файлах |
| `TextSummary` | Швидке текстове зведення для CI-логів |
| `Badges` | SVG-значки для відображення у README |
| `lcov` | Сумісний з багатьма платформами якості коду |

### Завдання 3 — Матричне тестування

Налаштуйте пайплайн для тестування у декількох конфігураціях:

```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet-version: ['9.0.x', '10.0.x']
```

1. Запускайте тести на всіх комбінаціях ОС + версія .NET
2. Переконайтеся, що всі матричні job-и повинні пройти для успішного завершення пайплайну

#### Повний приклад матричного job

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

#### Довідник поведінки матриці

| Налаштування | Ефект |
|-------------|-------|
| `fail-fast: true` (за замовчуванням) | Скасувати всі матричні job-и, якщо будь-який з них не вдався |
| `fail-fast: false` | Виконати всі матричні job-и, навіть якщо деякі не вдалися |
| `exclude` | Видалити конкретні комбінації з матриці |
| `include` | Додати конкретні комбінації до матриці |

> **Підказка:** Встановіть `fail-fast: false` під час розробки, щоб бачити, які саме комбінації ОС/версій не вдалися. У продуктивних пайплайнах `fail-fast: true` економить хвилини CI, зупиняючись раніше.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Базовий CI-пайплайн |
| Завдання 2 — Якісний шлюз покриття |
| Завдання 3 — Матричне тестування |

## Здача роботи

- URL репозиторію на GitHub з працюючим пайплайном

## Посилання

- [Документація GitHub Actions](https://docs.github.com/en/actions) -- офіційний довідник усіх можливостей GitHub Actions
- [Синтаксис Workflow GitHub Actions](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions) -- довідник синтаксису YAML
- [actions/checkout](https://github.com/actions/checkout) -- action для checkout, що використовується у кожному workflow
- [actions/setup-dotnet](https://github.com/actions/setup-dotnet) -- action для налаштування .NET SDK
- [actions/upload-artifact](https://github.com/actions/upload-artifact) -- завантаження артефактів збірки
- [dorny/test-reporter](https://github.com/dorny/test-reporter) -- публікація результатів тестів у PR
- [Репозиторій Coverlet на GitHub](https://github.com/coverlet-coverage/coverlet) -- бібліотека покриття коду для .NET
- [Документація Coverlet: Інтеграція з MSBuild](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md) -- розширена конфігурація Coverlet
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator) -- конвертація файлів покриття в HTML-звіти
- [Правила захисту гілок GitHub](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-a-branch-protection-rule) -- налаштування якісних шлюзів
- [Довідник CLI dotnet test](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test) -- всі прапорці та опції для `dotnet test`
- [Довідник CLI dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) -- інструмент контролю стилю коду
