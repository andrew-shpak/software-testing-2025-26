# Лекція 4: Тестування бази даних з Testcontainers

## Навчальні цілі

Після завершення цієї лекції студенти зможуть:

- Пояснити, чому тестування бази даних важливе та які виклики з цим пов'язані
- Порівняти підходи до тестування БД: EF Core InMemory, SQLite та реальні контейнери
- Налаштувати та використовувати Testcontainers для SQL Server та PostgreSQL у C#
- Писати інтеграційні тести з реальними СУБД, використовуючи xUnit v3 та Shouldly
- Тестувати патерни репозиторіїв, міграції, запити, транзакції та конкурентність
- Застосовувати стратегії ізоляції тестів: окрема БД на тест vs. відкат транзакцій
- Керувати життєвим циклом контейнерів за допомогою `IAsyncLifetime`
- Оптимізувати продуктивність тестів за допомогою повторного використання контейнерів

---

## 1. Чому тестування бази даних має значення

### 1.1 Дані — основа більшості додатків

Майже кожен нетривіальний додаток зберігає та отримує дані. Рівень бази даних — це де:

- Бізнес-правила примусово виконуються (обмеження, тригери, збережені процедури)
- Підтримується цілісність даних (зовнішні ключі, унікальні індекси)
- Відбуваються критичні для продуктивності операції (складні запити, агрегації)
- Виникають конфлікти конкурентності (взаємні блокування, втрачені оновлення)

```
┌──────────────────────────────────────────────────┐
│              Typical Application                 │
│                                                  │
│   UI ──► API ──► Services ──► Repository ──► DB  │
│                                              ▲   │
│                                              │   │
│                               Constraints    │   │
│                               Indexes        │   │
│                               Triggers       │   │
│                               Transactions   │   │
│                               Migrations     │   │
│                                              │   │
│                            Most bugs hide HERE   │
└──────────────────────────────────────────────────┘
```

Якщо ваші тести ніколи не торкаються реальної бази даних, ви залишаєте значну частину додатку непротестованою. Модульні тести з замокованими репозиторіями перевіряють коректність логіки сервісів, але нічого не говорять про:

- Чи перекладаються ваші LINQ-запити у валідний SQL
- Чи коректні ваші EF Core маппінги
- Чи застосовуються ваші міграції чисто
- Чи справді ваші обмеження запобігають поганим даним
- Чи добре працюють ваші запити при реалістичних обсягах даних

### 1.2 Що може піти не так без тестування бази даних

| Категорія | Приклад |
|---|---|
| Помилки маппінгу | EF Core мовчки ігнорує властивість, дані ніколи не зберігаються |
| Трансляція запитів | LINQ-вираз працює in-memory, але зазнає невдачі при трансляції в SQL |
| Дрейф міграцій | Скрипт міграції ламається на продакшн-схемі |
| Порушення обмежень | Конфлікт унікального індексу не виявлений до продакшну |
| Помилки транзакцій | Часткові оновлення залишають дані в неузгодженому стані |
| Проблеми продуктивності | Запит N+1 видимий лише на реальній БД з реалістичними даними |
| Поведінка, специфічна для провайдера | `LIKE` чутливий до регістру в PostgreSQL, але не в SQL Server |

> **Обговорення (5 хв):** Чи стикалися ви з помилкою, яка з'являлася лише при роботі з реальною базою даних? Що сталося, і як тестування могло б виявити її раніше?

---

## 2. Виклики тестування бази даних

### 2.1 Три основні виклики

```
Виклик 1: СТАН                   Виклик 2: ІЗОЛЯЦІЯ              Виклик 3: ШВИДКІСТЬ
──────────────────                ─────────────────────            ────────────────────
Бази даних зберігають стан.       Тести не повинні впливати       Запуск реальної БД
Кожен тест може залишити          один на одного. Дані             повільний. Виконання
дані, що впливають                тесту A не повинні               сотень тестів
на наступний тест.                потрапляти в тест B.             з БД може зайняти
                                                                   хвилини.

Рішення:                          Рішення:                         Рішення:
Скидати стан між                  Окремі бази даних,               Повторне використання
тестами або                       відкат транзакцій,               контейнерів, паралельне
використовувати відкат.           або скрипти очищення.            виконання, легкі провайдери.
```

### 2.2 Піраміда тестування та тести бази даних

Тести бази даних знаходяться на рівні **інтеграційних тестів** піраміди тестування:

```
         /\
        /  \          E2E тести (мало, повільні, дорогі)
       /    \
      /──────\
     /        \       Інтеграційні тести ◄── Тести БД знаходяться тут
    /          \
   /────────────\
  /              \    Модульні тести (багато, швидкі, дешеві)
 /________________\
```

Вони повільніші за модульні тести, але забезпечують більшу впевненість у коректній роботі рівня даних. Мета — мати **достатньо** тестів бази даних для покриття критичних шляхів, не роблячи набір тестів нестерпно повільним.

### 2.3 Спектр точності

Різні підходи до тестування балансують між швидкістю та точністю (наскільки тестове середовище відповідає продакшну):

```
Швидкість   ████████████████████████  Швидко
            ████████████████
            ████████████
            ████████
Точність    ████████████████████████  Висока

            ┌──────────────┐
            │  EF Core     │  Найшвидший, найнижча точність
            │  InMemory    │  Без SQL, без обмежень
            ├──────────────┤
            │  SQLite      │  Швидкий, середня точність
            │  In-Memory   │  Реальний SQL, деякі обмеження
            ├──────────────┤
            │ Testcontainers│  Повільніший запуск, найвища точність
            │ (Реальна БД)  │  Ідентично продакшну
            └──────────────┘
```

> **Обговорення (5 хв):** З огляду на описані компроміси, коли б ви обрали кожен підхід? Чи існує універсальне рішення?

---

## 3. Підхід 1: EF Core InMemory провайдер

### 3.1 Що таке InMemory провайдер?

EF Core InMemory провайдер замінює реальну базу даних на in-memory сховище даних. Він реалізує `IQueryable` безпосередньо в .NET без перекладу в SQL.

```bash
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### 3.2 Налаштування та використання

```csharp
// Domain model
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
}

// DbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.Name).IsUnique();
        });
    }
}
```

```csharp
// Repository
public class ProductRepository(AppDbContext context)
{
    public async Task<Product?> GetByIdAsync(int id)
        => await context.Products.FindAsync(id);

    public async Task<List<Product>> GetByCategoryAsync(string category)
        => await context.Products
            .Where(p => p.Category == category)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public async Task<int> AddAsync(Product product)
    {
        context.Products.Add(product);
        return await context.SaveChangesAsync();
    }

