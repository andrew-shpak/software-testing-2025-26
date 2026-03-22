# Лекція 7: Наскрізне тестування (E2E)

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Explain what end-to-end (E2E) testing is and how it differs from unit and integration testing
- Describe the testing pyramid and where E2E tests fit within it
- Set up Playwright for .NET projects
- Write E2E tests that interact with web applications through a real browser
- Apply the Page Object Model pattern to organize E2E tests
- Handle common E2E challenges: waits, flakiness, authentication, and test data
- Run E2E tests in CI/CD pipelines with GitHub Actions
- Decide when E2E tests add value vs. when lower-level tests are more appropriate

---

## 1. Що таке наскрізне тестування

### 1.1 Визначення

End-to-end (E2E) testing verifies that a complete application flow works from the user's perspective. Unlike unit tests (which test isolated functions) or integration tests (which test component interactions), E2E tests exercise the **entire system** — frontend, backend, database, and external services — as a real user would.

```
Testing Levels Comparison:

Unit Test:
  [Function] → assert result ✓

Integration Test:
  [Controller] → [Service] → [Database] → assert response ✓

E2E Test:
  [Browser] → [UI] → [API] → [Service] → [Database]
      ↑                                        │
      └────────── assert what user sees ────────┘
```

### 1.2 Піраміда тестування

The **testing pyramid** (Mike Cohn, 2009) suggests a distribution of tests:

```
            ╱╲
           ╱  ╲          E2E Tests
          ╱ E2E╲         - Few, slow, expensive
         ╱──────╲        - Verify critical user journeys
        ╱        ╲
       ╱Integration╲     Integration Tests
      ╱──────────────╲   - Moderate count, moderate speed
     ╱                ╲  - Verify component interactions
    ╱   Unit Tests     ╲
   ╱────────────────────╲ Unit Tests
  ╱                      ╲ - Many, fast, cheap
 ╱________________________╲ - Verify individual logic
```

| Level | Count | Speed | Cost per test | Confidence |
|---|---|---|---|---|
| Unit | Thousands | < 1ms | Low | Low (isolated) |
| Integration | Hundreds | 10-500ms | Medium | Medium |
| E2E | Tens | 1-30s | High | High (realistic) |

> **Key insight:** E2E tests give the highest confidence that the system works as a user expects, but they are slow and expensive. The pyramid says: **write fewer E2E tests, but make each one count.**

### 1.3 Альтернативні моделі

The classic pyramid has evolved. Modern teams often use:

**Testing Trophy (Kent C. Dodds):**
```
        ╱╲         E2E
       ╱──╲
      ╱    ╲       Integration (largest)
     ╱      ╲
    ╱────────╲
   ╱          ╲    Unit
  ╱────────────╲
 ╱  Static      ╲  Static analysis
╱────────────────╲
```

**Testing Honeycomb (Spotify):**
```
   ┌─────────────────┐
   │    E2E (few)     │
   ├─────────────────┤
   │  Integration     │  ← largest layer
   │  (many)          │
   ├─────────────────┤
   │  Unit (some)     │
   └─────────────────┘
```

The common theme: **integration tests** are the sweet spot, and E2E tests should be used **strategically** for critical paths.

### 1.4 Коли E2E тести додають найбільше цінності

E2E tests are most valuable when they verify:

| Scenario | Example |
|---|---|
| **Critical business flows** | User registration → login → purchase → confirmation |
| **Cross-system integration** | Frontend form → API → database → email service |
| **Workflows spanning multiple pages** | Multi-step checkout, onboarding wizards |
| **Authentication & authorization** | Login, role-based access, session management |
| **Real browser behavior** | JavaScript rendering, CSS layout, responsive design |

E2E tests are **less** valuable for:
- Testing individual form validations (unit test the validator)
- API response formats (integration test the endpoint)
- Business logic rules (unit test the service)
- Database queries (integration test the repository)

> **Discussion (5 min):** Think of an application you use daily. What are the 3-5 most critical user journeys you would E2E test?

---

## 2. Інструменти для E2E тестування

### 2.1 Огляд фреймворків

| Tool | Language | Browser Engine | Key Feature |
|---|---|---|---|
| **Playwright** | C#, JS, Python, Java | Chromium, Firefox, WebKit | Auto-wait, codegen, tracing |
| Selenium | Many | WebDriver protocol | Oldest, largest ecosystem |
| Cypress | JavaScript | Chromium only | In-browser execution, time travel |
| Puppeteer | JavaScript | Chromium only | Chrome DevTools Protocol |

### 2.2 Чому Playwright

For this course, we use **Playwright for .NET** because:

1. **Native .NET support** — integrates with xUnit/NUnit, same language as our API
2. **Auto-wait** — automatically waits for elements to be actionable (no manual `Thread.Sleep`)
3. **Multi-browser** — tests run on Chromium, Firefox, and WebKit from the same code
4. **Codegen** — records user actions and generates test code
5. **Tracing** — captures screenshots, network logs, and DOM snapshots for debugging
6. **Resilient selectors** — prefers text, role, and test-id selectors over fragile CSS/XPath

