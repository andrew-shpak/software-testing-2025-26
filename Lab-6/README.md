# Лабораторна 6 — Тестування баз даних: цілісність даних та транзакції

## Мета

Тестування транзакційної поведінки, наповнення даними, коректності міграцій та шаблонів репозиторіїв з реальними обмеженнями бази даних.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що у вас є:

- Встановлений .NET 10+ SDK (`dotnet --version`)
- Встановлений та запущений Docker (необхідний для Testcontainers PostgreSQL)
- Розуміння транзакцій бази даних, властивостей ACID та поведінки EF Core `SaveChanges`
- Знайомство з міграціями EF Core, початковими даними та перехоплювачами/перевизначенням `SaveChanges`
- Виконана Лабораторна 5 (тестування баз даних з InMemory, SQLite та Testcontainers)

## Ключові концепції

### Транзакції ACID

Транзакція групує кілька операцій з базою даних в єдину атомарну одиницю. Або всі операції успішні (фіксація), або всі відкочуються. У цій лабораторній `TransferService` має гарантувати, що гроші ніколи не втрачаються: якщо списання з рахунку відправника успішне, але зарахування на рахунок отримувача невдале, обидві зміни мають бути скасовані.

### Оптимістичний vs песимістичний контроль паралелізму

Коли кілька потоків або запитів одночасно змінюють один і той самий рядок, можуть виникати конфлікти. EF Core підтримує оптимістичний контроль паралелізму через `[ConcurrencyCheck]` або стовпці версії рядка. База даних відхиляє оновлення, де рядок змінився з моменту останнього зчитування. Це критично для запобігання від'ємних балансів при паралельних переказах.

### Наповнення даними

Метод `HasData()` EF Core у `OnModelCreating` дозволяє визначити початкові дані, які застосовуються під час виконання міграцій. Тести повинні перевіряти, що початкові дані існують після міграції та що зміни схеми (додавання/видалення стовпців) зберігають існуючі дані.

### Аудит через перевизначення SaveChanges

Перевизначивши `SaveChanges` або використовуючи перехоплювачі EF Core, ви можете автоматично записувати кожну зміну сутності (створення, оновлення, видалення) до таблиці `AuditLog`. Запис аудиту повинен бути записаний у тій самій транзакції, що й зміна сутності, забезпечуючи узгодженість.

## Інструменти

