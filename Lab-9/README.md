# Лабораторна 9 — End-to-End UI тестування: Playwright, Page Object Model та Page Factory

> **Lab → 11 points**

## Мета

Навчитися писати end-to-end (E2E) UI-тести для реального веб-додатку за допомогою **Playwright** (JavaScript) та патернів **Page Object Model (POM)** і **Page Factory**. Протестувати головну сторінку, сторінку входу та сторінку профілю користувача на `https://umsys.com.ua`, а також налаштувати CI-пайплайн у GitHub Actions для автоматичного запуску цих тестів.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений Node.js 20+ (`node --version`)
- Встановлений npm або pnpm
- Обліковий запис на [umsys.com.ua](https://umsys.com.ua) (для тестів автентифікації)
- Базові знання JavaScript: `async/await`, ES-модулі
- Розуміння DOM та CSS-селекторів
- Знайомство з GitHub Actions — хоча б на рівні читання YAML-конфігурацій

## Ключові концепції

| Концепція | Опис |
|-----------|------|
| **End-to-End (E2E) тест** | Тест, що імітує взаємодію реального користувача з UI — натискання, введення, навігацію. Перевіряє всю систему "наскрізно": браузер → фронтенд → API → БД. |
| **Playwright** | Сучасний фреймворк для автоматизації браузерів (Chromium, Firefox, WebKit). Пакет `@playwright/test` містить і test runner, і бібліотеку. Має авто-очікування та ізольовані контексти. |
| **Page Object Model (POM)** | Патерн, у якому кожна сторінка UI представлена окремим класом, що інкапсулює локатори та дії. Тести стають декларативними, а зміна верстки вимагає правок лише в одному місці. |
| **Page Factory** | Фабрика, що централізує створення об'єктів сторінок. Тест отримує `PageFactory`, а не конструює `HomePage`/`SignInPage`/`ProfilePage` вручну — це спрощує DI, кешування та підстановку моків. У Playwright (JS) фабрика часто віддається через **fixture**. |
| **Fixture** | Механізм Playwright Test для інʼєкції залежностей у тест. Через `test.extend` створюються власні фікстури (наприклад, `pages: PageFactory`), що автоматично передаються у тест. |
| **Локатор** | Стратегія пошуку елемента на сторінці (`getByRole`, `getByText`, `getByTestId`, CSS). Рекомендовано обирати user-facing локатори (роль, текст, мітка) — вони стабільніші за CSS-класи. |
| **Storage State** | JSON-файл із cookies та localStorage, що дозволяє перевикористовувати авторизацію між тестами без повторного входу (через `storageState` у `use` або `playwright.config`). |
| **Trace Viewer** | Запис кроків тесту з DOM-знімками, консольними повідомленнями та скриншотами. Увімкнення: `trace: 'on-first-retry'` у конфігу. Незамінний для діагностики падінь у CI. |

## Інструменти

- Мова: **JavaScript** (Node.js 20+)
- Фреймворк UI-тестів: [`@playwright/test`](https://playwright.dev/docs/intro)
- CI: [GitHub Actions](https://docs.github.com/en/actions)
- SUT: [umsys.com.ua](https://umsys.com.ua)

## Налаштування

```bash
mkdir Lab9.E2E && cd Lab9.E2E
npm init -y
npm i -D @playwright/test
npx playwright install --with-deps        # усі 3 браузери: chromium, firefox, webkit
npx playwright install-deps               # за потреби системних залежностей
```

Додайте у `package.json`:

```json
{
  "type": "module",
  "scripts": {
    "test": "playwright test",
    "test:headed": "playwright test --headed",
    "report": "playwright show-report"
  }
}
```

Створіть `playwright.config.js`:

```js
// playwright.config.js
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: [['html', { open: 'never' }], ['list']],
  use: {
    baseURL: 'https://umsys.com.ua',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome']  } },
    { name: 'firefox',  use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit',   use: { ...devices['Desktop Safari']  } },
  ],
});
```

## Структура проєкту

```
Lab9.E2E/
├── pages/
│   ├── base-page.js
│   ├── home-page.js
│   ├── sign-in-page.js
│   ├── profile-page.js
│   └── page-factory.js
├── fixtures/
│   └── pages.js          # test.extend з PageFactory + storageState
├── tests/
│   ├── home.spec.js
│   ├── sign-in.spec.js
│   └── profile.spec.js
├── .auth/
│   └── user.json         # storageState (в .gitignore!)
├── playwright.config.js
└── package.json
```

## Завдання

### Завдання 1 — Page Object базовий клас, Page Factory та головна сторінка (`umsys.com.ua`)

#### 1.1 BasePage

```js
// pages/base-page.js
export class BasePage {
  /** @param {import('@playwright/test').Page} page */
  constructor(page) {
    this.page = page;
  }

  /** Дочірні класи перевизначають */
  get path() { return '/'; }

  async goto() {
    return this.page.goto(this.path, { waitUntil: 'networkidle' });
  }

  title() { return this.page.title(); }
}
```

#### 1.2 HomePage

```js
// pages/home-page.js
import { BasePage } from './base-page.js';

export class HomePage extends BasePage {
  get path() { return '/'; }

  get signInLink() {
    return this.page.getByRole('link', { name: /sign[- ]?in|увійти/i });
  }

  get heroHeading() {
    return this.page.getByRole('heading').first();
  }

  async openSignIn() {
    await this.signInLink.click();
  }
}
```

#### 1.3 PageFactory

```js
// pages/page-factory.js
import { HomePage }    from './home-page.js';
import { SignInPage }  from './sign-in-page.js';
import { ProfilePage } from './profile-page.js';

export class PageFactory {
  /** @param {import('@playwright/test').Page} page */
  constructor(page) {
    this.page = page;
  }

  home()           { return new HomePage(this.page); }
  signIn()         { return new SignInPage(this.page); }
  profile(email)   { return new ProfilePage(this.page, email); }
}
```

> **Чому фабрика?** Тест не знає про конкретні конструктори. Якщо сторінці знадобиться залежність (логер, конфіг, перемикач фіч) — зміниться лише фабрика, а не десятки тестових файлів.

#### 1.4 Fixture з PageFactory

```js
// fixtures/pages.js
import { test as base, expect } from '@playwright/test';
import { PageFactory } from '../pages/page-factory.js';

export const test = base.extend({
  pages: async ({ page }, use) => {
    await use(new PageFactory(page));
  },
});

export { expect };
```

#### 1.5 Тести головної сторінки

```js
// tests/home.spec.js
import { test, expect } from '../fixtures/pages.js';

test.describe('Home page', () => {
  test('loads successfully and shows a non-empty title', async ({ pages, page }) => {
    const response = await page.goto('/', { waitUntil: 'networkidle' });

    expect(response?.status()).toBe(200);
    expect(await pages.home().title()).not.toBe('');
    await expect(pages.home().heroHeading).toBeVisible();
  });

  test('sign-in link navigates to /sign-in', async ({ pages, page }) => {
    const home = pages.home();
    await home.goto();

    await home.openSignIn();

    await expect(page).toHaveURL(/\/sign-in\/?$/);
  });

  test('has a visible sign-in link on load', async ({ pages }) => {
    const home = pages.home();
    await home.goto();

    await expect(home.signInLink).toBeVisible();
  });
});
```

**Мінімальна кількість тестів для Завдання 1**: 3 тести.

### Завдання 2 — Сторінка входу (`umsys.com.ua/sign-in`)

```js
// pages/sign-in-page.js
import { BasePage } from './base-page.js';

export class SignInPage extends BasePage {
  get path() { return '/sign-in'; }

  get emailInput()    { return this.page.getByLabel(/e-?mail/i); }
  get passwordInput() { return this.page.getByLabel(/password|пароль/i); }
  get submitButton()  { return this.page.getByRole('button', { name: /sign in|увійти|log in/i }); }
  get errorMessage()  { return this.page.getByRole('alert'); }

  async login(email, password) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }
}
```

Тести для `/sign-in` **не перевіряють фактичну автентифікацію** — жодних реальних облікових даних тут не використовується. Ми перевіряємо лише **структуру сторінки та клієнтську поведінку форми**. Логін як потік виконується один раз у `auth.setup.js` (Завдання 3).

```js
// tests/sign-in.spec.js
import { test, expect } from '../fixtures/pages.js';

test.describe('Sign in page (no auth)', () => {
  test.beforeEach(async ({ pages }) => {
    await pages.signIn().goto();
  });

  test('form renders with email, password and submit', async ({ pages }) => {
    const signIn = pages.signIn();

    await expect(signIn.emailInput).toBeVisible();
    await expect(signIn.passwordInput).toBeVisible();
    await expect(signIn.submitButton).toBeVisible();
  });

  test('email input has type="email" and password is masked', async ({ pages }) => {
    const signIn = pages.signIn();

    await expect(signIn.emailInput).toHaveAttribute('type', 'email');
    await expect(signIn.passwordInput).toHaveAttribute('type', 'password');
  });

  test('submitting empty form keeps user on /sign-in (no network call)', async ({ pages, page }) => {
    const signIn = pages.signIn();

    await signIn.submitButton.click();

    // Жодного переходу не відбулось — або HTML5-валідація, або клієнтська перевірка.
    await expect(page).toHaveURL(/\/sign-in/);
  });
});
```

**Мінімальна кількість тестів для Завдання 2**: 3 тести — усі **без** реальних облікових даних.

> **Чому без auth?** Фактичний логін — це відповідальність `auth.setup.js` у Завданні 3: він робить це один раз і зберігає сесію у `storageState`. Дублювати логін у тестах сторінки `/sign-in` зайве і крихке (капчі, rate limits, зміни backend). Тут ми тестуємо **сторінку**, а не **систему автентифікації**.

### Завдання 3 — Сторінка профілю (`umsys.com.ua/<your-email>`)

Сторінка профілю доступна лише автентифікованим користувачам. Замість повторного логіну в кожному тесті створіть одноразовий **authentication setup**, який збереже `storageState` у `.auth/user.json`, і перевикористайте його через окремий проєкт `playwright.config`.

#### 3.1 Setup-проєкт в конфігу

```js
// playwright.config.js (розширення)
projects: [
  { name: 'setup', testMatch: /.*\.setup\.js/ },

  {
    name: 'chromium',
    dependencies: ['setup'],
    use: { ...devices['Desktop Chrome'],  storageState: '.auth/user.json' },
  },
  {
    name: 'firefox',
    dependencies: ['setup'],
    use: { ...devices['Desktop Firefox'], storageState: '.auth/user.json' },
  },
  {
    name: 'webkit',
    dependencies: ['setup'],
    use: { ...devices['Desktop Safari'],  storageState: '.auth/user.json' },
  },
],
```

> **Примітка:** `setup` виконується один раз для сесії, а `.auth/user.json` перевикористовується усіма трьома browser-проєктами. Тести запускаються паралельно в Chromium, Firefox та WebKit.

#### 3.2 Auth setup

```js
// tests/auth.setup.js
import { test as setup, expect } from '@playwright/test';
import { SignInPage } from '../pages/sign-in-page.js';

const authFile = '.auth/user.json';

setup('authenticate', async ({ page }) => {
  const email = process.env.UMSYS_EMAIL;
  const password = process.env.UMSYS_PASSWORD;
  if (!email || !password) {
    throw new Error('UMSYS_EMAIL / UMSYS_PASSWORD must be set');
  }

  await page.goto('/sign-in');
  const signIn = new SignInPage(page);
  await signIn.login(email, password);

  await page.waitForURL(new RegExp(`/${email.replace(/[.@+]/g, '\\$&')}/?$`));
  await page.context().storageState({ path: authFile });
});
```

#### 3.3 ProfilePage

```js
// pages/profile-page.js
import { BasePage } from './base-page.js';

export class ProfilePage extends BasePage {
  constructor(page, email) {
    super(page);
    this.email = email;
  }

  get path() { return `/${this.email}`; }

  get profileHeading() { return this.page.getByRole('heading').first(); }
  get signOutButton()  { return this.page.getByRole('button', { name: /sign out|вийти|log out/i }); }

  async signOut() { await this.signOutButton.click(); }
}
```

#### 3.4 Тести профілю

```js
// tests/profile.spec.js
import { test, expect } from '../fixtures/pages.js';

const EMAIL = process.env.UMSYS_EMAIL;

test.describe('Profile page (authenticated)', () => {
  test('loads for authenticated user', async ({ pages, page }) => {
    test.skip(!EMAIL, 'UMSYS_EMAIL not set');
    const profile = pages.profile(EMAIL);
    await profile.goto();

    await expect(page).toHaveURL(new RegExp(`/${EMAIL.replace(/[.@+]/g, '\\$&')}/?$`));
    await expect(profile.profileHeading).toBeVisible();
  });

  test('sign-out returns to sign-in or home', async ({ pages, page }) => {
    test.skip(!EMAIL, 'UMSYS_EMAIL not set');
    const profile = pages.profile(EMAIL);
    await profile.goto();

    await profile.signOut();

    await expect(page).toHaveURL(/\/(sign-in)?\/?$/);
  });
});

test.describe('Profile page (anonymous)', () => {
  test.use({ storageState: { cookies: [], origins: [] } }); // без сесії

  test('anonymous visitor is redirected to /sign-in', async ({ page }) => {
    test.skip(!EMAIL, 'UMSYS_EMAIL not set');
    await page.goto(`/${EMAIL}`);

    await expect(page).toHaveURL(/\/sign-in/);
  });
});
```

**Мінімальна кількість тестів для Завдання 3**: 3 тести (2 автентифіковані + 1 анонімний редірект).

> **Підказка:** `test.use({ storageState: { cookies: [], origins: [] } })` скидає авторизацію саме для цього `describe`-блоку, хоча глобальний конфіг використовує `.auth/user.json`.

### Завдання 4 — CI у GitHub Actions

Створіть workflow `.github/workflows/lab9-e2e.yml` для автоматичного запуску UI-тестів у CI.

```yaml
# .github/workflows/lab9-e2e.yml
name: Lab-9 E2E (Playwright)

on:
  push:
    branches: [ "**" ]
  workflow_dispatch:

jobs:
  e2e:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        browser: [chromium, firefox, webkit]
    defaults:
      run:
        working-directory: Lab-9/Lab9.E2E
    env:
      UMSYS_EMAIL: ${{ secrets.UMSYS_EMAIL }}
      UMSYS_PASSWORD: ${{ secrets.UMSYS_PASSWORD }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: Lab-9/Lab9.E2E/package-lock.json

      - name: Install dependencies
        run: npm ci

      - name: Install Playwright browser (${{ matrix.browser }})
        run: npx playwright install --with-deps ${{ matrix.browser }}

      - name: Run Playwright tests on ${{ matrix.browser }}
        run: npx playwright test --project=setup --project=${{ matrix.browser }}

      - name: Upload Playwright report (${{ matrix.browser }})
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report-${{ matrix.browser }}
          path: Lab-9/Lab9.E2E/playwright-report/
          retention-days: 7

      - name: Upload traces on failure (${{ matrix.browser }})
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-traces-${{ matrix.browser }}
          path: Lab-9/Lab9.E2E/test-results/
          retention-days: 7
```

> **Секрети у GitHub:** Settings → Secrets and variables → Actions → New repository secret. Створіть `UMSYS_EMAIL` та `UMSYS_PASSWORD`. Перевірте, що у `.gitignore` є `.auth/` та `node_modules/` — вони **не** мають потрапляти в репозиторій.

## Оцінювання

**Lab → 11 points**

| Критерії | Бали |
|----------|:----:|
| Завдання 1 — `BasePage`, `HomePage`, `PageFactory`, fixture + 3 тести | 3 |
| Завдання 2 — `SignInPage` + 3 тести (форма, помилка, успіх) | 3 |
| Завдання 3 — `ProfilePage` + `auth.setup.js` із `storageState` + 3 тести | 3 |
| Завдання 4 — GitHub Actions workflow із секретами та артефактами | 2 |
| **Разом** | **11** |

## Здача роботи

- Проєкт `Lab9.E2E` з `package.json`, `playwright.config.js` та структурою `pages/ fixtures/ tests/`
- Працюючий `.github/workflows/lab9-e2e.yml` (успішний run у вашому форку)
- Посилання на зелений CI run
- `.auth/user.json` та `node_modules/` у `.gitignore`

## Посилання

- [Playwright — Getting Started](https://playwright.dev/docs/intro)
- [Playwright — Locators](https://playwright.dev/docs/locators)
- [Playwright — Page Object Models](https://playwright.dev/docs/pom)
- [Playwright — Test Fixtures](https://playwright.dev/docs/test-fixtures)
- [Playwright — Authentication](https://playwright.dev/docs/auth)
- [Playwright — Trace Viewer](https://playwright.dev/docs/trace-viewer)
- [Playwright — CI: GitHub Actions](https://playwright.dev/docs/ci-intro)
- [GitHub Actions — Encrypted Secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets)