```
Selenium vs. Playwright Architecture:

Selenium:
  Test → WebDriver Protocol → Browser Driver → Browser
         (HTTP commands)      (chromedriver)

Playwright:
  Test → CDP / DevTools Protocol → Browser
         (WebSocket, direct)

Playwright's direct connection means:
  - Faster command execution
  - Better auto-waiting
  - Access to network interception
  - Native mobile emulation
```

### 2.3 Встановлення Playwright для .NET

```bash
# Create a new test project
dotnet new nunit -n MyApp.E2E.Tests
cd MyApp.E2E.Tests

# Add Playwright
dotnet add package Microsoft.Playwright.NUnit

# Build and install browsers
dotnet build
pwsh bin/Debug/net8.0/playwright.ps1 install
```

> **Note:** Playwright requires PowerShell (`pwsh`) for browser installation. On macOS/Linux, install it via `dotnet tool install --global PowerShell` if not available.

Project structure:
```
MyApp.E2E.Tests/
├── MyApp.E2E.Tests.csproj
├── Pages/                    # Page Object Models
│   ├── LoginPage.cs
│   ├── DashboardPage.cs
│   └── OrderPage.cs
├── Tests/
│   ├── AuthenticationTests.cs
│   ├── OrderFlowTests.cs
│   └── NavigationTests.cs
├── Fixtures/                 # Test setup and shared state
│   └── TestFixture.cs
└── playwright.config.cs      # (optional) configuration
```

---

## 3. Перший E2E тест з Playwright

### 3.1 Базова структура тесту

```csharp
using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;

namespace MyApp.E2E.Tests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class HomePageTests : PageTest
{
    [Test]
    public async Task HomePage_ShouldDisplayWelcomeMessage()
    {
        // Arrange — navigate to the application
        await Page.GotoAsync("https://localhost:5001");

        // Act — find the heading
        var heading = Page.GetByRole(AriaRole.Heading, new() { Name = "Welcome" });

        // Assert — verify it's visible
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task HomePage_ShouldHaveCorrectTitle()
    {
        await Page.GotoAsync("https://localhost:5001");

        await Expect(Page).ToHaveTitleAsync("My Application");
    }
}
```

**Key concepts:**
- `PageTest` base class provides a fresh `Page` (browser tab) for each test
- `Page.GotoAsync()` navigates to a URL
- `Page.GetByRole()` finds elements by their accessibility role
- `Expect()` provides Playwright assertions with auto-retry

### 3.2 Локатори — як знаходити елементи

Playwright recommends **user-facing locators** that mirror how users find elements:

```csharp
// ✅ RECOMMENDED — resilient, user-facing locators

// By role (button, link, heading, textbox, etc.)
Page.GetByRole(AriaRole.Button, new() { Name = "Submit" });
Page.GetByRole(AriaRole.Link, new() { Name = "Sign up" });
Page.GetByRole(AriaRole.Heading, new() { Level = 1 });

// By label (form fields)
Page.GetByLabel("Email address");
Page.GetByLabel("Password");

// By placeholder
Page.GetByPlaceholder("Enter your email");

// By text content
Page.GetByText("Welcome back");
Page.GetByText("Order #12345");

// By test ID (when no semantic locator fits)
Page.GetByTestId("checkout-button");
// Requires: <button data-testid="checkout-button">Pay</button>

// By alt text (images)
Page.GetByAltText("Company logo");
```

```csharp
// ❌ AVOID — fragile locators that break on refactoring

// CSS selectors tied to structure
Page.Locator("div.container > div:nth-child(3) > button.btn-primary");

// IDs that may change
Page.Locator("#btn-submit-form-v2");

// XPath
Page.Locator("//div[@class='header']/nav/ul/li[2]/a");

// Classes used for styling
Page.Locator(".text-blue-500.font-bold.mt-4");
```

**Locator priority** (most to least resilient):

```
1. GetByRole()       — semantic, accessible, rarely changes
2. GetByLabel()      — tied to form behavior
3. GetByPlaceholder()— visible to user
4. GetByText()       — content-based
5. GetByTestId()     — explicit contract between dev and test
6. Locator("css")    — last resort, fragile
```

### 3.3 Дії — взаємодія з елементами

