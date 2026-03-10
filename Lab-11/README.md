# Лабораторна 11 — Тестування сервісів: наскрізне тестування (End-to-End)

## Мета

Написати наскрізні тести, які перевіряють повні користувацькі сценарії у веб-застосунку, включаючи взаємодію з інтерфейсом, навігацію, відправку форм та API-запити.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що у вас є:

- Встановлений .NET 10 SDK (або новіший)
- Знайомство з ASP.NET Core Razor Pages або MVC
- Виконані попередні лабораторні з модульного та інтеграційного тестування
- Базове розуміння HTML-форм, CSS-селекторів та структури DOM
- PowerShell (для скрипта встановлення браузерів Playwright)

Після додавання NuGet-пакета `Microsoft.Playwright` необхідно встановити бінарні файли браузерів:

```bash
# Спочатку зберіть тестовий проект, щоб скрипт playwright.ps1 став доступним
dotnet build Lab11.Tests

# Встановіть браузери (Chromium, Firefox, WebKit)
pwsh Lab11.Tests/bin/Debug/net10.0/playwright.ps1 install
```

> **Примітка:** У CI-середовищах вам також може знадобитися встановити системні залежності. Виконайте `pwsh playwright.ps1 install-deps`, щоб встановити необхідні бібліотеки на рівні ОС.

## Ключові поняття

### Що таке наскрізні тести?

Наскрізні (E2E) тести перевіряють весь стек застосунку, імітуючи реальну поведінку користувача у браузері. Вони взаємодіють з інтерфейсом саме так, як це робив би користувач: натискають кнопки, заповнюють форми, переходять між сторінками та перевіряють результати на екрані.

### Піраміда тестування та E2E-тести

```
        /  E2E  \        <-- Мало, повільні, висока впевненість
       /----------\
      / Інтеграційні \    <-- Помірна кількість, середня швидкість
     /----------------\
    /  Модульні тести   \ <-- Багато, швидкі, ізольовані
   /--------------------\
```

E2E-тести знаходяться на вершині піраміди тестування. Вони:
- **Найповільніші** у виконанні (реальний браузер, реальний рендеринг)
- **Найреалістичніші** (найближче до реального користувацького досвіду)
- **Найкрихкіші** (чутливі до змін інтерфейсу, проблем із таймінгом)
- **Найцінніші** для виявлення інтеграційних проблем у всьому стеку

Використовуйте їх помірно для критичних користувацьких сценаріїв.

### Чому Playwright?

Playwright — це сучасна бібліотека автоматизації браузерів, яка вирішує багато проблем попередніх інструментів (Selenium, Puppeteer):

| Функція | Playwright | Selenium |
|---------|-----------|----------|
| Автоочікування | Вбудоване (чекає, поки елементи стануть доступними для дій) | Потрібні ручні очікування |
| Підтримка браузерів | Chromium, Firefox, WebKit | Усі основні браузери через драйвери |
| Швидкість | Швидкий (пряма комунікація через протокол) | Повільніший (протокол WebDriver) |
| Підтримка .NET | Першокласна (`Microsoft.Playwright`) | Через `Selenium.WebDriver` |
| Налагодження | Переглядач трасувань, codegen, інспектор | Обмежені вбудовані інструменти |

### Шаблон Page Object Model (POM)

Page Object Model — це шаблон проектування, який створює рівень абстракції над безпосередніми взаємодіями зі сторінкою. Кожна сторінка (або компонент) вашого застосунку отримує власний клас, який інкапсулює селектори та дії.

**Без POM** (крихкий, дубльовані селектори):
```csharp
await page.FillAsync("#Title", "My Task");
await page.FillAsync("#Description", "Details");
await page.ClickAsync("input[type='submit']");
```

**З POM** (підтримуваний, багаторазовий):
```csharp
var createPage = new TaskCreatePage(page);
await createPage.FillTitle("My Task");
await createPage.FillDescription("Details");
await createPage.Submit();
```

Якщо селектор змінюється, ви виправляєте його в одному місці, а не в десятках тестів.

## Інструменти

