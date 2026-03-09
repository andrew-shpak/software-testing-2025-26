# Lab 11 — Services Testing: End-to-End

## Objective

Write end-to-end tests that verify complete user workflows through a web application, covering UI interactions, navigation, form submissions, and API calls.

## Prerequisites

Before starting this lab, make sure you have:

- .NET 10 SDK (or later) installed
- Familiarity with ASP.NET Core Razor Pages or MVC
- Completed previous labs on unit and integration testing
- Basic understanding of HTML forms, CSS selectors, and DOM structure
- PowerShell (for the Playwright browser installation script)

After adding the `Microsoft.Playwright` NuGet package, you must install the browser binaries:

```bash
# Build the test project first so the playwright.ps1 script is available
dotnet build Lab11.Tests

# Install browsers (Chromium, Firefox, WebKit)
pwsh Lab11.Tests/bin/Debug/net10.0/playwright.ps1 install
```

> **Note:** On CI environments you may also need to install system dependencies. Run `pwsh playwright.ps1 install-deps` to install required OS-level libraries.

## Key Concepts

### What Are End-to-End Tests?

End-to-end (E2E) tests validate the entire application stack by simulating real user behavior in a browser. They interact with the UI exactly as a user would: clicking buttons, filling forms, navigating pages, and verifying the results on screen.

### The Testing Pyramid and E2E Tests

```
        /  E2E  \        <-- Few, slow, high confidence
       /----------\
      / Integration \    <-- Some, moderate speed
     /----------------\
    /    Unit Tests     \ <-- Many, fast, isolated
   /--------------------\
```

E2E tests sit at the top of the testing pyramid. They are:
- **Slowest** to run (real browser, real rendering)
- **Most realistic** (closest to actual user experience)
- **Most brittle** (sensitive to UI changes, timing issues)
- **Most valuable** for catching integration issues across the full stack

Use them sparingly for critical user journeys.

### Why Playwright?

Playwright is a modern browser automation library that addresses many pain points of earlier tools (Selenium, Puppeteer):

| Feature | Playwright | Selenium |
|---------|-----------|----------|
| Auto-waiting | Built-in (waits for elements to be actionable) | Manual waits required |
| Browser support | Chromium, Firefox, WebKit | All major browsers via drivers |
| Speed | Fast (direct protocol communication) | Slower (WebDriver protocol) |
| .NET support | First-class (`Microsoft.Playwright`) | Via `Selenium.WebDriver` |
| Debugging | Trace viewer, codegen, inspector | Limited built-in tools |

### The Page Object Model (POM)

The Page Object Model is a design pattern that creates an abstraction layer over raw page interactions. Each page (or component) of your application gets its own class that encapsulates selectors and actions.

**Without POM** (fragile, duplicated selectors):
```csharp
await page.FillAsync("#Title", "My Task");
await page.FillAsync("#Description", "Details");
await page.ClickAsync("input[type='submit']");
```

**With POM** (maintainable, reusable):
```csharp
var createPage = new TaskCreatePage(page);
await createPage.FillTitle("My Task");
await createPage.FillDescription("Details");
await createPage.Submit();
```

If a selector changes, you fix it in one place rather than across dozens of tests.

## Tools