```csharp
// Clicking
await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
await Page.GetByRole(AriaRole.Link, new() { Name = "Dashboard" }).ClickAsync();

// Typing
await Page.GetByLabel("Username").FillAsync("john@example.com");
await Page.GetByLabel("Password").FillAsync("secret123");

// Clearing and typing
await Page.GetByLabel("Search").ClearAsync();
await Page.GetByLabel("Search").FillAsync("new search term");

// Selecting from dropdown
await Page.GetByLabel("Country").SelectOptionAsync("UA");

// Checking / unchecking
await Page.GetByLabel("I agree to terms").CheckAsync();
await Page.GetByLabel("Subscribe to newsletter").UncheckAsync();

// Pressing keyboard keys
await Page.GetByLabel("Search").PressAsync("Enter");

// Hovering
await Page.GetByText("Menu").HoverAsync();

// Uploading files
await Page.GetByLabel("Upload").SetInputFilesAsync("invoice.pdf");
```

### 3.4 Assertions — перевірка результатів

Playwright assertions **auto-retry** until the condition is met or timeout expires (default: 5 seconds):

```csharp
// Page assertions
await Expect(Page).ToHaveTitleAsync("Dashboard");
await Expect(Page).ToHaveURLAsync("https://localhost:5001/dashboard");
await Expect(Page).ToHaveURLAsync(new Regex("/dashboard$"));

// Element assertions
var alert = Page.GetByRole(AriaRole.Alert);
await Expect(alert).ToBeVisibleAsync();
await Expect(alert).ToHaveTextAsync("Order placed successfully");
await Expect(alert).ToContainTextAsync("successfully");
await Expect(alert).ToHaveAttributeAsync("class", "alert-success");

// Visibility
await Expect(Page.GetByTestId("loading-spinner")).ToBeHiddenAsync();
await Expect(Page.GetByText("Welcome")).ToBeVisibleAsync();

// Form state
await Expect(Page.GetByLabel("Email")).ToHaveValueAsync("john@example.com");
await Expect(Page.GetByLabel("Submit")).ToBeEnabledAsync();
await Expect(Page.GetByLabel("Submit")).ToBeDisabledAsync();

// Negation
await Expect(Page.GetByText("Error")).Not.ToBeVisibleAsync();

// Count
await Expect(Page.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(5);
```

> **Important:** Playwright assertions auto-retry. Do NOT use NUnit `Assert.That()` for checking page state — it does not retry and will fail on dynamic content.

---

## 4. Реальний приклад: тестування інтернет-магазину

### 4.1 Сценарій

Consider an Order Management API with a web frontend. The critical user journey:

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Login   │───►│  Browse  │───►│  Create  │───►│  Verify  │
│  Page    │    │  Orders  │    │  Order   │    │  Order   │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
                                      │
                                      ▼
                                ┌──────────┐
                                │  Order   │
                                │ Appears  │
                                │ in List  │
                                └──────────┘
```

### 4.2 Тест для створення замовлення

```csharp
[Test]
public async Task User_CanCreateOrder_AndSeeItInList()
{
    // Arrange — login
    await Page.GotoAsync("https://localhost:5001/login");
    await Page.GetByLabel("Email").FillAsync("user@example.com");
    await Page.GetByLabel("Password").FillAsync("Password123!");
    await Page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();

    // Verify login succeeded
    await Expect(Page).ToHaveURLAsync(new Regex("/dashboard"));

    // Act — navigate to orders and create a new one
    await Page.GetByRole(AriaRole.Link, new() { Name = "Orders" }).ClickAsync();
    await Page.GetByRole(AriaRole.Button, new() { Name = "New Order" }).ClickAsync();

    // Fill order form
    await Page.GetByLabel("Customer Name").FillAsync("John Doe");
    await Page.GetByLabel("Product").SelectOptionAsync("Widget A");
    await Page.GetByLabel("Quantity").FillAsync("5");
    await Page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

    // Assert — success message appears
    await Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync("Order created successfully");

    // Assert — order appears in the list
    await Page.GetByRole(AriaRole.Link, new() { Name = "Orders" }).ClickAsync();
    await Expect(Page.GetByText("John Doe")).ToBeVisibleAsync();
    await Expect(Page.GetByText("Widget A")).ToBeVisibleAsync();
}
```

### 4.3 Проблеми цього підходу

This test works but has issues:

```
Problems with the flat test above:

1. Login logic duplicated across every test
2. Locator strings scattered — if "Email" label changes, fix everywhere
3. Long test — hard to read at a glance
4. No reusable abstractions — each test rebuilds the same steps
5. Fragile to UI changes — a redesigned form breaks many tests
```

The solution: **Page Object Model**.

---

## 5. Page Object Model (POM)

### 5.1 Концепція

The Page Object Model encapsulates page-specific locators and actions in dedicated classes. Tests interact with **page objects** instead of raw locators:

```
Without POM:                          With POM:

Test: "click #login-btn"              Test: loginPage.Login(user, pass)
Test: "fill #email-input"
Test: "click .nav > .orders"          Test: nav.GoToOrders()
Test: "assert .order-row count=5"     Test: ordersPage.AssertOrderCount(5)

If UI changes:                        If UI changes:
  → Fix every test                      → Fix only the Page Object
```

### 5.2 Реалізація Page Objects

**LoginPage.cs:**
```csharp
using Microsoft.Playwright;