    public async Task<List<Product>> GetExpensiveProductsAsync(decimal minPrice)
        => await context.Products
            .Where(p => p.Price >= minPrice)
            .OrderByDescending(p => p.Price)
            .ToListAsync();
}
```

```csharp
// Test with InMemory provider
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ProductRepositoryInMemoryTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddAsync_ValidProduct_PersistsToDatabase()
    {
        // Arrange
        var context = CreateContext(nameof(AddAsync_ValidProduct_PersistsToDatabase));
        var repo = new ProductRepository(context);
        var product = new Product
        {
            Name = "Widget",
            Price = 19.99m,
            StockQuantity = 100,
            Category = "Electronics"
        };

        // Act
        await repo.AddAsync(product);

        // Assert
        var saved = await context.Products.FirstOrDefaultAsync(p => p.Name == "Widget");
        saved.ShouldNotBeNull();
        saved.Price.ShouldBe(19.99m);
    }

    [Fact]
    public async Task GetByCategoryAsync_MultipleProducts_ReturnsFilteredAndSorted()
    {
        // Arrange
        var context = CreateContext(nameof(GetByCategoryAsync_MultipleProducts_ReturnsFilteredAndSorted));
        context.Products.AddRange(
            new Product { Name = "Banana", Price = 1.50m, Category = "Fruit" },
            new Product { Name = "Apple",  Price = 2.00m, Category = "Fruit" },
            new Product { Name = "Bread",  Price = 3.00m, Category = "Bakery" }
        );
        await context.SaveChangesAsync();

        var repo = new ProductRepository(context);

        // Act
        var result = await repo.GetByCategoryAsync("Fruit");

        // Assert
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Apple");  // sorted by name
        result[1].Name.ShouldBe("Banana");
    }
}
```

### 3.3 Обмеження InMemory провайдера

InMemory провайдер має значні обмеження, які роблять його непридатним для багатьох сценаріїв тестування:

| Обмеження | Вплив |
|---|---|
| Немає трансляції SQL | LINQ виконується в .NET, не транслюється в SQL. Запит, що компілюється в C#, може зазнати невдачі в продакшні. |
| Немає посилальної цілісності | Обмеження зовнішніх ключів не примусово виконуються. Можна вставити осиротілі записи. |
| Немає унікальних обмежень | `HasIndex().IsUnique()` ігнорується. Дублікати даних мовчки приймаються. |
| Немає транзакцій | `BeginTransaction()` — це no-op. Поведінку відкату транзакцій неможливо протестувати. |
| Немає підтримки сирого SQL | `FromSqlRaw()` / `ExecuteSqlRaw()` викидають винятки. |
| Немає специфічних для провайдера функцій | Немає JSON-колонок, просторових типів, повнотекстового пошуку тощо. |
| Чутливість до регістру | Порівняння рядків поводиться інакше, ніж у SQL Server чи PostgreSQL. |

```csharp
// This test PASSES with InMemory but FAILS against a real database
[Fact]
public async Task AddAsync_DuplicateName_ShouldViolateUniqueConstraint()
{
    var context = CreateContext("DuplicateTest");
    var repo = new ProductRepository(context);

    await repo.AddAsync(new Product { Name = "Widget", Price = 10m, Category = "A" });

    // InMemory does NOT enforce the unique index on Name!
    // This succeeds with InMemory but would throw DbUpdateException on a real DB
    await repo.AddAsync(new Product { Name = "Widget", Price = 20m, Category = "B" });

    var count = await context.Products.CountAsync(p => p.Name == "Widget");
    count.ShouldBe(2); // 2 duplicates exist — constraint not enforced!
}
```

### 3.4 Коли використовувати InMemory

| Випадок використання | Рекомендація |
|---|---|
| Швидке прототипування та навчання | Прийнятно |
| Тестування чистої LINQ-логіки (без специфіки SQL) | Прийнятно з обережністю |
| Тестування патернів репозиторіїв (базовий CRUD) | Прийнятно, якщо ви розумієте обмеження |
| Тестування коректності трансляції запитів | **Використовуйте реальну базу даних** |
| Тестування обмежень та цілісності даних | **Використовуйте реальну базу даних** |
| Тестування міграцій | **Використовуйте реальну базу даних** |
| Тестування транзакцій | **Використовуйте реальну базу даних** |

> **Примітка:** Команда EF Core явно не рекомендує використовувати InMemory для тестування. З [офіційної документації](https://learn.microsoft.com/en-us/ef/core/testing/): *"In-memory база даних часто поводиться інакше, ніж реляційні бази даних. Використовуйте in-memory базу даних лише якщо ви розумієте пов'язані з цим проблеми та компроміси."*

---

## 4. Підхід 2: SQLite In-Memory

### 4.1 Чому SQLite?

SQLite — це реальний реляційний двигун бази даних. При використанні в in-memory режимі він забезпечує:

- Реальне виконання SQL (не лише .NET LINQ обчислення)
- Примусове виконання зовнішніх ключів (коли увімкнено)
- Примусове виконання унікальних обмежень
- Підтримка транзакцій
- Швидкий запуск (контейнер не потрібен)

```bash
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
```

### 4.2 Налаштування

Ключовий трюк з SQLite in-memory: база даних існує лише поки з'єднання відкрите. Потрібно тримати з'єднання відкритим протягом усього тесту.

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ProductRepositorySqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProductRepository _repo;

    public ProductRepositorySqliteTests()
    {
        // Create and open a persistent in-memory connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated(); // Creates schema from model
        _repo = new ProductRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        await _repo.AddAsync(new Product
        {
            Name = "Widget", Price = 10m, Category = "A"
        });

        // Act & Assert — SQLite DOES enforce unique constraints!
        var duplicate = new Product
        {
            Name = "Widget", Price = 20m, Category = "B"
        };

        await Should.ThrowAsync<DbUpdateException>(
            () => _repo.AddAsync(duplicate));
    }

    [Fact]
    public async Task GetExpensiveProductsAsync_ReturnsFilteredAndSorted()
    {
        // Arrange
        _context.Products.AddRange(
            new Product { Name = "Cheap",  Price = 5m,    Category = "A" },
            new Product { Name = "Mid",    Price = 50m,   Category = "A" },
            new Product { Name = "Pricy",  Price = 200m,  Category = "B" },
            new Product { Name = "Luxury", Price = 500m,  Category = "B" }
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _repo.GetExpensiveProductsAsync(50m);

        // Assert
        result.Count.ShouldBe(3);
        result[0].Name.ShouldBe("Luxury"); // ordered by price desc
        result[1].Name.ShouldBe("Pricy");
        result[2].Name.ShouldBe("Mid");
    }
}
```

### 4.3 Обмеження SQLite

SQLite кращий за InMemory, але це все ще не ваша продакшн база даних:

| Обмеження | Вплив |
|---|---|
| Інший діалект SQL | `GETDATE()`, `NEWID()`, `TOP`, `IDENTITY` не існують у SQLite |
| Немає збережених процедур | Неможливо тестувати збережені процедури або функції БД |
| Обмежена система типів | Немає точності `decimal` — зберігається як `REAL` (з плаваючою комою) |
| Немає `ALTER COLUMN` | Міграції EF Core з використанням `AlterColumn` можуть зазнати невдачі |
| Немає конкурентних з'єднань | In-memory режим використовує одне з'єднання |
| Відсутні функції | Немає обчислюваних колонок, JSON-функцій (залежить від версії) |
| Інше зіставлення | Порівняння рядків та сортування можуть відрізнятися від продакшну |

