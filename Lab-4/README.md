# Лабораторна 4 — Тестування бази даних: Запити та міграції

> **Lab → 2 points**

## Мета

Навчитися тестувати взаємодію з базою даних за допомогою Entity Framework Core. Писати тести для репозиторіїв, запитів та міграцій з використанням тестових провайдерів InMemory та SQLite.

**Тривалість:** 60 хвилин

## Передумови

Перед початком цієї лабораторної переконайтеся, що:

- Встановлений .NET 10+ SDK (`dotnet --version`)
- Встановлений та запущений Docker (необхідний для Завдання 3 з Testcontainers)
- Ви маєте робоче розуміння Entity Framework Core (DbContext, DbSet, міграції, LINQ-запити)
- Ви знайомі з концепціями реляційних баз даних (зовнішні ключі, унікальні обмеження, каскадне видалення)
- Виконані Лабораторна 2 та Лабораторна 3 (основи інтеграційного тестування)

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
dotnet new sln -n Lab4
dotnet new classlib -n Lab4.Data
dotnet new classlib -n Lab4.Tests
dotnet sln add Lab4.Data Lab4.Tests
dotnet add Lab4.Data package Microsoft.EntityFrameworkCore
dotnet add Lab4.Data package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab4.Tests reference Lab4.Data
dotnet add Lab4.Tests package xunit.v3
dotnet add Lab4.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab4.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet add Lab4.Tests package Microsoft.EntityFrameworkCore.Sqlite
dotnet add Lab4.Tests package Testcontainers.MsSql
dotnet add Lab4.Tests package Microsoft.EntityFrameworkCore.SqlServer
dotnet add Lab4.Tests package Shouldly
```

## Завдання

### Завдання 1 — Тести DbContext та репозиторію з провайдером InMemory

Створіть `AppDbContext` з наступними сутностями:

```csharp
public class Student
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public DateTime EnrollmentDate { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; }
}

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; }
    public int Credits { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; }
}

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public decimal? Grade { get; set; }
    public Student Student { get; set; }
    public Course Course { get; set; }
}
```

Створіть `StudentRepository` з методами:

- `GetByIdAsync(int id)` — включає реєстрації
- `GetAllAsync()` — повертає всіх студентів
- `AddAsync(Student student)`
- `UpdateAsync(Student student)`
- `DeleteAsync(int id)`
- `GetTopStudentsAsync(int count)` — студенти з найвищим середнім балом

Напишіть тести з використанням `InMemoryDatabase`:

1. Протестуйте всі CRUD-операції
2. Протестуйте, що навігаційні властивості завантажуються правильно
3. Протестуйте, що `GetTopStudentsAsync` повертає правильне впорядкування
4. Кожен тест використовує унікальну назву бази даних для ізоляції

**Мінімальна кількість тестів: 6 тестів**

#### Приклад: Налаштування провайдера InMemory

```csharp
private AppDbContext CreateInMemoryContext()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    var context = new AppDbContext(options);
    return context;
}
```

#### Приклад: Тести репозиторію з Shouldly

```csharp
[Fact]
public async Task AddAsync_ValidStudent_SavesSuccessfullyAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var repo = new StudentRepository(context);
    var student = new Student
    {
        FullName = "John Doe",
        Email = "john@example.com",
        EnrollmentDate = DateTime.UtcNow
    };

    // Act
    await repo.AddAsync(student);

    // Assert
    var saved = await context.Students.FirstOrDefaultAsync(s => s.Email == "john@example.com");
    saved.ShouldNotBeNull();
    saved.FullName.ShouldBe("John Doe");
    saved.Id.ShouldBeGreaterThan(0);
}

[Fact]
public async Task GetByIdAsync_IncludesEnrollmentsAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var course = new Course { Title = "Testing 101", Credits = 3 };
    var student = new Student
    {
        FullName = "Jane Smith",
        Email = "jane@example.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment>
        {
            new Enrollment { Course = course, Grade = 95 }
        }
    };
    context.Students.Add(student);
    await context.SaveChangesAsync();

    var repo = new StudentRepository(context);

    // Act
    var result = await repo.GetByIdAsync(student.Id);

    // Assert
    result.ShouldNotBeNull();
    result.Enrollments.ShouldNotBeNull();
    result.Enrollments.Count.ShouldBe(1);
    result.Enrollments.First().Grade.ShouldBe(95m);
}