namespace MyApp.E2E.Tests.Pages;

public class LoginPage
{
    private readonly IPage _page;

    // Locators — defined once, used everywhere
    private ILocator EmailInput => _page.GetByLabel("Email");
    private ILocator PasswordInput => _page.GetByLabel("Password");
    private ILocator LoginButton => _page.GetByRole(AriaRole.Button, new() { Name = "Login" });
    private ILocator ErrorMessage => _page.GetByRole(AriaRole.Alert);

    public LoginPage(IPage page)
    {
        _page = page;
    }

    public async Task GotoAsync()
    {
        await _page.GotoAsync("/login");
    }

    public async Task LoginAsync(string email, string password)
    {
        await EmailInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
        await LoginButton.ClickAsync();
    }

    public async Task AssertErrorMessageAsync(string expectedMessage)
    {
        await Assertions.Expect(ErrorMessage).ToHaveTextAsync(expectedMessage);
    }
}
```

**OrdersPage.cs:**
```csharp
using Microsoft.Playwright;

namespace MyApp.E2E.Tests.Pages;

public class OrdersPage
{
    private readonly IPage _page;

    private ILocator NewOrderButton => _page.GetByRole(AriaRole.Button, new() { Name = "New Order" });
    private ILocator CustomerNameInput => _page.GetByLabel("Customer Name");
    private ILocator ProductSelect => _page.GetByLabel("Product");
    private ILocator QuantityInput => _page.GetByLabel("Quantity");
    private ILocator SubmitButton => _page.GetByRole(AriaRole.Button, new() { Name = "Submit" });
    private ILocator SuccessAlert => _page.GetByRole(AriaRole.Alert);
    private ILocator OrderRows => _page.GetByRole(AriaRole.Row);

    public OrdersPage(IPage page)
    {
        _page = page;
    }

    public async Task GotoAsync()
    {
        await _page.GotoAsync("/orders");
    }

    public async Task CreateOrderAsync(string customerName, string product, int quantity)
    {
        await NewOrderButton.ClickAsync();
        await CustomerNameInput.FillAsync(customerName);
        await ProductSelect.SelectOptionAsync(product);
        await QuantityInput.FillAsync(quantity.ToString());
        await SubmitButton.ClickAsync();
    }

    public async Task AssertOrderCreatedAsync()
    {
        await Assertions.Expect(SuccessAlert).ToHaveTextAsync("Order created successfully");
    }

    public async Task AssertOrderVisibleAsync(string customerName)
    {
        await Assertions.Expect(_page.GetByText(customerName)).ToBeVisibleAsync();
    }
}
```

### 5.3 Тест з Page Objects

```csharp
[Test]
public async Task User_CanCreateOrder_AndSeeItInList()
{
    var loginPage = new LoginPage(Page);
    var ordersPage = new OrdersPage(Page);

    // Login
    await loginPage.GotoAsync();
    await loginPage.LoginAsync("user@example.com", "Password123!");

    // Create order
    await ordersPage.GotoAsync();
    await ordersPage.CreateOrderAsync("John Doe", "Widget A", 5);
    await ordersPage.AssertOrderCreatedAsync();

    // Verify in list
    await ordersPage.GotoAsync();
    await ordersPage.AssertOrderVisibleAsync("John Doe");
}
```

Compare this with the flat test in section 4.2 — the POM version is:
- **Shorter** — focuses on intent, not implementation
- **Readable** — reads like a user story
- **Maintainable** — UI changes require updating only the page object

### 5.4 Правила Page Object Model

| Rule | Reason |
|---|---|
| Page objects expose **actions**, not locators | Tests shouldn't know about CSS/HTML |
| Page objects **never** contain assertions* | Keep assertions in tests for clarity |
| Methods return the **next page object** if navigation occurs | Enables fluent chaining |
| One page object per page or significant component | Keeps classes focused |
| Use **composition** for shared components (nav, footer) | Avoids inheritance chains |

*Some teams put assertions in page objects (as shown above). Both approaches have trade-offs — the key is **consistency**.

---

## 6. Обробка типових проблем

### 6.1 Очікування та автоматичне повторення

Playwright auto-waits for elements before performing actions:

```csharp
// Playwright automatically waits for the button to be:
// 1. Attached to the DOM
// 2. Visible
// 3. Stable (not animating)
// 4. Enabled
// 5. Not obscured by other elements
await Page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

// ❌ NEVER do this — Playwright handles waiting
Thread.Sleep(3000); // anti-pattern!
await Task.Delay(2000); // anti-pattern!
```

For cases where you need to wait for a specific condition:

```csharp
// Wait for a network request to complete
await Page.WaitForResponseAsync(
    response => response.Url.Contains("/api/orders") && response.Status == 200
);

// Wait for navigation
await Page.WaitForURLAsync("/dashboard");