```
Порівняння:   InMemory         SQLite           Реальна БД
              ────────         ──────           ───────
SQL?          Ні               Так (підмножина) Так (повний)
Обмеження?    Ні               Так              Так
Транзакції?   Ні               Так              Так
Міграції?     Ні               Частково         Так
Діалект SQL?  Н/Д              SQLite           Продакшн-діалект
Швидкість?    Найшвидший       Швидкий          Повільніший (запуск)
Точність?     Низька           Середня          Висока
```

> **Обговорення (5 хв):** З огляду на обмеження SQLite, які помилки все ще можуть пройти через тести, що проходять на SQLite, але зазнають невдачі на SQL Server чи PostgreSQL?

---

## 5. Підхід 3: Testcontainers (Реальні контейнери бази даних)

### 5.1 Що таке Testcontainers?

Testcontainers — це бібліотека, що надає легковагі, одноразові екземпляри баз даних (та інших сервісів), що працюють у Docker-контейнерах. Ваші тести запускають реальний SQL Server, PostgreSQL, MySQL або будь-який інший докеризований сервіс, виконують тести з ним, а потім зупиняють його.

```
┌──────────────────────────────────────────────────────────┐
│  Test Process                                            │
│                                                          │
│   Test Code                                              │
│      │                                                   │
│      ▼                                                   │
│   Testcontainers Library                                 │
│      │                                                   │
│      │  1. Pulls Docker image (if not cached)            │
│      │  2. Starts container with random port             │
│      │  3. Waits until container is healthy              │
│      │  4. Returns connection string                     │
│      ▼                                                   │
│   ┌───────────────────────────┐                          │
│   │  Docker Container         │                          │
│   │  ┌─────────────────────┐  │                          │
│   │  │  SQL Server /       │  │                          │
│   │  │  PostgreSQL /       │  │                          │
│   │  │  MySQL / ...        │  │  ◄── Real database!      │
│   │  └─────────────────────┘  │                          │
│   └───────────────────────────┘                          │
│      │                                                   │
│      │  5. Tests run against real DB                     │
│      │  6. Container is stopped and removed              │
│      ▼                                                   │
│   Test Results                                           │
└──────────────────────────────────────────────────────────┘
```

### 5.2 Чому Testcontainers?

| Перевага | Опис |
|---|---|
| **Паритет з продакшном** | Тести запускаються на тому самому двигуні БД, що й продакшн |
| **Немає спільного стану** | Кожен запуск тестів отримує свіжий контейнер — жодних застарілих даних |
| **Не потрібна інсталяція** | Розробникам не потрібно встановлювати SQL Server або PostgreSQL локально |
| **Відтворюваність** | Той самий образ контейнера = та сама поведінка на кожній машині та в CI |
| **Ізольованість** | Кожен тестовий клас (або тест) може мати власний контейнер |
| **Автоматичне очищення** | Контейнери видаляються після завершення тестів |

### 5.3 Передумови

Testcontainers вимагає встановленого та запущеного Docker:

```bash
# Verify Docker is available
docker --version
# Docker version 24.x.x or later

# Verify Docker daemon is running
docker info
```

### 5.4 Як Testcontainers працює під капотом

1. **Image Pull** — Downloads the Docker image if not already cached locally
2. **Container Start** — Creates and starts a container with a random available port
3. **Health Check** — Waits until the database inside the container is ready to accept connections
4. **Connection String** — Exposes the dynamically allocated host port so your tests can connect
5. **Test Execution** — Your code connects to the container as if it were any database server
6. **Cleanup** — After tests complete, the container is stopped and removed

```
Timeline:

  Pull Image    Start Container    Wait for Ready    Run Tests    Stop & Remove
  (cached?)     (random port)      (health check)                 (cleanup)
      │               │                  │               │              │
      ▼               ▼                  ▼               ▼              ▼
  ════════════════════════════════════════════════════════════════════════
  │  ~0s if cached  │   ~2-10s         │   ~1-5s       │  test time   │ ~1s
  │  ~30s first time│                  │               │              │
  ════════════════════════════════════════════════════════════════════════
```

---

## 6. Налаштування Testcontainers у C#

### 6.1 NuGet-пакети

```bash
# For SQL Server
dotnet add package Testcontainers.MsSql

# For PostgreSQL
dotnet add package Testcontainers.PostgreSql

# EF Core providers
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
# or
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# Test framework
dotnet add package xunit.v3
dotnet add package Shouldly
```

### 6.2 Налаштування контейнера SQL Server

```csharp
using Testcontainers.MsSql;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class SqlServerContainerTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        // Start the container (pulls image if needed, starts SQL Server)
        await _container.StartAsync();

        // Configure EF Core to use the container's connection string
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task AddProduct_ValidData_PersistsToSqlServer()
    {
        // Arrange
        var repo = new ProductRepository(_context);
        var product = new Product
        {
            Name = "Gadget",
            Price = 49.99m,
            StockQuantity = 50,
            Category = "Electronics"
        };

        // Act
        await repo.AddAsync(product);

        // Assert — query directly to verify
        var saved = await _context.Products.FirstAsync(p => p.Name == "Gadget");
        saved.Price.ShouldBe(49.99m);
        saved.StockQuantity.ShouldBe(50);
    }

    [Fact]
    public async Task AddProduct_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        var repo = new ProductRepository(_context);
        await repo.AddAsync(new Product
        {
            Name = "Unique-Item", Price = 10m, Category = "A"
        });

        // Act & Assert — unique constraint is enforced on real SQL Server
        var duplicate = new Product
        {
            Name = "Unique-Item", Price = 20m, Category = "B"
        };

        await Should.ThrowAsync<DbUpdateException>(
            () => repo.AddAsync(duplicate));
    }
}
```

### 6.3 Налаштування контейнера PostgreSQL

```csharp
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class PostgreSqlContainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _context = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetByCategoryAsync_PostgreSql_ReturnsSortedResults()
    {
        // Arrange
        _context.Products.AddRange(
            new Product { Name = "Cherry", Price = 3m, Category = "Fruit" },
            new Product { Name = "Apple",  Price = 2m, Category = "Fruit" },
            new Product { Name = "Bread",  Price = 4m, Category = "Bakery" }
        );
        await _context.SaveChangesAsync();

        var repo = new ProductRepository(_context);

        // Act
        var fruits = await repo.GetByCategoryAsync("Fruit");

        // Assert — this runs against real PostgreSQL!
        fruits.Count.ShouldBe(2);
        fruits[0].Name.ShouldBe("Apple");
        fruits[1].Name.ShouldBe("Cherry");
    }
}
```