[Fact]
public async Task GetTopStudentsAsync_ReturnsOrderedByAverageGradeAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var course1 = new Course { Title = "Math", Credits = 4 };
    var course2 = new Course { Title = "Science", Credits = 3 };

    var studentA = new Student
    {
        FullName = "Alice", Email = "alice@test.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment>
        {
            new Enrollment { Course = course1, Grade = 70 },
            new Enrollment { Course = course2, Grade = 80 }
        }
    };
    var studentB = new Student
    {
        FullName = "Bob", Email = "bob@test.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment>
        {
            new Enrollment { Course = course1, Grade = 90 },
            new Enrollment { Course = course2, Grade = 95 }
        }
    };

    context.Students.AddRange(studentA, studentB);
    await context.SaveChangesAsync();

    var repo = new StudentRepository(context);

    // Act
    var top = await repo.GetTopStudentsAsync(1);

    // Assert
    top.Count.ShouldBe(1);
    top.First().FullName.ShouldBe("Bob"); // avg 92.5 > avg 75
}
```

> **Підказка:** Використовуйте `Guid.NewGuid().ToString()` як назву бази даних InMemory для гарантії ізоляції. Пам'ятайте, що InMemory не підтримує `Include()` так само, як реляційний провайдер — навігаційні властивості завантажуються, якщо вони відстежувалися в тому ж контексті, але `Include()` не завершується помилкою.

### Завдання 2 — Провайдер SQLite для реляційних тестів

Деякі поведінки (зовнішні ключі, обмеження) не забезпечуються провайдером InMemory. Перепишіть ключові тести з використанням SQLite у режимі пам'яті:

```csharp
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
options.UseSqlite(connection);
```

Напишіть тести, що:

1. Перевіряють обмеження зовнішнього ключа — реєстрація студента на неіснуючий курс завершується помилкою
2. Перевіряють унікальне обмеження на `Student.Email`
3. Тестують каскадне видалення — видалення студента видаляє його реєстрації
4. Тестують обробку конкурентних оновлень (оптимістична конкурентність)
5. Порівнюють відмінності поведінки між провайдерами InMemory та SQLite (задокументуйте в коментарях)

**Мінімальна кількість тестів: 5 тестів**

#### Приклад: Налаштування провайдера SQLite

```csharp
private (AppDbContext context, SqliteConnection connection) CreateSqliteContext()
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    var context = new AppDbContext(options);
    context.Database.EnsureCreated(); // applies the schema
    return (context, connection);
}
```

#### Приклад: Тести обмежень з Shouldly

```csharp
[Fact]
public async Task ForeignKey_EnrollingInNonExistingCourse_ThrowsAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var enrollment = new Enrollment
        {
            StudentId = 999, // does not exist
            CourseId = 999,  // does not exist
            Grade = 85
        };

        // Act & Assert
        context.Enrollments.Add(enrollment);
        var exception = await Should.ThrowAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        exception.ShouldNotBeNull();
    }
}

[Fact]
public async Task UniqueConstraint_DuplicateEmail_ThrowsAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var student1 = new Student
        {
            FullName = "Alice", Email = "dup@test.com",
            EnrollmentDate = DateTime.UtcNow
        };
        var student2 = new Student
        {
            FullName = "Bob", Email = "dup@test.com", // same email
            EnrollmentDate = DateTime.UtcNow
        };

        context.Students.Add(student1);
        await context.SaveChangesAsync();
        context.Students.Add(student2);

        // Act & Assert
        await Should.ThrowAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
    }
}

[Fact]
public async Task CascadeDelete_DeletingStudent_RemovesEnrollmentsAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        var course = new Course { Title = "CS101", Credits = 3 };
        var student = new Student
        {
            FullName = "Charlie", Email = "charlie@test.com",
            EnrollmentDate = DateTime.UtcNow,
            Enrollments = new List<Enrollment>
            {
                new Enrollment { Course = course, Grade = 88 }
            }
        };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        // Act
        context.Students.Remove(student);
        await context.SaveChangesAsync();

        // Assert
        var enrollments = await context.Enrollments.ToListAsync();
        enrollments.ShouldBeEmpty();
    }
}
```

#### Таблиця порівняння провайдерів

| Поведінка | InMemory | SQLite | SQL Server |
|---|---|---|---|
| Забезпечення зовнішніх ключів | Ні | Так | Так |
| Унікальні обмеження | Ні | Так | Так |
| Каскадне видалення | Вручну | Так | Так |
| Транзакції | Ні (без ефекту) | Так | Так |
| Сирий SQL / збережені процедури | Ні | Обмежено | Повністю |
| Поведінка автоінкременту | Послідовна | Послідовна | Identity |
| Чутливість `LIKE` до регістру | За замовчуванням C# | Без урахування регістру | Залежить від порівняння |

> **Підказка:** Не забудьте налаштувати каскадне видалення в `OnModelCreating` за допомогою `.OnDelete(DeleteBehavior.Cascade)`. SQLite вимагає `PRAGMA foreign_keys = ON`, що EF Core вмикає за замовчуванням для своїх з'єднань.

> **Примітка:** Тести з Testcontainers та реальною СУБД винесено в **Лабораторну 5**.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести репозиторію з InMemory |
| Завдання 2 — Реляційні тести з SQLite |

## Здача роботи

- Рішення з проєктами `Lab4.Data` та `Lab4.Tests`
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