// Wait for an element to appear
await Page.GetByText("Loading complete").WaitForAsync();

// Wait for an element to disappear
await Page.GetByTestId("spinner").WaitForAsync(new() { State = WaitForSelectorState.Hidden });
```

### 6.2 Нестабільні тести (Flaky Tests)

Flaky tests pass sometimes and fail sometimes with the same code. They are the #1 problem in E2E testing:

```
Common causes of flakiness:

┌─────────────────────────────────────────────────────┐
│  Cause              │  Solution                      │
├─────────────────────┼────────────────────────────────┤
│  Timing issues      │  Use auto-wait, avoid Sleep    │
│  Test data coupling │  Isolate test data per test    │
│  Shared state       │  Reset state before each test  │
│  Animation/CSS      │  Wait for stability            │
│  Network latency    │  Mock external services        │
│  Non-deterministic  │  Use fixed seeds, freeze time  │
│  order              │                                │
│  Parallel execution │  Ensure test independence      │
└─────────────────────┴────────────────────────────────┘
```

**Strategies to reduce flakiness:**

```csharp
// 1. Use web-first assertions (they auto-retry)
await Expect(Page.GetByText("Success")).ToBeVisibleAsync();

// 2. Wait for network idle after actions that trigger API calls
await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

// 3. Use test-specific data to avoid conflicts
var uniqueName = $"Order-{Guid.NewGuid():N[..8]}";
await Page.GetByLabel("Name").FillAsync(uniqueName);

// 4. Retry flaky tests (use sparingly — fix the root cause first!)
[Test]
[Retry(3)]  // NUnit retry attribute
public async Task FlakyTest_WithRetry()
{
    // ...
}
```

### 6.3 Автентифікація

Logging in through the UI for every test is slow. Playwright offers **storage state** to reuse authentication:

```csharp
// GlobalSetup.cs — run once before all tests
public class GlobalSetup
{
    public static async Task AuthenticateAsync()
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Login through UI once
        await page.GotoAsync("https://localhost:5001/login");
        await page.GetByLabel("Email").FillAsync("user@example.com");
        await page.GetByLabel("Password").FillAsync("Password123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
        await page.WaitForURLAsync("/dashboard");

        // Save authentication state (cookies, localStorage)
        await context.StorageStateAsync(new()
        {
            Path = "auth-state.json"
        });

        await browser.CloseAsync();
    }
}

// In tests — reuse the stored state
[TestFixture]
public class AuthenticatedTests : PageTest
{
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            StorageStatePath = "auth-state.json"
        };
    }

    [Test]
    public async Task Dashboard_ShowsUserName()
    {
        // Already logged in!
        await Page.GotoAsync("/dashboard");
        await Expect(Page.GetByText("Welcome, user@example.com")).ToBeVisibleAsync();
    }
}
```

```
Authentication strategies:

Strategy          Speed     Realism     Use When
─────────────────────────────────────────────────────
UI login/test     Slow      High        Testing the login flow itself
Storage state     Fast      High        Most authenticated tests
API login         Fast      Medium      When UI login is complex
Mock auth         Fastest   Low         Unit/integration tests only
```

### 6.4 Тестові дані

E2E tests need data. Strategies:

```
┌──────────────────────────────────────────────────────────────┐
│  Strategy           │ Pros              │ Cons               │
├──────────────────────┼───────────────────┼────────────────────┤
│  Seed database       │ Fast, consistent  │ Needs cleanup      │
│  before tests        │                   │ logic              │
├──────────────────────┼───────────────────┼────────────────────┤
│  Create via API      │ Tests real flow   │ Slower, dependent  │
│  in test setup       │                   │ on API stability   │
├──────────────────────┼───────────────────┼────────────────────┤
│  Create via UI       │ Most realistic    │ Slowest, fragile   │
│  in test setup       │                   │                    │
├──────────────────────┼───────────────────┼────────────────────┤
│  Shared test         │ Simple setup      │ Tests coupled,     │
│  database snapshot   │                   │ order-dependent    │
└──────────────────────┴───────────────────┴────────────────────┘
```

```csharp
// Recommended: Create test data via API, test via UI
[SetUp]
public async Task SetUp()
{
    // Create test data through the API (fast, reliable)
    var client = new HttpClient();
    await client.PostAsJsonAsync("https://localhost:5001/api/orders", new
    {
        CustomerName = "Test User",
        Product = "Widget A",
        Quantity = 3
    });
}

[Test]
public async Task OrdersList_ShowsExistingOrders()
{
    // Test through the UI (verifies the full stack)
    await Page.GotoAsync("/orders");
    await Expect(Page.GetByText("Test User")).ToBeVisibleAsync();
}
```

---

## 7. Розширені можливості Playwright

### 7.1 Перехоплення мережевих запитів

```csharp
// Mock an API response
await Page.RouteAsync("**/api/orders", async route =>
{
    await route.FulfillAsync(new()
    {
        Status = 200,
        ContentType = "application/json",
        Body = """
        [
            { "id": 1, "customerName": "Mock User", "product": "Widget A", "quantity": 10 }
        ]
        """
    });
});