### 6.4 Конфігурація Builder контейнера

Both `MsSqlBuilder` and `PostgreSqlBuilder` support additional configuration:

```csharp
// SQL Server with custom configuration
var mssqlContainer = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .WithPassword("YourStrong!Passw0rd")    // custom SA password
    .WithEnvironment("ACCEPT_EULA", "Y")     // required for SQL Server
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilPortIsAvailable(1433))
    .Build();

// PostgreSQL with custom configuration
var pgContainer = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithDatabase("testdb")                   // custom database name
    .WithUsername("testuser")                  // custom username
    .WithPassword("testpass")                  // custom password
    .Build();
```

---

## 7. Розуміння IAsyncLifetime

### 7.1 Чому IAsyncLifetime?

Контейнери потребують часу для запуску. xUnit v3 надає `IAsyncLifetime` для асинхронного налаштування та очищення, що є необхідним для управління життєвим циклом контейнерів:

```csharp
public interface IAsyncLifetime
{
    Task InitializeAsync();   // Called before each test class
    Task DisposeAsync();      // Called after all tests in the class complete
}
```

```
Container Lifecycle with IAsyncLifetime:

  ┌──────────────────────────────────────────────────┐
  │  InitializeAsync()                               │
  │  ├─ Start container                              │
  │  ├─ Wait for health check                        │
  │  ├─ Create DbContext                             │
  │  └─ Create/migrate schema                        │
  ├──────────────────────────────────────────────────┤
  │  Test 1 runs                                     │
  ├──────────────────────────────────────────────────┤
  │  (new class instance created)                    │
  │  InitializeAsync() again                         │
  ├──────────────────────────────────────────────────┤
  │  Test 2 runs                                     │
  ├──────────────────────────────────────────────────┤
  │  DisposeAsync()                                  │
  │  ├─ Dispose DbContext                            │
  │  └─ Stop and remove container                    │
  └──────────────────────────────────────────────────┘
```

> **Important:** In xUnit v3, each test method gets a new class instance. If `IAsyncLifetime` is on the test class, `InitializeAsync` and `DisposeAsync` run for **every test**. This means a new container per test unless you use a fixture (covered in Section 11).

### 7.2 Class Fixture для спільних контейнерів

Щоб поділити один контейнер між усіма тестами в класі, використовуйте `IClassFixture<T>`:

```csharp
// Fixture: starts container once, shared across all tests in the class
public class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}

// Tests: use the shared fixture
public class ProductRepositoryTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private AppDbContext _context = null!;
    private ProductRepository _repo = null!;

    public async Task InitializeAsync()
    {
        // Each test gets a fresh DbContext, but shares the container
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new AppDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // Clean data between tests
        _context.Products.RemoveRange(_context.Products);
        await _context.SaveChangesAsync();

        _repo = new ProductRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_ValidProduct_PersistsCorrectly()
    {
        // Arrange
        var product = new Product
        {
            Name = "Test Product",
            Price = 25.99m,
            StockQuantity = 10,
            Category = "Testing"
        };

        // Act
        await _repo.AddAsync(product);

        // Assert
        var saved = await _context.Products.FirstAsync();
        saved.Name.ShouldBe("Test Product");
        saved.Price.ShouldBe(25.99m);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Act
        var result = await _repo.GetByIdAsync(999);

        // Assert
        result.ShouldBeNull();
    }
}
```

---

## 8. Тестування патернів репозиторіїв з EF Core

### 8.1 Реалістичний репозиторій

Визначимо більш повний репозиторій для тестування:

```csharp
// Domain models
public class Order
{
    public int Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(i => i.Quantity * i.UnitPrice);
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum OrderStatus { Pending, Confirmed, Shipped, Delivered, Cancelled }
```

```csharp
// DbContext with relationships
public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.CustomerEmail).HasMaxLength(256).IsRequired();
            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(50);
            entity.HasIndex(o => o.CustomerEmail);
            entity.HasIndex(o => o.CreatedAt);
            entity.Ignore(o => o.TotalAmount); // Computed in .NET
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.UnitPrice).HasColumnType("decimal(18,2)");
            entity.HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

```csharp
// Repository with various query patterns
public class OrderRepository(OrderDbContext context)
{
    public async Task<Order?> GetByIdWithItemsAsync(int id)
        => await context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<List<Order>> GetByCustomerAsync(string email)
        => await context.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerEmail == email)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status)
        => await context.Orders
            .Where(o => o.Status == status)
            .ToListAsync();

    public async Task<int> CreateAsync(Order order)
    {
        context.Orders.Add(order);
        return await context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int orderId, OrderStatus newStatus)
    {
        var order = await context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        order.Status = newStatus;
        await context.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime from, DateTime to)
        => await context.Orders
            .Where(o => o.Status == OrderStatus.Delivered)
            .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
            .SelectMany(o => o.Items)
            .SumAsync(i => i.Quantity * i.UnitPrice);

    public async Task<Dictionary<OrderStatus, int>> GetOrderCountsByStatusAsync()
        => await context.Orders
            .GroupBy(o => o.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
}
```

### 8.2 Тестування репозиторію з Testcontainers

```csharp
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class OrderRepositoryFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync() => await Container.StartAsync();
    public async Task DisposeAsync() => await Container.DisposeAsync();
}