- Мова: C#
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- Тестова БД: SQLite in-memory / [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Налаштування

```bash
dotnet new sln -n Lab6
dotnet new classlib -n Lab6.Data
dotnet new classlib -n Lab6.Tests
dotnet sln add Lab6.Data Lab6.Tests
dotnet add Lab6.Data package Microsoft.EntityFrameworkCore
dotnet add Lab6.Data package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab6.Tests reference Lab6.Data
dotnet add Lab6.Tests package xunit.v3
dotnet add Lab6.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab6.Tests package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab6.Tests package Testcontainers.PostgreSql
dotnet add Lab6.Tests package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add Lab6.Tests package Shouldly
```

## Завдання

### Завдання 1 — Тестування транзакцій з Testcontainers

Створіть `BankAccountService`, який керує переказами між рахунками:

```csharp
public class BankAccount
{
    public int Id { get; set; }
    public string AccountNumber { get; set; }
    public string OwnerName { get; set; }
    public decimal Balance { get; set; }
}

public class TransferService
{
    Task<TransferResult> TransferAsync(int fromAccountId, int toAccountId, decimal amount);
}
```

Використовуйте Testcontainers для запуску реального екземпляра PostgreSQL:

```csharp
private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();
```

Напишіть тести, які перевіряють:

1. Успішний переказ зменшує баланс відправника та збільшує баланс отримувача
2. Переказ з недостатніми коштами відхиляється, і обидва баланси залишаються незмінними (відкат)
3. Переказ на неіснуючий рахунок невдалий, і баланс відправника не змінюється
4. Переказ нульової або від'ємної суми відхиляється
5. Паралельні перекази з одного рахунку не призводять до від'ємного балансу

**Мінімальна кількість тестів: 5 тестів**

> **Передумова**: Docker має бути встановлений та запущений.

#### Приклад: налаштування Testcontainers PostgreSQL

```csharp
public class TransferServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;
    private TransferService _service = null!;

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // Наповнення тестовими рахунками
        _context.BankAccounts.AddRange(
            new BankAccount { AccountNumber = "ACC-001", OwnerName = "Alice", Balance = 1000m },
            new BankAccount { AccountNumber = "ACC-002", OwnerName = "Bob", Balance = 500m }
        );
        await _context.SaveChangesAsync();

        _service = new TransferService(_context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
```

#### Приклад: тести транзакцій з Shouldly

```csharp
[Fact]
public async Task Transfer_ValidAmount_UpdatesBothBalancesAsync()
{
    // Arrange
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act
    var result = await _service.TransferAsync(alice.Id, bob.Id, 200m);

    // Assert
    result.Success.ShouldBeTrue();

    // Перезавантажити з бази даних для перевірки збереженого стану
    await _context.Entry(alice).ReloadAsync();
    await _context.Entry(bob).ReloadAsync();

    alice.Balance.ShouldBe(800m);  // 1000 - 200
    bob.Balance.ShouldBe(700m);    // 500 + 200
}

[Fact]
public async Task Transfer_InsufficientFunds_RollsBackAsync()
{
    // Arrange
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act
    var result = await _service.TransferAsync(alice.Id, bob.Id, 5000m); // У Alice лише 1000

    // Assert
    result.Success.ShouldBeFalse();

    // Перевірка відкату — баланси не змінились
    await _context.Entry(alice).ReloadAsync();
    await _context.Entry(bob).ReloadAsync();

    alice.Balance.ShouldBe(1000m);
    bob.Balance.ShouldBe(500m);
}

[Fact]
public async Task Transfer_ToNonExistingAccount_FailsAndSenderUnchangedAsync()
{
    // Arrange
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");

    // Act
    var result = await _service.TransferAsync(alice.Id, toAccountId: 999, amount: 100m);

    // Assert
    result.Success.ShouldBeFalse();

    await _context.Entry(alice).ReloadAsync();
    alice.Balance.ShouldBe(1000m); // не змінився
}

[Fact]
public async Task Transfer_NegativeAmount_IsRejectedAsync()
{
    // Arrange
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act
    var result = await _service.TransferAsync(alice.Id, bob.Id, -50m);

    // Assert
    result.Success.ShouldBeFalse();
}

[Fact]
public async Task Transfer_ZeroAmount_IsRejectedAsync()
{
    // Arrange
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act
    var result = await _service.TransferAsync(alice.Id, bob.Id, 0m);

    // Assert
    result.Success.ShouldBeFalse();
}

[Fact]
public async Task ConcurrentTransfers_DoNotCauseNegativeBalanceAsync()
{
    // Arrange — у Alice 1000, спроба двох паралельних переказів по 600
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act — запуск двох паралельних переказів
    var task1 = Task.Run(() =>
    {
        using var ctx1 = CreateNewContext();
        var svc1 = new TransferService(ctx1);
        return svc1.TransferAsync(alice.Id, bob.Id, 600m);
    });
    var task2 = Task.Run(() =>
    {
        using var ctx2 = CreateNewContext();
        var svc2 = new TransferService(ctx2);
        return svc2.TransferAsync(alice.Id, bob.Id, 600m);
    });

    var results = await Task.WhenAll(task1, task2);

    // Assert — максимум один має бути успішним
    var successCount = results.Count(r => r.Success);
    successCount.ShouldBeLessThanOrEqualTo(1);

    // Перевірка, що баланс Alice ніколи не стає від'ємним
    await _context.Entry(alice).ReloadAsync();
    alice.Balance.ShouldBeGreaterThanOrEqualTo(0m);
}
```

#### Таблиця очікуваної поведінки

| Сценарій | Очікуваний результат | Баланс відправника | Баланс отримувача |
|---|---|---|---|
| Переказ 200 (у відправника 1000) | Успіх | 800 | +200 |
| Переказ 5000 (у відправника 1000) | Невдача (відкат) | 1000 (не змінився) | не змінився |
| Переказ на неіснуючий рахунок | Невдача | не змінився | Н/Д |
| Переказ від'ємної суми | Невдача (відхилено) | не змінився | не змінився |
| Переказ нульової суми | Невдача (відхилено) | не змінився | не змінився |
| Два паралельних перекази по 600 (у відправника 1000) | Максимум один успішний | >= 0 | варіюється |

> **Підказка:** Для тестування паралельних переказів кожна паралельна задача повинна використовувати власний екземпляр `DbContext` (контексти EF Core не є потокобезпечними). Використовуйте `IDbContextFactory<AppDbContext>` або створюйте нові контексти вручну з тим самим рядком підключення. Розгляньте використання `[ConcurrencyCheck]` на властивості `Balance` або стовпця версії рядка `[Timestamp]`.

### Завдання 2 — Тестування аудиту

Реалізуйте аудит за допомогою перехоплювачів EF Core або перевизначення `SaveChanges`:

```csharp
public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; }
    public string Action { get; set; }       // "Created", "Updated", "Deleted"
    public string Changes { get; set; }       // JSON змінених властивостей
    public DateTime Timestamp { get; set; }
}
```

Напишіть тести, які перевіряють:

1. Створення сутності генерує запис аудиту з `Action = "Created"`
2. Оновлення сутності записує змінені властивості
3. Видалення сутності генерує запис з `Action = "Deleted"`
4. Запис аудиту записується в тій самій транзакції, що й зміна сутності

**Мінімальна кількість тестів: 4 тести**

#### Приклад: перевизначення SaveChanges для аудиту

```csharp
public class AuditableDbContext : DbContext
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog &&
                        e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var audit = new AuditLog
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Updated",
                    EntityState.Deleted => "Deleted",
                    _ => "Unknown"
                },
                Changes = JsonSerializer.Serialize(
                    entry.Properties
                        .Where(p => entry.State == EntityState.Added || p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString())),
                Timestamp = DateTime.UtcNow
            };
            AuditLogs.Add(audit);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

#### Приклад: тести аудиту з Shouldly

```csharp
[Fact]
public async Task CreateEntity_GeneratesAuditLogWithCreatedActionAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var account = new BankAccount
        {
            AccountNumber = "AUDIT-001", OwnerName = "Test", Balance = 500m
        };

        // Act
        context.BankAccounts.Add(account);
        await context.SaveChangesAsync();

        // Assert
        var logs = await context.AuditLogs.ToListAsync();
        logs.Count.ShouldBe(1);
        logs.First().EntityName.ShouldBe("BankAccount");
        logs.First().Action.ShouldBe("Created");
        logs.First().Timestamp.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }
}