await Page.GotoAsync("/orders");
await Expect(Page.GetByText("Mock User")).ToBeVisibleAsync();
```

Use cases for network mocking:
- **Slow external services** — avoid waiting for third-party APIs
- **Error scenarios** — simulate 500 errors, timeouts, malformed responses
- **Edge cases** — return specific data that's hard to create naturally
- **Offline behavior** — test what happens when the API is unreachable

### 7.2 Скріншоти та відео

```csharp
// Screenshot on specific step
await Page.ScreenshotAsync(new() { Path = "screenshots/order-created.png" });

// Screenshot of a specific element
await Page.GetByTestId("order-summary").ScreenshotAsync(new()
{
    Path = "screenshots/order-summary.png"
});

// Full-page screenshot
await Page.ScreenshotAsync(new()
{
    Path = "screenshots/full-page.png",
    FullPage = true
});

// Enable video recording for all tests
public override BrowserNewContextOptions ContextOptions()
{
    return new BrowserNewContextOptions
    {
        RecordVideoDir = "videos/",
        RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
    };
}
```

### 7.3 Трейсинг

Traces capture a complete record of a test execution — screenshots, DOM snapshots, network requests, and console logs:

```csharp
[Test]
public async Task OrderFlow_WithTracing()
{
    // Start tracing
    await Context.Tracing.StartAsync(new()
    {
        Screenshots = true,
        Snapshots = true,
        Sources = true
    });

    try
    {
        await Page.GotoAsync("/orders");
        await Page.GetByRole(AriaRole.Button, new() { Name = "New Order" }).ClickAsync();
        // ... test steps
    }
    finally
    {
        // Save trace (even on failure)
        await Context.Tracing.StopAsync(new()
        {
            Path = "traces/order-flow.zip"
        });
    }
}
```

View traces with:
```bash
pwsh bin/Debug/net8.0/playwright.ps1 show-trace traces/order-flow.zip
```

```
Trace Viewer shows:
┌─────────────────────────────────────────────────────────┐
│  Timeline: ──●──────●──────●──────●──────●──            │
│             nav    click   fill   click  assert          │
│                                                         │
│  ┌──────────────────┐  ┌──────────────────────────────┐ │
│  │                  │  │ Action Log:                   │ │
│  │  DOM Snapshot    │  │ 1. goto /orders               │ │
│  │  (interactive)   │  │ 2. click "New Order"          │ │
│  │                  │  │ 3. fill "Customer Name"       │ │
│  │                  │  │ 4. click "Submit"             │ │
│  │                  │  │ 5. ✓ expect visible           │ │
│  └──────────────────┘  └──────────────────────────────┘ │
│                                                         │
│  Network: GET /api/orders 200 (45ms)                    │
│           POST /api/orders 201 (120ms)                  │
│  Console: [info] Order created: #1234                   │
└─────────────────────────────────────────────────────────┘
```

### 7.4 Codegen — генерація тестів

Playwright can record your interactions and generate test code:

```bash
pwsh bin/Debug/net8.0/playwright.ps1 codegen https://localhost:5001
```

This opens a browser and a code generator window. Every action you take is recorded as C# code:

```
┌──────────────────────────────┐  ┌──────────────────────────────┐
│  Browser Window              │  │  Code Generator              │
│                              │  │                              │
│  ┌────────────────────────┐  │  │  await page.GotoAsync(       │
│  │  Your App              │  │  │    "https://localhost:5001");│
│  │                        │  │  │                              │
│  │  [Email: ________]     │  │  │  await page.GetByLabel(      │
│  │  [Password: ________]  │  │  │    "Email")                  │
│  │  [Login]               │  │  │    .FillAsync("user@...");   │
│  └────────────────────────┘  │  │                              │
│                              │  │  await page.GetByLabel(      │
│  Click, type, navigate...    │  │    "Password")               │
│  Code appears in real-time →│  │    .FillAsync("pass");       │
│                              │  │                              │
└──────────────────────────────┘  └──────────────────────────────┘
```

> **Tip:** Codegen-generated code is a starting point, not a finished test. Always refactor it into Page Objects and add proper assertions.

### 7.5 Емуляція пристроїв

```csharp
// Test on mobile viewport
public override BrowserNewContextOptions ContextOptions()
{
    return new BrowserNewContextOptions
    {
        ViewportSize = new ViewportSize { Width = 375, Height = 667 },
        IsMobile = true,
        HasTouch = true,
        // Emulate specific device
        UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X)..."
    };
}