public class OrderRepositoryTests(OrderRepositoryFixture fixture)
    : IClassFixture<OrderRepositoryFixture>, IAsyncLifetime
{
    private OrderDbContext _context = null!;
    private OrderRepository _repo = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new OrderDbContext(options);

        // Drop and recreate for clean state
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();

        _repo = new OrderRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    // ── Helper Methods ──────────────────────────────────────

    private static Order CreateOrder(
        string email = "alice@example.com",
        OrderStatus status = OrderStatus.Pending,
        DateTime? createdAt = null,
        params (string Name, int Qty, decimal Price)[] items)
    {
        var order = new Order
        {
            CustomerEmail = email,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        foreach (var (name, qty, price) in items)
        {
            order.Items.Add(new OrderItem
            {
                ProductName = name,
                Quantity = qty,
                UnitPrice = price
            });
        }

        return order;
    }

    // ── CRUD Tests ──────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidOrder_PersistsWithItems()
    {
        // Arrange
        var order = CreateOrder(
            items: [("Widget", 2, 10.00m), ("Gadget", 1, 25.50m)]);

        // Act
        await _repo.CreateAsync(order);

        // Assert
        var saved = await _repo.GetByIdWithItemsAsync(order.Id);
        saved.ShouldNotBeNull();
        saved.CustomerEmail.ShouldBe("alice@example.com");
        saved.Items.Count.ShouldBe(2);
        saved.TotalAmount.ShouldBe(45.50m); // (2*10) + (1*25.50)
    }

    [Fact]
    public async Task GetByIdWithItemsAsync_NonExistent_ReturnsNull()
    {
        // Act
        var result = await _repo.GetByIdWithItemsAsync(99999);

        // Assert
        result.ShouldBeNull();
    }

    // ── Filtering Tests ─────────────────────────────────────

    [Fact]
    public async Task GetByCustomerAsync_MultipleOrders_ReturnsNewestFirst()
    {
        // Arrange
        var older = CreateOrder(
            email: "bob@example.com",
            createdAt: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            items: [("A", 1, 10m)]);
        var newer = CreateOrder(
            email: "bob@example.com",
            createdAt: new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            items: [("B", 1, 20m)]);
        var otherCustomer = CreateOrder(
            email: "carol@example.com",
            items: [("C", 1, 30m)]);

        await _repo.CreateAsync(older);
        await _repo.CreateAsync(newer);
        await _repo.CreateAsync(otherCustomer);

        // Act
        var bobOrders = await _repo.GetByCustomerAsync("bob@example.com");

        // Assert
        bobOrders.Count.ShouldBe(2);
        bobOrders[0].CreatedAt.ShouldBeGreaterThan(bobOrders[1].CreatedAt);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersByStatus()
    {
        // Arrange
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Pending, items: [("A", 1, 10m)]));
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Shipped, items: [("B", 1, 20m)]));
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Shipped, items: [("C", 1, 30m)]));

        // Act
        var shipped = await _repo.GetByStatusAsync(OrderStatus.Shipped);

        // Assert
        shipped.Count.ShouldBe(2);
        shipped.ShouldAllBe(o => o.Status == OrderStatus.Shipped);
    }

    // ── Update Tests ────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ValidOrder_ChangesStatus()
    {
        // Arrange
        var order = CreateOrder(
            status: OrderStatus.Pending, items: [("A", 1, 10m)]);
        await _repo.CreateAsync(order);

        // Act
        await _repo.UpdateStatusAsync(order.Id, OrderStatus.Confirmed);

        // Assert — use a fresh context to verify persistence
        var updated = await _repo.GetByIdWithItemsAsync(order.Id);
        updated.ShouldNotBeNull();
        updated.Status.ShouldBe(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistent_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(
            () => _repo.UpdateStatusAsync(99999, OrderStatus.Cancelled));
    }

    // ── Aggregation Tests ───────────────────────────────────

    [Fact]
    public async Task GetTotalRevenueAsync_DeliveredOrdersInRange_ReturnsSumAsync()
    {
        // Arrange
        var jan = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var mar = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc);
        var jun = new DateTime(2025, 6, 20, 0, 0, 0, DateTimeKind.Utc);

        // Delivered in January — in range
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Delivered, createdAt: jan,
            items: [("A", 2, 50m)]));  // 100

        // Delivered in March — in range
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Delivered, createdAt: mar,
            items: [("B", 1, 75m)]));  // 75

        // Delivered in June — out of range
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Delivered, createdAt: jun,
            items: [("C", 1, 200m)]));

        // Pending in January — not delivered
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Pending, createdAt: jan,
            items: [("D", 1, 300m)]));

        // Act
        var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var revenue = await _repo.GetTotalRevenueAsync(from, to);

        // Assert
        revenue.ShouldBe(175m);  // 100 + 75
    }

    [Fact]
    public async Task GetOrderCountsByStatusAsync_ReturnsCorrectCounts()
    {
        // Arrange
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Pending, items: [("A", 1, 10m)]));
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Pending, items: [("B", 1, 10m)]));
        await _repo.CreateAsync(CreateOrder(
            status: OrderStatus.Shipped, items: [("C", 1, 10m)]));

        // Act
        var counts = await _repo.GetOrderCountsByStatusAsync();

        // Assert
        counts[OrderStatus.Pending].ShouldBe(2);
        counts[OrderStatus.Shipped].ShouldBe(1);
        counts.ContainsKey(OrderStatus.Delivered).ShouldBeFalse();
    }
}
```

> **Discussion (5 min):** Looking at the test helper method `CreateOrder`, why is it useful to have factory methods for test data? What would happen if we duplicated the object creation in every test?

---

## 9. Тестування міграцій та змін схеми

### 9.1 Навіщо тестувати міграції?

Міграції EF Core — це код, що модифікує схему бази даних. Міграції можуть зазнати невдачі з багатьох причин:

- Синтаксичні помилки в SQL міграції
- Конфлікти з існуючими даними
- Відсутні залежності (зовнішні ключі, що посилаються на неіснуючі таблиці)
- Несумісності, специфічні для провайдера

Тестування міграцій з реальним двигуном бази даних виявляє ці проблеми до потрапляння в продакшн.

### 9.2 Тестування застосування міграцій

```csharp
public class MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task AllMigrations_ApplyCleanly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        await using var context = new OrderDbContext(options);

        // Act — apply all pending migrations
        await context.Database.MigrateAsync();

        // Assert — verify migration was applied by checking tables exist
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.ShouldBeTrue();

        // Verify we can perform basic operations
        context.Orders.Add(new Order
        {
            CustomerEmail = "test@test.com",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        });

        var saved = await context.SaveChangesAsync();
        saved.ShouldBe(1);
    }

    [Fact]
    public async Task Migration_CanRollForwardFromEmpty()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        await using var context = new OrderDbContext(options);

        // Act
        var pendingMigrations = await context.Database
            .GetPendingMigrationsAsync();

        await context.Database.MigrateAsync();

        var appliedMigrations = await context.Database
            .GetAppliedMigrationsAsync();

        // Assert
        appliedMigrations.Count().ShouldBeGreaterThan(0);
        (await context.Database.GetPendingMigrationsAsync())
            .ShouldBeEmpty();
    }
}
```

### 9.3 EnsureCreated vs. Migrate

| Method | Behavior | Use For |
|---|---|---|
| `EnsureCreated()` | Creates schema from current model. Does not use migrations. | Quick test setup when migration history does not matter |
| `Migrate()` | Applies pending migrations in order. Creates `__EFMigrationsHistory` table. | Testing the actual migration path |

```
EnsureCreated():
  Empty DB ──► Full schema (from current model snapshot)
  - Does NOT create __EFMigrationsHistory table
  - Cannot run subsequent Migrate() calls
  - Good for: disposable test databases

Migrate():
  Empty DB ──► Migration 1 ──► Migration 2 ──► ... ──► Current schema
  - Creates __EFMigrationsHistory table
  - Applies migrations in order
  - Good for: testing the migration path itself