- Language: C#
- E2E Framework: [Playwright for .NET](https://playwright.dev/dotnet/)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- App Under Test: ASP.NET Core MVC or Razor Pages application

## Setup

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

# Install Playwright browsers
pwsh Lab11.Tests/bin/Debug/net10.0/playwright.ps1 install
```

## Tasks

### Task 1 — Build the Application Under Test

Create a simple task management web app with Razor Pages:

- `/` — Home page with list of tasks
- `/Tasks/Create` — Form to create a new task (Title, Description, DueDate, Priority)
- `/Tasks/Edit/{id}` — Form to edit a task
- `/Tasks/Details/{id}` — Task details page
- `/Tasks/Delete/{id}` — Delete confirmation page
- Include form validation (required fields, date format)

#### Example Task Model

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

> **Hint:** Use an in-memory list or EF Core InMemory provider for storage. The goal is the tests, not a production database. Keep the app simple.

### Task 2 — Playwright E2E Tests

Write E2E tests covering full user workflows:

1. **Create task workflow**:
   - Navigate to home page
   - Click "Create New Task"
   - Fill in the form fields
   - Submit the form
   - Verify redirect to task list
   - Verify new task appears in the list

2. **Edit task workflow**:
   - Navigate to task details
   - Click "Edit"
   - Modify fields
   - Save changes
   - Verify changes are persisted

3. **Delete task workflow**:
   - Navigate to task details
   - Click "Delete"
   - Confirm deletion
   - Verify task is removed from list

4. **Validation workflow**:
   - Try to submit empty form
   - Verify validation messages appear
   - Fill required fields one by one
   - Verify validation messages disappear

5. **Navigation workflow**:
   - Verify all navigation links work
   - Verify browser back/forward works correctly
   - Verify page titles are correct

#### Starting the App for Tests

You need to launch the web application before running Playwright against it. One approach is to start the app in the test fixture:

```csharp
public class WebAppFixture : IAsyncLifetime
{
    private WebApplication? _app;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Build and start the web application
        var builder = WebApplication.CreateBuilder(new[] { "--urls", "http://localhost:5180" });
        // ... configure services ...
        _app = builder.Build();
        // ... configure middleware ...
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

#### Example E2E Test

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
        // Navigate to home page
        await _page.GotoAsync(_fixture.BaseUrl);
        await _page.GetByRole(AriaRole.Link, new() { Name = "Create New Task" }).ClickAsync();

        // Fill the form
        await _page.GetByLabel("Title").FillAsync("Buy groceries");
        await _page.GetByLabel("Description").FillAsync("Milk, eggs, bread");
        await _page.GetByLabel("Due Date").FillAsync("2026-03-01");
        await _page.GetByLabel("Priority").SelectOptionAsync("High");

        // Submit
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Verify redirect and task in list
        await _page.WaitForURLAsync($"{_fixture.BaseUrl}/**");
        var taskInList = _page.GetByText("Buy groceries");
        await Expect(taskInList).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CreateTask_WithEmptyTitle_ShouldShowValidationAsync()
    {
        await _page.GotoAsync($"{_fixture.BaseUrl}/Tasks/Create");

        // Submit without filling required fields
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        // Validation message should appear
        var validationMessage = _page.GetByText("The Title field is required");
        await Expect(validationMessage).ToBeVisibleAsync();
    }
}
```

> **Hint:** Playwright has built-in auto-waiting. Methods like `ClickAsync`, `FillAsync`, and `GetByRole` automatically wait for elements to become actionable. You rarely need explicit `Task.Delay` or `WaitForTimeout` calls. If you find yourself adding manual delays, reconsider your approach -- use `WaitForURLAsync`, `WaitForSelectorAsync`, or Playwright's `Expect` assertions instead.

#### Expected Behavior Summary

| Workflow | Steps | Expected Outcome |
|----------|-------|-----------------|
| Create task | Fill form, submit | Task appears in list, redirect to index |
| Create empty task | Submit empty form | Validation messages shown, no redirect |
| Edit task | Change fields, save | Updated values visible on details page |
| Delete task | Confirm deletion | Task removed from list |
| Navigation | Click links | Correct pages load, titles match |

### Task 3 — Advanced E2E Patterns

Implement:

1. **Page Object Model**: Create page classes for each page:
   ```csharp
   public class TaskListPage
   {
       private readonly IPage _page;
       public async Task<TaskCreatePage> ClickCreateNew() { ... }
       public async Task<int> GetTaskCount() { ... }
       public async Task<bool> HasTask(string title) { ... }
   }
   ```

2. **Screenshot on failure**: Capture screenshots when tests fail
3. **Test data cleanup**: Ensure each test starts with clean state

#### Page Object Model Example

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

#### Screenshot on Failure Pattern

```csharp
public async Task DisposeAsync()
{
    // Capture screenshot if test failed (check via test context or try-catch)
    if (TestContext.Current.TestState == TestState.Failed)
    {
        var screenshotPath = Path.Combine("screenshots",
            $"{TestContext.Current.Test.DisplayName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
    }

    await _browser.DisposeAsync();
}
```

> **Hint:** Create a `screenshots/` directory in your test project and add it to `.gitignore`. Screenshots are for local debugging and CI artifacts, not for committing to the repository.

### Task 4 — Cross-Browser Testing

Run the same test suite on:

1. Chromium
2. Firefox
3. WebKit (Safari)

Use `[Theory]` with browser types:

```csharp
[Theory]
[InlineData("chromium")]
[InlineData("firefox")]
[InlineData("webkit")]
public async Task CreateTask_ShouldWork_OnAllBrowsers(string browserType) { ... }
```

Document any cross-browser differences found.

#### Cross-Browser Launch Example

```csharp
[Theory]
[InlineData("chromium")]
[InlineData("firefox")]
[InlineData("webkit")]
public async Task CreateTask_ShouldWork_OnAllBrowsers(string browserType)
{
    using var playwright = await Playwright.CreateAsync();

    var browser = browserType switch
    {
        "chromium" => await playwright.Chromium.LaunchAsync(new() { Headless = true }),
        "firefox" => await playwright.Firefox.LaunchAsync(new() { Headless = true }),
        "webkit" => await playwright.Webkit.LaunchAsync(new() { Headless = true }),
        _ => throw new ArgumentException($"Unknown browser: {browserType}")
    };

    var page = await browser.NewPageAsync();

    try
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Tasks/Create");
        await page.GetByLabel("Title").FillAsync("Cross-browser test");
        await page.GetByLabel("Priority").SelectOptionAsync("High");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();

        var taskInList = page.GetByText("Cross-browser test");
        await Expect(taskInList).ToBeVisibleAsync();
    }
    finally
    {
        await browser.DisposeAsync();
    }
}
```

#### Common Cross-Browser Differences to Watch For

| Area | Potential Differences |
|------|----------------------|
| Date input | Chromium uses date picker; Firefox/WebKit may need different input format |
| Form validation | Validation popups look different; messages may vary |
| CSS rendering | Minor visual differences (fonts, spacing) |
| JavaScript timing | Event handling order can vary between engines |
| File upload | Dialog behavior differs between browsers |

> **Hint:** If a test fails on only one browser, use `Headless = false` to launch that browser visually and debug the issue interactively. You can also use `await page.PauseAsync()` to pause execution and inspect the page with Playwright Inspector.

## Grading

| Criteria |
|----------|
| Task 1 — Web application |
| Task 2 — E2E workflow tests |
| Task 3 — Page Object Model and patterns |
| Task 4 — Cross-browser tests |
| Test stability (no flaky tests) |

## Submission

- Solution with `Lab11.WebApp` and `Lab11.Tests` projects
- Page Object classes in `Lab11.Tests/Pages/`
- Screenshots directory for failure captures

## References

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/docs/intro) -- official getting started guide
- [Playwright .NET API Reference](https://playwright.dev/dotnet/docs/api/class-playwright) -- full API documentation
- [Playwright Locators Guide](https://playwright.dev/dotnet/docs/locators) -- best practices for finding elements
- [Playwright Auto-Waiting](https://playwright.dev/dotnet/docs/actionability) -- understanding how auto-wait works
- [Playwright Trace Viewer](https://playwright.dev/dotnet/docs/trace-viewer-intro) -- post-mortem debugging tool for CI failures
- [Page Object Model Pattern](https://playwright.dev/dotnet/docs/pom) -- Playwright's guide to POM
- [ASP.NET Core Razor Pages Tutorial](https://learn.microsoft.com/en-us/aspnet/core/tutorials/razor-pages/) -- for building the app under test
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) -- test framework reference
- [Shouldly Assertion Library](https://docs.shouldly.org/) -- assertion library used in this lab
- [Martin Fowler: Page Object Pattern](https://martinfowler.com/bliki/PageObject.html) -- design pattern origin