[Test]
public async Task MobileMenu_ShouldToggleOnClick()
{
    await Page.GotoAsync("/");

    // Mobile hamburger menu
    await Page.GetByRole(AriaRole.Button, new() { Name = "Menu" }).ClickAsync();
    await Expect(Page.GetByRole(AriaRole.Navigation)).ToBeVisibleAsync();
}
```

---

## 8. E2E тести в CI/CD

### 8.1 GitHub Actions Workflow

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  e2e:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_DB: testdb
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build application
        run: dotnet build src/MyApp.Api/MyApp.Api.csproj

      - name: Start application
        run: |
          dotnet run --project src/MyApp.Api/MyApp.Api.csproj &
          # Wait for the app to be ready
          for i in $(seq 1 30); do
            curl -s http://localhost:5001/health && break
            sleep 1
          done

      - name: Install Playwright browsers
        run: pwsh tests/MyApp.E2E.Tests/bin/Debug/net8.0/playwright.ps1 install --with-deps

      - name: Run E2E tests
        run: dotnet test tests/MyApp.E2E.Tests/ --logger "trx;LogFileName=e2e-results.trx"
        env:
          BASE_URL: http://localhost:5001

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-results
          path: |
            tests/MyApp.E2E.Tests/TestResults/
            tests/MyApp.E2E.Tests/traces/
            tests/MyApp.E2E.Tests/screenshots/
```

### 8.2 Конфігурація для CI

```csharp
// In tests, use environment variable for the base URL
[TestFixture]
public class BaseE2ETest : PageTest
{
    protected string BaseUrl =>
        Environment.GetEnvironmentVariable("BASE_URL") ?? "https://localhost:5001";

    [SetUp]
    public async Task NavigateToBase()
    {
        await Page.GotoAsync(BaseUrl);
    }
}
```

### 8.3 Headless vs. Headed

```
CI Environment (headless — default):
  ┌─────────────────────────────┐
  │  No visible browser window  │
  │  Faster execution           │
  │  Lower resource usage       │
  │  Used in CI/CD pipelines    │
  └─────────────────────────────┘

Local Development (headed):
  ┌─────────────────────────────┐
  │  Visible browser window     │
  │  See test execution live    │
  │  Useful for debugging       │
  │  Set via environment var    │
  └─────────────────────────────┘
```

Run tests headed locally:
```bash
HEADED=1 dotnet test
```

```csharp
public override BrowserNewContextOptions ContextOptions()
{
    return new BrowserNewContextOptions
    {
        // Slow down actions for debugging
        // (only locally — never in CI)
    };
}
```

---

## 9. Найкращі практики

### 9.1 Що тестувати E2E

```
✅ DO test with E2E:                    ❌ DON'T test with E2E:

• Critical happy paths                  • Every form validation rule
• Authentication flows                  • API response formats
• Multi-page workflows                  • Business logic calculations
• Payment / checkout                    • Individual component rendering
• User onboarding                       • Error message wording
• Search and filtering                  • Sorting algorithms
  (with results displayed)              • Database query correctness
```

### 9.2 Правило "3-5 E2E тестів"

For most applications, 3-5 well-chosen E2E tests provide 80% of the confidence:

```
Example: E-commerce Application

1. Registration → Login → Browse → Add to Cart → Checkout → Confirmation
2. Search → Filter → Product Detail → Reviews
3. Login → Order History → Reorder → Checkout
4. Login → Account Settings → Change Password → Login with new password
5. Guest → Add to Cart → Login prompt → Login → Cart preserved → Checkout
```

### 9.3 Чек-лист якості E2E тестів

```
□ Each test is independent (can run alone, in any order)
□ No hardcoded waits (Thread.Sleep, Task.Delay)
□ Uses resilient locators (role, label, text, test-id)
□ Page Object Model for shared pages
□ Test data created per test or in setup (not shared across tests)
□ Assertions use Playwright's auto-retry (Expect)
□ Authentication reused via storage state
□ Tests run in CI without modification
□ Traces/screenshots captured on failure
□ Cleanup after tests (if creating data)
```

### 9.4 Антипатерни

| Anti-pattern | Problem | Solution |
|---|---|---|
| Testing everything E2E | Slow suite, high flakiness | Push tests down the pyramid |
| `Thread.Sleep(5000)` | Slow, still flaky | Use Playwright auto-wait |
| Shared test data | Tests depend on execution order | Isolate data per test |
| Testing implementation | Breaks on refactoring | Test user-visible behavior |
| Ignoring flaky tests | Erodes trust in test suite | Fix root cause immediately |
| No Page Objects | Duplicated locators everywhere | Extract page objects early |
| Screenshot-only debugging | Hard to reproduce | Use traces |

---

## 10. E2E тестування API (без браузера)

Playwright can also test APIs directly, which is useful for testing backend flows that don't have a UI:

### 10.1 API тестування з Playwright