```

---

## 10. Тестування транзакцій та конкурентності

### 10.1 Тестування транзакцій

Транзакції критичні для узгодженості даних. Їх тестування потребує реальної бази даних — InMemory провайдер не підтримує транзакції.

```csharp
// Service that uses transactions
public class OrderService(OrderDbContext context)
{
    public async Task TransferItemAsync(
        int sourceOrderId, int targetOrderId, int itemId)
    {
        await using var transaction = await context.Database
            .BeginTransactionAsync();

        try
        {
            var item = await context.OrderItems
                .FirstOrDefaultAsync(i => i.Id == itemId && i.OrderId == sourceOrderId)
                ?? throw new KeyNotFoundException("Item not found in source order");

            // Remove from source
            item.OrderId = targetOrderId;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

```csharp
public class TransactionTests(OrderRepositoryFixture fixture)
    : IClassFixture<OrderRepositoryFixture>, IAsyncLifetime
{
    private OrderDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new OrderDbContext(options);
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task TransferItemAsync_ValidTransfer_MovesItemBetweenOrders()
    {
        // Arrange
        var source = new Order
        {
            CustomerEmail = "a@b.com",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = [new OrderItem
            {
                ProductName = "Widget",
                Quantity = 1,
                UnitPrice = 10m
            }]
        };
        var target = new Order
        {
            CustomerEmail = "c@d.com",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };
        _context.Orders.AddRange(source, target);
        await _context.SaveChangesAsync();

        var itemId = source.Items[0].Id;
        var service = new OrderService(_context);

        // Act
        await service.TransferItemAsync(source.Id, target.Id, itemId);

        // Assert — reload from DB
        var reloadedSource = await _context.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == source.Id);
        var reloadedTarget = await _context.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == target.Id);

        reloadedSource.Items.ShouldBeEmpty();
        reloadedTarget.Items.Count.ShouldBe(1);
        reloadedTarget.Items[0].ProductName.ShouldBe("Widget");
    }

    [Fact]
    public async Task TransferItemAsync_InvalidItem_RollsBack()
    {
        // Arrange
        var source = new Order
        {
            CustomerEmail = "a@b.com",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Items = [new OrderItem
            {
                ProductName = "Widget",
                Quantity = 1,
                UnitPrice = 10m
            }]
        };
        _context.Orders.Add(source);
        await _context.SaveChangesAsync();

        var service = new OrderService(_context);

        // Act & Assert — transferring a non-existent item should fail
        await Should.ThrowAsync<KeyNotFoundException>(
            () => service.TransferItemAsync(source.Id, 999, itemId: 99999));

        // Verify source order still has its item (transaction rolled back)
        var reloaded = await _context.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == source.Id);
        reloaded.Items.Count.ShouldBe(1);
    }
}
```

### 10.2 Тестування конкурентності (оптимістичний контроль конкурентності)

EF Core підтримує оптимістичну конкурентність за допомогою токенів конкурентності або версій рядків:

```csharp
// Entity with concurrency token
public class InventoryItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int StockLevel { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;
}
```

```csharp
[Fact]
public async Task ConcurrentUpdate_SameRow_ThrowsDbUpdateConcurrencyException()
{
    // Arrange — seed an inventory item
    var item = new InventoryItem
    {
        ProductName = "Widget",
        StockLevel = 100
    };
    _context.Set<InventoryItem>().Add(item);
    await _context.SaveChangesAsync();

    // Simulate two users loading the same item
    var options = new DbContextOptionsBuilder<OrderDbContext>()
        .UseNpgsql(fixture.ConnectionString)
        .Options;

    await using var context1 = new OrderDbContext(options);
    await using var context2 = new OrderDbContext(options);

    var item1 = await context1.Set<InventoryItem>().FindAsync(item.Id);
    var item2 = await context2.Set<InventoryItem>().FindAsync(item.Id);

    // User 1 updates stock
    item1!.StockLevel = 90;
    await context1.SaveChangesAsync();

    // User 2 tries to update the same item — should fail
    item2!.StockLevel = 80;

    await Should.ThrowAsync<DbUpdateConcurrencyException>(
        () => context2.SaveChangesAsync());
}
```

> **Discussion (10 min):** Why is optimistic concurrency important? What alternatives exist (pessimistic locking)? When would you choose one over the other?

---

## 11. Стратегії ізоляції тестів

### 11.1 Огляд стратегій

Коли кілька тестів поділяють базу даних, потрібна стратегія для запобігання взаємному впливу тестів:

```
Strategy 1:                 Strategy 2:                Strategy 3:
PER-TEST DATABASE           TRANSACTION ROLLBACK       TABLE CLEANUP
─────────────────           ────────────────────       ─────────────

Each test creates           Each test wraps its        Each test deletes
and drops a new             work in a transaction      data in setup or
database.                   that is rolled back.       teardown.

Pros:                       Pros:                      Pros:
+ Perfect isolation         + Fast (no DB create)      + Simple to implement
+ No data leaks             + Automatic cleanup        + Works with any DB

Cons:                       Cons:                      Cons:
- Slowest option            - Cannot test commits      - May miss orphaned data
- Resource intensive        - Nested tx complexity     - Fragile ordering
                            - Some ops force commit    - Slower for large datasets
```

### 11.2 Стратегія 1: Окрема БД на тест (EnsureDeleted + EnsureCreated)

```csharp
public class IsolatedDatabaseTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private OrderDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new OrderDbContext(options);
        // Drop everything and recreate — clean slate for each test
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Test1_SeesEmptyDatabase()
    {
        var count = await _context.Orders.CountAsync();
        count.ShouldBe(0);
    }

    [Fact]
    public async Task Test2_AlsoSeesEmptyDatabase()
    {
        // Even if Test1 added data, this test starts fresh
        var count = await _context.Orders.CountAsync();
        count.ShouldBe(0);
    }
}
```

### 11.3 Стратегія 2: Відкат транзакцій

```csharp
public class TransactionRollbackTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private OrderDbContext _context = null!;
    private IDbContextTransaction _transaction = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new OrderDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // Begin a transaction that will be rolled back after each test
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        // Roll back — all changes from this test are undone
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AddOrder_WithinTransaction_VisibleDuringTest()
    {
        // Arrange & Act
        _context.Orders.Add(new Order
        {
            CustomerEmail = "test@test.com",
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        });
        await _context.SaveChangesAsync();

        // Assert — data is visible within the transaction
        var count = await _context.Orders.CountAsync();
        count.ShouldBe(1);

        // After DisposeAsync, this data will be rolled back
    }
}
```

### 11.4 Стратегія 3: Очищення таблиць

```csharp
public class TableCleanupTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private OrderDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        _context = new OrderDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        // Clean all tables before each test
        await CleanTablesAsync();
    }

    private async Task CleanTablesAsync()
    {
        // Delete in correct order to respect foreign keys
        _context.OrderItems.RemoveRange(_context.OrderItems);
        _context.Orders.RemoveRange(_context.Orders);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Test_StartsWithCleanTables()
    {
        var orderCount = await _context.Orders.CountAsync();
        var itemCount = await _context.OrderItems.CountAsync();

        orderCount.ShouldBe(0);
        itemCount.ShouldBe(0);
    }
}
```

### 11.5 Підсумок порівняння

| Strategy | Isolation | Speed | Can Test Commits? | Complexity |
|---|---|---|---|---|
| Per-test database (EnsureDeleted) | Perfect | Slowest | Yes | Low |
| Transaction rollback | Good | Fastest | No | Medium |
| Table cleanup | Good | Medium | Yes | Medium |
| Unique database name per test | Perfect | Slow | Yes | Low |

---

## 12. Стратегії заповнення даними

### 12.1 Чому заповнення даними має значення

Тести потребують даних. Те, як ви створюєте ці дані, впливає на читабельність, підтримуваність та надійність:

```
Bad: Inline data creation (verbose, duplicated)
──────────────────────────────────────────────
var order1 = new Order { CustomerEmail = "a@b.com", CreatedAt = DateTime.UtcNow,
    Status = OrderStatus.Pending, Items = new List<OrderItem> {
        new OrderItem { ProductName = "A", Quantity = 1, UnitPrice = 10m }
    }};
var order2 = new Order { CustomerEmail = "c@d.com", ...  // more duplication

Good: Factory methods or builders
─────────────────────────────────
var order1 = OrderFactory.CreatePending(items: [("Widget", 2, 10m)]);
var order2 = OrderFactory.CreateDelivered(email: "vip@co.com");
```

### 12.2 Фабричні методи

```csharp
public static class TestDataFactory
{
    private static int _counter;

    public static Order CreateOrder(
        string? email = null,
        OrderStatus status = OrderStatus.Pending,
        DateTime? createdAt = null,
        params (string Name, int Qty, decimal Price)[] items)
    {
        var order = new Order
        {
            CustomerEmail = email ?? $"user{Interlocked.Increment(ref _counter)}@test.com",
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };

        foreach (var (name, qty, price) in items)
        {
            order.Items.Add(new OrderItem
            {
                ProductName = name,
                Quantity = qty,
                UnitPrice = price
            });
        }

        return order;
    }

    public static async Task SeedOrdersAsync(
        OrderDbContext context, int count, OrderStatus status = OrderStatus.Pending)
    {
        for (int i = 0; i < count; i++)
        {
            context.Orders.Add(CreateOrder(
                status: status,
                items: [($"Product-{i}", 1, 10m + i)]));
        }
        await context.SaveChangesAsync();
    }
}
```

### 12.3 Патерн Builder для складних тестових даних

```csharp
public class OrderBuilder
{
    private string _email = "default@test.com";
    private OrderStatus _status = OrderStatus.Pending;
    private DateTime _createdAt = DateTime.UtcNow;
    private readonly List<OrderItem> _items = [];

    public OrderBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public OrderBuilder WithStatus(OrderStatus status)
    {
        _status = status;
        return this;
    }

    public OrderBuilder CreatedOn(DateTime date)
    {
        _createdAt = date;
        return this;
    }

    public OrderBuilder WithItem(string name, int qty, decimal price)
    {
        _items.Add(new OrderItem
        {
            ProductName = name,
            Quantity = qty,
            UnitPrice = price
        });
        return this;
    }

    public Order Build() => new()
    {
        CustomerEmail = _email,
        Status = _status,
        CreatedAt = _createdAt,
        Items = [.. _items]
    };
}

// Usage in tests:
var order = new OrderBuilder()
    .WithEmail("vip@company.com")
    .WithStatus(OrderStatus.Delivered)
    .CreatedOn(new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc))
    .WithItem("Premium Widget", 5, 99.99m)
    .WithItem("Extended Warranty", 1, 29.99m)
    .Build();
```

### 12.4 Заповнення за допомогою SQL-скриптів

For complex scenarios, you may seed data using raw SQL:

```csharp
private async Task SeedFromSqlAsync(OrderDbContext context)
{
    await context.Database.ExecuteSqlRawAsync("""
        INSERT INTO "Orders" ("CustomerEmail", "CreatedAt", "Status")
        VALUES
            ('alice@example.com', '2025-01-15', 'Delivered'),
            ('bob@example.com',   '2025-02-20', 'Shipped'),
            ('carol@example.com', '2025-03-10', 'Pending');
    """);
}
```

> **Discussion (5 min):** When would you prefer SQL scripts over C# factory methods for test data? What are the tradeoffs in terms of maintainability and type safety?

---

## 13. Питання продуктивності

### 13.1 Час запуску контейнера

Найбільша вартість продуктивності — запуск контейнера. Ось типовий розподіл:

```
Operation                        Time
─────────────────────────────    ────────
Docker image pull (first time)   10-60s   (cached after first pull)
Container start                  2-5s
Database ready (health check)    3-10s
Schema creation (EnsureCreated)  0.5-2s
Running a single test            0.01-0.5s
Container stop + remove          0.5-1s

Total for first test class:      ~6-18s
Total for subsequent tests:      ~0.01-0.5s each (if container is shared)
```

### 13.2 Стратегії оптимізації

#### Стратегія 1: Спільні контейнери з Class Fixtures

Як показано в Розділі 7.2, використовуйте `IClassFixture<T>` для запуску одного контейнера на тестовий клас замість кожного тесту.

#### Стратегія 2: Спільні контейнери між тестовими класами з Collection Fixtures

```csharp
// Define a collection fixture — one container for ALL test classes in the collection
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>;

// Any test class in this collection shares the same container
[Collection("Database")]
public class OrderRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SomeTestAsync()
    {
        // Uses the shared container from PostgreSqlFixture
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        // ...
    }
}

[Collection("Database")]
public class ProductRepositoryTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task AnotherTestAsync()
    {
        // Same container, different test class
        // ...
    }
}
```

#### Стратегія 3: Повторне використання контейнерів (функція Testcontainers Reuse)

Testcontainers підтримує збереження контейнерів між запусками тестів під час розробки:

```csharp
var container = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .WithReuse(true)   // Keep container running between test executions
    .Build();
```

> **Note:** Container reuse is primarily a development-time optimization. In CI/CD pipelines, containers should be created fresh for each run to ensure isolation.

#### Стратегія 4: Паралельне виконання тестів

xUnit v3 запускає колекції тестів паралельно за замовчуванням. При використанні collection fixtures кожна колекція отримує свій контейнер, і колекції виконуються паралельно:

```
Collection "Orders"  ──► PostgreSQL Container A ──► Order tests (sequential)
Collection "Products"──► PostgreSQL Container B ──► Product tests (sequential)
                              ▲
                              │ Both collections run in parallel
```

### 13.3 Порівняння продуктивності

| Approach | Cold Start | Per-Test Cost | Fidelity |
|---|---|---|---|
| EF Core InMemory | ~0s | ~1ms | Low |
| SQLite In-Memory | ~0.1s | ~5ms | Medium |
| Testcontainers (per test) | ~10s | ~10s | High |
| Testcontainers (shared fixture) | ~10s | ~50ms | High |
| Testcontainers (collection fixture) | ~10s | ~50ms | High |
| Testcontainers (reuse) | ~0s (warm) | ~50ms | High |

> **Discussion (5 min):** For a project with 200 database tests, which sharing strategy would you choose and why? What are the tradeoffs between test isolation and execution speed?

---

## 14. Збираємо все разом: Повний набір тестів

### 14.1 Рекомендована структура проєкту

```
src/
  OrderManagement/
    Data/
      OrderDbContext.cs
    Models/
      Order.cs
      OrderItem.cs
    Repositories/
      OrderRepository.cs
    Services/
      OrderService.cs

tests/
  OrderManagement.Tests/
    Fixtures/
      PostgreSqlFixture.cs
    Helpers/
      TestDataFactory.cs
      OrderBuilder.cs
    Repositories/
      OrderRepositoryTests.cs
    Services/
      OrderServiceTests.cs
    Migrations/
      MigrationTests.cs
    OrderManagement.Tests.csproj
```

### 14.2 Фікстура з допоміжними методами

```csharp
// Fixtures/PostgreSqlFixture.cs
using Testcontainers.PostgreSql;
using Microsoft.EntityFrameworkCore;

public class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }

    public DbContextOptions<OrderDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }

    public async Task<OrderDbContext> CreateFreshContextAsync()
    {
        var context = new OrderDbContext(CreateDbContextOptions());
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
```

### 14.3 Визначення колекції

```csharp
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgreSqlFixture>;
```

### 14.4 Шаблон тестового класу

```csharp
[Collection("Database")]
public class OrderServiceIntegrationTests(PostgreSqlFixture fixture)
    : IAsyncLifetime
{
    private OrderDbContext _context = null!;
    private OrderService _service = null!;

    public async Task InitializeAsync()
    {
        _context = await fixture.CreateFreshContextAsync();
        _service = new OrderService(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CompleteWorkflow_CreateAndUpdateOrder()
    {
        // Arrange
        var order = new OrderBuilder()
            .WithEmail("integration@test.com")
            .WithItem("Widget", 3, 15.00m)
            .Build();

        // Act — create
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Act — update status
        await _service.UpdateStatusAsync(order.Id, OrderStatus.Confirmed);

        // Assert
        var loaded = await _context.Orders
            .Include(o => o.Items)
            .FirstAsync(o => o.Id == order.Id);

        loaded.Status.ShouldBe(OrderStatus.Confirmed);
        loaded.Items.Count.ShouldBe(1);
        loaded.TotalAmount.ShouldBe(45.00m);
    }
}
```

---

## 15. Поширені помилки та найкращі практики

### 15.1 Помилки, яких слід уникати

| Pitfall | Problem | Solution |
|---|---|---|
| Sharing DbContext across tests | State leaks between tests | Create a new DbContext per test |
| Forgetting `await` on async DB calls | Tests pass without executing DB operations | Always await and verify results |
| Not disposing containers | Docker containers accumulate on disk | Always implement `IAsyncLifetime` or `IDisposable` |
| Hardcoding connection strings | Tests fail on different machines | Use Testcontainers' dynamic connection strings |
| Testing against a shared development DB | Tests interfere with each other and with developers | Each test environment should be isolated |
| Not testing with production DB engine | Behavior differences between SQLite and production | Use Testcontainers for critical paths |
| Ignoring test execution time | Test suite becomes too slow to run frequently | Use shared fixtures and container reuse |

### 15.2 Підсумок найкращих практик

```
1. Match your test DB to production
   Use the same database engine (SQL Server, PostgreSQL) in tests.

2. Isolate tests
   Each test should start with known state and not affect other tests.

3. Use factory methods / builders
   Avoid duplicating test data creation across tests.

4. Share containers wisely
   One container per test class (IClassFixture) or per collection
   (ICollectionFixture) — not per test.

5. Test the data layer specifically
   Constraints, indexes, queries, transactions, migrations.

6. Keep tests focused
   Each test should verify one behavior, even in integration tests.

7. Use async throughout
   Database operations are I/O-bound — use async/await consistently.

8. Clean up resources
   Dispose DbContexts, connections, and containers.
```

---

## 16. Підсумок

### Ключові висновки

1. **Database testing catches bugs that unit tests cannot** — query translation errors, constraint violations, migration failures, and concurrency issues only surface against a real database
2. **EF Core InMemory is fast but low fidelity** — it does not enforce constraints, does not support transactions, and does not translate LINQ to SQL. Use it only for quick prototyping
3. **SQLite in-memory offers better fidelity** — real SQL execution, constraint enforcement, and transaction support, but with a different SQL dialect than your production database
4. **Testcontainers provide production parity** — spin up real SQL Server or PostgreSQL in Docker containers for your tests. The startup cost is acceptable when containers are shared
5. **Test isolation is essential** — use per-test database reset, transaction rollback, or table cleanup to prevent tests from affecting each other
6. **`IAsyncLifetime` manages container lifecycles** — use it with `IClassFixture` or `ICollectionFixture` to control when containers start and stop
7. **Data factories and builders make tests readable** — avoid duplicating object creation code across tests
8. **Performance is manageable** — share containers with fixtures, use container reuse during development, and run test collections in parallel

### Матриця рішень: Вибір підходу

```
Question                                    Recommendation
──────────────────────────────────────────  ─────────────────────
Do I need to test LINQ-only logic?          InMemory or SQLite
Do I need to test constraints?              SQLite or Testcontainers
Do I need to test migrations?               Testcontainers
Do I need to test transactions?             SQLite or Testcontainers
Do I need production SQL dialect fidelity?  Testcontainers
Do I need maximum speed?                    InMemory
Do I need CI/CD compatibility?              Testcontainers (with Docker)
```

### Анонс наступної лекції

У **Лекції 5: Тестування продуктивності з k6** ми:
- Зрозуміємо, чому тестування продуктивності важливе та коли його проводити
- Вивчимо типи тестів продуктивності: навантажувальні, стресові, пікові, тривалі
- Напишемо скрипти тестів продуктивності з k6
- Визначимо порогові значення продуктивності та SLA
- Проаналізуємо результати тестів продуктивності та визначимо вузькі місця
- Інтегруємо тести продуктивності в конвеєри CI/CD

---

## Посилання та додаткова література

- **Testcontainers for .NET Documentation** — https://dotnet.testcontainers.org/
- **EF Core Testing Documentation** — https://learn.microsoft.com/en-us/ef/core/testing/
- **EF Core — Testing Without Your Production Database System** — https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database
- **EF Core — Testing Against Your Production Database System** — https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database
- **xUnit v3 Documentation** — https://xunit.net/docs/getting-started/v3/cmdline
- **Shouldly Documentation** — https://docs.shouldly.org/
- **Docker Documentation** — https://docs.docker.com/get-started/
- **"Unit Testing Principles, Practices, and Patterns"** — Vladimir Khorikov (Manning, 2020) — Chapter 8: Integration Testing
- **PostgreSQL Docker Image** — https://hub.docker.com/_/postgres
- **SQL Server Docker Image** — https://hub.docker.com/r/microsoft/mssql-server
