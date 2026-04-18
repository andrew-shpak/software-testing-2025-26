# Лабораторна 5 — Тестування бази даних: Запити та міграції

> **Lab → 5 points**

## Мета

Навчитися тестувати взаємодію з базою даних за допомогою Entity Framework Core. Писати тести для репозиторіїв, запитів та міграцій з використанням тестових провайдерів InMemory та SQLite.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що:

- Встановлений .NET 10+ SDK (`dotnet --version`)
- Встановлений та запущений Docker (необхідний для Testcontainers)
- Ви маєте робоче розуміння Entity Framework Core (DbContext, DbSet, міграції, LINQ-запити)
- Ви знайомі з концепціями реляційних баз даних (зовнішні ключі, унікальні обмеження, каскадне видалення)
- Виконані Лабораторна 3 та Лабораторна 4 (основи інтеграційного тестування)

## Ключові поняття

### Провайдер EF Core InMemory

Провайдер InMemory (`Microsoft.EntityFrameworkCore.InMemory`) є найшвидшим варіантом для тестування базових CRUD-операцій та LINQ-запитів. Однак він **не** забезпечує реляційних обмежень (зовнішні ключі, унікальні індекси, каскадне видалення). Використовуйте його лише для тестів на рівні логіки.

### SQLite у режимі пам'яті

SQLite у режимі пам'яті (`DataSource=:memory:`) надає реальний реляційний двигун бази даних, що забезпечує зовнішні ключі, унікальні обмеження та каскади. База даних існує лише доки з'єднання відкрите, що робить очищення автоматичним. Це ідеальний варіант для тестів на рівні обмежень.

### Testcontainers

Testcontainers запускає реальний сервер бази даних (SQL Server, PostgreSQL тощо) у Docker-контейнері для кожного тестового класу. Це дає повну поведінку бази даних, включаючи збережені процедури, сирий SQL та семантику запитів, специфічну для провайдера. Тести з Testcontainers повільніші, але забезпечують найвищу точність.

### Ізоляція за назвою бази даних

При використанні провайдера InMemory кожен тест повинен використовувати унікальну назву бази даних (наприклад, через `Guid.NewGuid().ToString()`), щоб запобігти спільному стану між тестами. З SQLite створюйте нове з'єднання для кожного тесту. З Testcontainers використовуйте `IAsyncLifetime` для запуску/зупинки контейнера.

## Інструменти

- Мова: C#
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- Тестова БД: `Microsoft.EntityFrameworkCore.InMemory` / `Microsoft.EntityFrameworkCore.Sqlite`
- Контейнери: [Testcontainers для .NET](https://dotnet.testcontainers.org/)
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Налаштування

```bash
dotnet new sln -n Lab5
dotnet new classlib -n Lab5.Data
dotnet new classlib -n Lab5.Tests
dotnet sln add Lab5.Data Lab5.Tests
dotnet add Lab5.Data package Microsoft.EntityFrameworkCore
dotnet add Lab5.Data package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab5.Tests reference Lab5.Data
dotnet add Lab5.Tests package xunit.v3
dotnet add Lab5.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab5.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet add Lab5.Tests package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab5.Tests package Testcontainers.MsSql
dotnet add Lab5.Tests package Microsoft.EntityFrameworkCore.SqlServer
dotnet add Lab5.Tests package Shouldly
```

## Завдання

> **Примітка:** Завдання 1 (InMemory) та Завдання 2 (SQLite) винесено в **Лабораторну 4**. Ця лабораторна зосереджена на Testcontainers із реальною СУБД.

### Завдання 1 — Testcontainers з реальною базою даних

Використовуйте Testcontainers для запуску реального екземпляра SQL Server у Docker для тестів:

```csharp
private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .Build();

// In IAsyncLifetime.InitializeAsync:
await _dbContainer.StartAsync();
var connectionString = _dbContainer.GetConnectionString();
```

Напишіть тести, що:

1. Виконують усі CRUD-операції на реальному контейнері SQL Server
2. Перевіряють, що обмеження зовнішніх ключів забезпечуються (порівняйте з поведінкою InMemory)
3. Тестують виконання збережених процедур або сирих SQL-запитів
4. Перевіряють, що міграції EF застосовуються чисто до нової бази даних
5. Порівнюють поведінку запитів між SQLite та SQL Server (задокументуйте відмінності)

**Мінімальна кількість тестів: 4 тести**

> **Передумова**: Docker повинен бути встановлений та запущений.

#### Приклад: Налаштування Testcontainers з IAsyncLifetime

```csharp
public class SqlServerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private AppDbContext _context = null!;

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_dbContainer.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    [Fact]
    public async Task CrudOperations_WorkWithRealSqlServerAsync()
    {
        // Arrange
        var student = new Student
        {
            FullName = "Test User",
            Email = "test@sqlserver.com",
            EnrollmentDate = DateTime.UtcNow
        };

        // Act
        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        // Assert
        var found = await _context.Students.FindAsync(student.Id);
        found.ShouldNotBeNull();
        found.FullName.ShouldBe("Test User");
    }

    [Fact]
    public async Task RawSql_ReturnsExpectedResultsAsync()
    {
        // Arrange
        _context.Students.Add(new Student
        {
            FullName = "SQL User", Email = "sql@test.com",
            EnrollmentDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        // Act
        var students = await _context.Students
            .FromSqlRaw("SELECT * FROM Students WHERE FullName LIKE '%SQL%'")
            .ToListAsync();

        // Assert
        students.ShouldNotBeEmpty();
        students.First().FullName.ShouldContain("SQL");
    }

    [Fact]
    public async Task Migrations_ApplyCleanlyAsync()
    {
        // Act — drop and re-apply via migrations
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.MigrateAsync();

        // Assert — schema exists, can insert data
        _context.Students.Add(new Student
        {
            FullName = "Migration Test", Email = "migrate@test.com",
            EnrollmentDate = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var count = await _context.Students.CountAsync();
        count.ShouldBe(1);
    }
}
```

> **Підказка:** Тести з Testcontainers повільніші (запуск контейнера займає 10-30 секунд). Використовуйте `IAsyncLifetime` на рівні класу та спільний контейнер для тестів у межах одного класу. Позначайте ці тести `[Trait("Category", "Integration")]`, щоб їх можна було фільтрувати під час локальної розробки.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести з Testcontainers |
| Ізоляція тестів та правильні асинхронні шаблони |

## Здача роботи

- Рішення з проєктами `Lab5.Data` та `Lab5.Tests`
- Тестові класи для InMemory та SQLite

## Посилання

- [Огляд тестування EF Core](https://learn.microsoft.com/en-us/ef/core/testing/) — офіційний посібник з вибору стратегії тестування
- [Тестування з провайдером InMemory](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#inmemory-provider) — обмеження та використання
- [Тестування з SQLite](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory) — налаштування режиму пам'яті
- [Testcontainers для .NET](https://dotnet.testcontainers.org/) — тестова інфраструктура на основі контейнерів
- [Модуль Testcontainers для SQL Server](https://dotnet.testcontainers.org/modules/mssql/) — конфігурація контейнера SQL Server
- [Документація xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline) — довідник тестового фреймворку
- [Документація Shouldly](https://docs.shouldly.org/) — API бібліотеки перевірок
- [Зв'язки в EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) — налаштування зовнішніх ключів, каскадне видалення
- [Сирі SQL-запити в EF Core](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries) — `FromSqlRaw`, `ExecuteSqlRawAsync`