```csharp
using Microsoft.Playwright.NUnit;
using Microsoft.Playwright;

[TestFixture]
public class OrderApiE2ETests : PlaywrightTest
{
    private IAPIRequestContext _api = null!;

    [SetUp]
    public async Task SetUp()
    {
        _api = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = "https://localhost:5001",
            ExtraHTTPHeaders = new Dictionary<string, string>
            {
                ["Accept"] = "application/json"
            }
        });
    }

    [Test]
    public async Task FullOrderLifecycle()
    {
        // Create
        var createResponse = await _api.PostAsync("/api/orders", new()
        {
            DataObject = new
            {
                CustomerName = "E2E Test",
                Product = "Widget",
                Quantity = 3
            }
        });
        Assert.That(createResponse.Status, Is.EqualTo(201));

        var created = await createResponse.JsonAsync();
        var orderId = created?.GetProperty("id").GetInt32();

        // Read
        var getResponse = await _api.GetAsync($"/api/orders/{orderId}");
        Assert.That(getResponse.Status, Is.EqualTo(200));

        // Update
        var updateResponse = await _api.PutAsync($"/api/orders/{orderId}", new()
        {
            DataObject = new
            {
                CustomerName = "E2E Test Updated",
                Product = "Widget",
                Quantity = 5
            }
        });
        Assert.That(updateResponse.Status, Is.EqualTo(200));

        // Delete
        var deleteResponse = await _api.DeleteAsync($"/api/orders/{orderId}");
        Assert.That(deleteResponse.Status, Is.EqualTo(204));

        // Verify deleted
        var verifyResponse = await _api.GetAsync($"/api/orders/{orderId}");
        Assert.That(verifyResponse.Status, Is.EqualTo(404));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _api.DisposeAsync();
    }
}
```

### 10.2 Комбінування API та UI тестів

The most powerful E2E tests combine API and UI:

```csharp
[Test]
public async Task OrderCreatedViaApi_AppearsInUi()
{
    // Arrange — create data via API (fast)
    var api = await Playwright.APIRequest.NewContextAsync(new()
    {
        BaseURL = "https://localhost:5001"
    });
    await api.PostAsync("/api/orders", new()
    {
        DataObject = new
        {
            CustomerName = "API-Created Order",
            Product = "Widget B",
            Quantity = 7
        }
    });

    // Act & Assert — verify via UI (realistic)
    await Page.GotoAsync("/orders");
    await Expect(Page.GetByText("API-Created Order")).ToBeVisibleAsync();
    await Expect(Page.GetByText("Widget B")).ToBeVisibleAsync();
}
```

```
This pattern:

  API (fast)           UI (realistic)
  ┌──────────┐         ┌──────────────┐
  │ Create   │────────►│ Verify       │
  │ test data│         │ user sees it │
  └──────────┘         └──────────────┘

  Best of both worlds:
  • Fast setup (no UI interaction for data creation)
  • Realistic validation (user perspective)
  • Tests the full stack (API → DB → UI)
```

---

## 11. Підсумок

### Ключові тези

| Topic | Key Takeaway |
|---|---|
| **What is E2E testing** | Tests the full system from the user's perspective |
| **Testing pyramid** | Few E2E tests, each covering a critical user journey |
| **Playwright** | Modern, fast, auto-waiting, multi-browser E2E framework for .NET |
| **Locators** | Prefer role, label, text-based selectors over CSS/XPath |
| **Page Object Model** | Encapsulate locators and actions; tests read like user stories |
| **Flakiness** | Auto-wait, isolate data, use traces to diagnose |
| **Authentication** | Use storage state to reuse login across tests |
| **CI/CD** | Run headless, upload traces as artifacts, use health checks |
| **Best practices** | Test critical paths, keep suite small, fix flaky tests immediately |

### Співвідношення тестів

```
Recommended distribution for a typical web application:

  E2E:          ████                          3-5%
  Integration:  ████████████████████          20-30%
  Unit:         ████████████████████████████  65-75%

Each level has a purpose. E2E ≠ "better" — it's "different."
Use the right level for each verification.
```

---

## Домашнє завдання

1. Set up a Playwright .NET project for the Lab6 Orders API
2. Write E2E tests for:
   - Creating an order through the API and verifying it appears in GET /api/orders
   - The full CRUD lifecycle (Create → Read → Update → Delete)
   - Error handling (creating an order with invalid data)
3. Implement the Page Object Model (or an equivalent abstraction for API tests)
4. Configure tests to run in a GitHub Actions workflow
5. Add trace capture for failed tests

---

## Додаткові ресурси

- [Playwright for .NET Documentation](https://playwright.dev/dotnet/)
- [Playwright Best Practices](https://playwright.dev/dotnet/docs/best-practices)
- [Page Object Model Pattern](https://playwright.dev/dotnet/docs/pom)
- [Martin Fowler: TestPyramid](https://martinfowler.com/bliki/TestPyramid.html)
- [Testing Trophy by Kent C. Dodds](https://kentcdodds.com/blog/the-testing-trophy-and-testing-classifications)