- Мова: C#
- E2E-фреймворк: [Playwright for .NET](https://playwright.dev/dotnet/)
- Тестовий фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Тестований застосунок: ASP.NET Core MVC або Razor Pages

## Налаштування

```bash
dotnet new sln -n Lab11
dotnet new webapp -n Lab11.WebApp
dotnet new classlib -n Lab11.Tests
dotnet sln add Lab11.WebApp Lab11.Tests
dotnet add Lab11.Tests reference Lab11.WebApp
dotnet add Lab11.Tests package xunit.v3
dotnet add Lab11.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab11.Tests package Microsoft.Playwright
dotnet add Lab11.Tests package Shouldly

# Встановіть браузери Playwright
pwsh Lab11.Tests/bin/Debug/net10.0/playwright.ps1 install
```

## Завдання

### Завдання 1 — Створення тестованого застосунку

Створіть простий веб-застосунок для управління завданнями з Razor Pages:

- `/` — Головна сторінка зі списком завдань
- `/Tasks/Create` — Форма створення нового завдання (Title, Description, DueDate, Priority)
- `/Tasks/Edit/{id}` — Форма редагування завдання
- `/Tasks/Details/{id}` — Сторінка деталей завдання
- `/Tasks/Delete/{id}` — Сторінка підтвердження видалення
- Включіть валідацію форм (обов'язкові поля, формат дати)

#### Приклад моделі завдання

```csharp
public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }

    [Required]
    public Priority Priority { get; set; } = Priority.Medium;
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}
```

> **Підказка:** Використовуйте список у пам'яті або EF Core InMemory провайдер для зберігання. Мета — тести, а не продуктивна база даних. Тримайте застосунок простим.

### Завдання 2 — Наскрізні тести з Playwright

Напишіть E2E-тести, що охоплюють повні користувацькі сценарії:

1. **Сценарій створення завдання**:
   - Перейдіть на головну сторінку
   - Натисніть "Create New Task"
   - Заповніть поля форми
   - Відправте форму
   - Перевірте перенаправлення на список завдань
   - Перевірте, що нове завдання з'явилося у списку

2. **Сценарій редагування завдання**:
   - Перейдіть до деталей завдання
   - Натисніть "Edit"
   - Змініть поля
   - Збережіть зміни
   - Перевірте, що зміни збережено

3. **Сценарій валідації**:
   - Спробуйте відправити порожню форму
   - Перевірте, що з'являються повідомлення про помилки валідації
   - Заповнюйте обов'язкові поля по одному
   - Перевірте, що повідомлення про помилки зникають

#### Запуск застосунку для тестів

Потрібно запустити веб-застосунок перед виконанням Playwright-тестів. Один із підходів — запустити застосунок у тестовому фікстурі:

```csharp
public class WebAppFixture : IAsyncLifetime
{
    private WebApplication? _app;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Збірка та запуск веб-застосунку
        var builder = WebApplication.CreateBuilder(new[] { "--urls", "http://localhost:5180" });
        // ... налаштування сервісів ...
        _app = builder.Build();
        // ... налаштування middleware ...
        await _app.StartAsync();
        BaseUrl = "http://localhost:5180";
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.StopAsync();
    }
}
```

#### Приклад E2E-тесту

```csharp
public class TaskWorkflowTests : IClassFixture<WebAppFixture>, IAsyncLifetime
{
    private readonly WebAppFixture _fixture;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    public TaskWorkflowTests(WebAppFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var playwright = await Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
    }

    [Fact]
    public async Task CreateTask_WithValidData_ShouldAppearInListAsync()
    {
        // Перехід на головну сторінку
        await _page.GotoAsync(_fixture.BaseUrl);
        await _page.GetByRole(AriaRole.Link, new() { Name = "Create New Task" }).ClickAsync();

        // Заповнення форми
        await _page.GetByLabel("Title").FillAsync("Buy groceries");
        await _page.GetByLabel("Description").FillAsync("Milk, eggs, bread");
        await _page.GetByLabel("Due Date").FillAsync("2026-03-01");
        await _page.GetByLabel("Priority").SelectOptionAsync("High");

        // Відправка
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Перевірка перенаправлення та наявності завдання у списку
        await _page.WaitForURLAsync($"{_fixture.BaseUrl}/**");
        var taskInList = _page.GetByText("Buy groceries");
        await Expect(taskInList).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreateTask_WithEmptyTitle_ShouldShowValidationAsync()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Tasks/Create");

        // Відправка без заповнення обов'язкових полів
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Повинно з'явитися повідомлення про помилку валідації
        var validationMessage = _page.GetByText("The Title field is required");
        await Expect(validationMessage).ToBeVisibleAsync();
    }
}
```

> **Підказка:** Playwright має вбудоване автоочікування. Методи `ClickAsync`, `FillAsync` та `GetByRole` автоматично чекають, поки елементи стануть доступними для дій. Вам рідко потрібні явні виклики `Task.Delay` або `WaitForTimeout`. Якщо ви додаєте ручні затримки, перегляньте свій підхід — використовуйте `WaitForURLAsync`, `WaitForSelectorAsync` або assertion-и Playwright через `Expect`.

#### Зведена таблиця очікуваної поведінки

| Сценарій | Кроки | Очікуваний результат |
|----------|-------|---------------------|
| Створення завдання | Заповнити форму, відправити | Завдання з'являється у списку, перенаправлення на головну |
| Створення порожнього завдання | Відправити порожню форму | Показані повідомлення валідації, без перенаправлення |
| Редагування завдання | Змінити поля, зберегти | Оновлені значення видно на сторінці деталей |
| Видалення завдання | Підтвердити видалення | Завдання видалено зі списку |
| Навігація | Натиснути посилання | Завантажуються правильні сторінки, заголовки відповідають |

### Завдання 3 — Розширені шаблони E2E

Реалізуйте **Page Object Model** — створіть класи сторінок для кожної сторінки:

```csharp
public class TaskListPage
{
    private readonly IPage _page;
    public async Task<TaskCreatePage> ClickCreateNew() { ... }
    public async Task<int> GetTaskCount() { ... }
    public async Task<bool> HasTask(string title) { ... }
}
```

Рефакторіть ваші тести із Завдання 2, щоб використовувати ці об'єкти сторінок замість безпосередніх селекторів.

#### Приклад Page Object Model

```csharp
public class TaskListPage
{
    private readonly IPage _page;

    public TaskListPage(IPage page) => _page = page;

    public async Task NavigateAsync(string baseUrl)
    {
        await _page.GotoAsync(baseUrl);
    }

    public async Task<TaskCreatePage> ClickCreateNewAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Create New Task" }).ClickAsync();
        return new TaskCreatePage(_page);
    }

    public async Task<int> GetTaskCountAsync()
    {
        var rows = _page.Locator("table tbody tr");
        return await rows.CountAsync();
    }

    public async Task<bool> HasTaskAsync(string title)
    {
        var cell = _page.GetByRole(AriaRole.Cell, new() { Name = title });
        return await cell.CountAsync() > 0;
    }
}

public class TaskCreatePage
{
    private readonly IPage _page;

    public TaskCreatePage(IPage page) => _page = page;

    public async Task FillTitleAsync(string title) =>
        await _page.GetByLabel("Title").FillAsync(title);

    public async Task FillDescriptionAsync(string description) =>
        await _page.GetByLabel("Description").FillAsync(description);

    public async Task SetDueDateAsync(string date) =>
        await _page.GetByLabel("Due Date").FillAsync(date);

    public async Task SelectPriorityAsync(string priority) =>
        await _page.GetByLabel("Priority").SelectOptionAsync(priority);

    public async Task<TaskListPage> SubmitAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        return new TaskListPage(_page);
    }
}
```

#### Шаблон створення скріншота при невдачі

```csharp
public async Task DisposeAsync()
{
    // Захоплення скріншота, якщо тест провалився (перевірка через контекст тесту або try-catch)
    if (TestContext.Current.TestState == TestState.Failed)
    {
        var screenshotPath = Path.Combine("screenshots",
            $"{TestContext.Current.Test.DisplayName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
    }

    await _browser.DisposeAsync();
}
```

> **Підказка:** Створіть директорію `screenshots/` у вашому тестовому проекті та додайте її до `.gitignore`. Скріншоти призначені для локального налагодження та CI-артефактів, а не для комітів у репозиторій.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Веб-застосунок |
| Завдання 2 — Наскрізні тести сценаріїв |
| Завдання 3 — Page Object Model та шаблони |
| Стабільність тестів (без нестабільних тестів) |

## Здача роботи

- Рішення з проектами `Lab11.WebApp` та `Lab11.Tests`
- Класи Page Object у `Lab11.Tests/Pages/`
- Директорія для скріншотів при невдачах

## Посилання

- [Документація Playwright для .NET](https://playwright.dev/dotnet/docs/intro) -- офіційний посібник для початку роботи
- [Довідник API Playwright .NET](https://playwright.dev/dotnet/docs/api/class-playwright) -- повна документація API
- [Посібник з локаторів Playwright](https://playwright.dev/dotnet/docs/locators) -- найкращі практики пошуку елементів
- [Автоочікування Playwright](https://playwright.dev/dotnet/docs/actionability) -- розуміння роботи автоочікування
- [Переглядач трасувань Playwright](https://playwright.dev/dotnet/docs/trace-viewer-intro) -- інструмент посмертного налагодження для CI-помилок
- [Шаблон Page Object Model](https://playwright.dev/dotnet/docs/pom) -- посібник Playwright з POM
- [Підручник з ASP.NET Core Razor Pages](https://learn.microsoft.com/en-us/aspnet/core/tutorials/razor-pages/) -- для створення тестованого застосунку
- [Документація xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline) -- довідник тестового фреймворку
- [Бібліотека assertion-ів Shouldly](https://docs.shouldly.org/) -- бібліотека assertion-ів, що використовується у цій лабораторній
- [Мартін Фаулер: Шаблон Page Object](https://martinfowler.com/bliki/PageObject.html) -- походження шаблону проектування