[Fact]
public async Task UpdateEntity_LogsChangedPropertiesAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var account = new BankAccount
        {
            AccountNumber = "AUDIT-002", OwnerName = "Before", Balance = 100m
        };
        context.BankAccounts.Add(account);
        await context.SaveChangesAsync();

        // Очистити журнали аудиту від створення
        context.AuditLogs.RemoveRange(context.AuditLogs);
        await context.SaveChangesAsync(); // це також згенерує журнали, обробіть відповідно

        // Act
        account.OwnerName = "After";
        account.Balance = 200m;
        await context.SaveChangesAsync();

        // Assert
        var updateLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == "Updated");
        updateLog.ShouldNotBeNull();
        updateLog.Changes.ShouldContain("OwnerName");
        updateLog.Changes.ShouldContain("After");
    }
}

[Fact]
public async Task DeleteEntity_GeneratesDeletedAuditLogAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var account = new BankAccount
        {
            AccountNumber = "AUDIT-003", OwnerName = "ToDelete", Balance = 0m
        };
        context.BankAccounts.Add(account);
        await context.SaveChangesAsync();

        // Act
        context.BankAccounts.Remove(account);
        await context.SaveChangesAsync();

        // Assert
        var deleteLog = await context.AuditLogs
            .FirstOrDefaultAsync(l => l.Action == "Deleted");
        deleteLog.ShouldNotBeNull();
        deleteLog.EntityName.ShouldBe("BankAccount");
    }
}

[Fact]
public async Task AuditLog_WrittenInSameTransactionAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        // Act — створення сутності (запис аудиту має бути збережений атомарно)
        context.BankAccounts.Add(new BankAccount
        {
            AccountNumber = "AUDIT-004", OwnerName = "Atomic", Balance = 100m
        });
        await context.SaveChangesAsync();

        // Assert — і сутність, і запис аудиту існують (одна транзакція)
        var accountExists = await context.BankAccounts.AnyAsync(a => a.AccountNumber == "AUDIT-004");
        var auditExists = await context.AuditLogs.AnyAsync(l => l.EntityName == "BankAccount" && l.Action == "Created");

        accountExists.ShouldBeTrue();
        auditExists.ShouldBeTrue();
    }
}
```

#### Таблиця очікуваної поведінки — аудит

| Операція | AuditLog.Action | AuditLog.EntityName | AuditLog.Changes |
|---|---|---|---|
| Додавання нового BankAccount | `"Created"` | `"BankAccount"` | JSON з усіма значеннями властивостей |
| Оновлення OwnerName | `"Updated"` | `"BankAccount"` | JSON, що містить `"OwnerName"` |
| Видалення BankAccount | `"Deleted"` | `"BankAccount"` | JSON зі значеннями видаленої сутності |

> **Підказка:** Будьте обережні з перевизначенням `SaveChanges` — не генеруйте записи аудиту для змін самої сутності `AuditLog`, інакше створите нескінченний цикл. Відфільтруйте записи `AuditLog` у запиті до `ChangeTracker`. Також зверніть увагу, що записи `AuditLog` додаються до того самого пакету `SaveChanges`, тому вони автоматично беруть участь у тій самій транзакції бази даних.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести транзакцій |
| Завдання 2 — Тести аудиту |
| Коректна перевірка відкату транзакцій |
| Ізоляція тестів та очищення |

## Здача роботи

- Рішення з проєктами `Lab6.Data` та `Lab6.Tests`
- Усі транзакційні тести повинні доводити атомарність (перевірені обидві сторони транзакції)

## Посилання

- [EF Core Transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — явні транзакції, `BeginTransaction`, поведінка `SaveChanges`
- [EF Core Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) — оптимістичний контроль паралелізму, `[ConcurrencyCheck]`, версії рядків
- [EF Core Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) — `HasData()` у `OnModelCreating`
- [EF Core Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) — `SaveChangesInterceptor`, шаблони аудиту
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — тестова інфраструктура на основі контейнерів
- [Testcontainers PostgreSQL Module](https://dotnet.testcontainers.org/modules/postgres/) — конфігурація контейнера PostgreSQL
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) — довідник тестового фреймворку
- [Shouldly Documentation](https://docs.shouldly.org/) — API бібліотеки тверджень
- [Npgsql EF Core Provider](https://www.npgsql.org/efcore/) — специфічні для PostgreSQL можливості EF Core
