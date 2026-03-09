# Lab 5 — Database Testing: Queries and Migrations

## Objective

Learn to test database interactions using Entity Framework Core. Write tests for repositories, queries, and migrations using in-memory and SQLite test providers.

## Prerequisites

Before starting this lab, make sure you have:

- .NET 10+ SDK installed (`dotnet --version`)
- Docker installed and running (required for Task 3 with Testcontainers)
- A working understanding of Entity Framework Core (DbContext, DbSet, migrations, LINQ queries)
- Familiarity with relational database concepts (foreign keys, unique constraints, cascade delete)
- Completed Lab 3 and Lab 4 (integration testing fundamentals)

## Key Concepts

### EF Core InMemory Provider

The InMemory provider (`Microsoft.EntityFrameworkCore.InMemory`) is the fastest option for testing basic CRUD and LINQ queries. However, it does **not** enforce relational constraints (foreign keys, unique indexes, cascade deletes). Use it for logic-level tests only.

### SQLite In-Memory Mode

SQLite in-memory mode (`DataSource=:memory:`) provides a real relational database engine that enforces foreign keys, unique constraints, and cascades. The database lives only as long as the connection is open, making cleanup automatic. This is ideal for constraint-level tests.

### Testcontainers

Testcontainers spins up a real database server (SQL Server, PostgreSQL, etc.) inside a Docker container for each test class. This gives you full database behavior including stored procedures, raw SQL, and provider-specific query semantics. Tests using Testcontainers are slower but provide the highest fidelity.

### Database Name Isolation

When using the InMemory provider, each test should use a unique database name (e.g., via `Guid.NewGuid().ToString()`) to prevent shared state across tests. With SQLite, create a fresh connection per test. With Testcontainers, use `IAsyncLifetime` to start/stop the container.

## Tools

- Language: C#
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- Test DB: `Microsoft.EntityFrameworkCore.InMemory` / `Microsoft.EntityFrameworkCore.Sqlite`
- Containers: [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Setup

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

## Tasks

### Task 1 — DbContext and Repository Tests with InMemory Provider

Create an `AppDbContext` with the following entities:

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

Create a `StudentRepository` with methods:

- `GetByIdAsync(int id)` — includes enrollments
- `GetAllAsync()` — returns all students
- `AddAsync(Student student)`
- `UpdateAsync(Student student)`
- `DeleteAsync(int id)`
- `GetTopStudentsAsync(int count)` — students with highest average grade

Write tests using `InMemoryDatabase`:

1. Test all CRUD operations
2. Test navigation properties are loaded correctly
3. Test `GetTopStudentsAsync` returns correct ordering
4. Each test uses a unique database name for isolation

**Minimum test count: 8 tests**

#### Example: InMemory Provider Setup

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

#### Example: Repository Tests with Shouldly

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

> **Hint:** Use `Guid.NewGuid().ToString()` as the InMemory database name to guarantee isolation. Remember that InMemory does not support `Include()` the same way a relational provider does -- navigation properties are loaded if they were tracked in the same context, but `Include()` does not fail.

### Task 2 — SQLite Provider for Relational Tests

Some behaviors (foreign keys, constraints) are not enforced by InMemory provider. Rewrite key tests using SQLite in-memory mode:

```csharp
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
options.UseSqlite(connection);
```

Write tests that:

1. Verify foreign key constraint — enrolling a student in a non-existing course fails
2. Verify unique constraint on `Student.Email`
3. Test cascade delete — deleting a student removes their enrollments
4. Test that concurrent updates are handled (optimistic concurrency)
5. Compare behavior differences between InMemory and SQLite providers (document in comments)

**Minimum test count: 6 tests**

#### Example: SQLite Provider Setup

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

#### Example: Constraint Tests with Shouldly

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

#### Provider Comparison Table

| Behavior | InMemory | SQLite | SQL Server |
|---|---|---|---|
| Foreign key enforcement | No | Yes | Yes |
| Unique constraints | No | Yes | Yes |
| Cascade delete | Manual | Yes | Yes |
| Transactions | No (no-op) | Yes | Yes |
| Raw SQL / stored procedures | No | Limited | Full |
| Auto-increment behavior | Sequential | Sequential | Identity |
| `LIKE` case sensitivity | C# default | Case-insensitive | Depends on collation |

> **Hint:** Remember to configure cascade delete in `OnModelCreating` using `.OnDelete(DeleteBehavior.Cascade)`. SQLite requires `PRAGMA foreign_keys = ON` which EF Core enables by default on its connections.

### Task 3 — Testcontainers with Real Database

Use Testcontainers to spin up a real SQL Server instance in Docker for tests:

```csharp
private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
    .Build();

// In IAsyncLifetime.InitializeAsync:
await _dbContainer.StartAsync();
var connectionString = _dbContainer.GetConnectionString();
```

Write tests that:

1. Run all CRUD operations against a real SQL Server container
2. Verify foreign key constraints are enforced (compare with InMemory behavior)
3. Test stored procedure or raw SQL query execution
4. Verify that EF migrations apply cleanly to a fresh database
5. Compare query behavior between SQLite and SQL Server (document differences)

**Minimum test count: 6 tests**

> **Prerequisite**: Docker must be installed and running.

#### Example: Testcontainers Setup with IAsyncLifetime

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

> **Hint:** Testcontainers tests are slower (container startup takes 10-30 seconds). Use `IAsyncLifetime` at the class level and share the container across tests within the same class. Mark these tests with `[Trait("Category", "Integration")]` so they can be filtered during local development.

### Task 4 — Query Testing

Write tests for complex LINQ queries (using any provider):

1. Get all students enrolled in a specific course
2. Get courses with no enrollments
3. Get average grade per course
4. Get students enrolled in more than N courses

**Minimum test count: 4 tests**

#### Example: LINQ Query Tests with Shouldly

```csharp
[Fact]
public async Task GetStudentsEnrolledInCourse_ReturnsCorrectStudentsAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var mathCourse = new Course { Title = "Math", Credits = 4 };
    var artCourse = new Course { Title = "Art", Credits = 2 };

    var alice = new Student
    {
        FullName = "Alice", Email = "alice@q.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment> { new() { Course = mathCourse, Grade = 90 } }
    };
    var bob = new Student
    {
        FullName = "Bob", Email = "bob@q.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment> { new() { Course = artCourse, Grade = 80 } }
    };

    context.Students.AddRange(alice, bob);
    await context.SaveChangesAsync();

    // Act
    var mathStudents = await context.Students
        .Where(s => s.Enrollments.Any(e => e.Course.Title == "Math"))
        .ToListAsync();

    // Assert
    mathStudents.Count.ShouldBe(1);
    mathStudents.First().FullName.ShouldBe("Alice");
}

[Fact]
public async Task GetCoursesWithNoEnrollments_ReturnsEmptyCoursesAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    context.Courses.Add(new Course { Title = "Physics", Credits = 3 });
    context.Courses.Add(new Course { Title = "Chemistry", Credits = 3 });
    var math = new Course { Title = "Math", Credits = 4 };
    context.Students.Add(new Student
    {
        FullName = "Test", Email = "t@t.com",
        EnrollmentDate = DateTime.UtcNow,
        Enrollments = new List<Enrollment> { new() { Course = math, Grade = 85 } }
    });
    await context.SaveChangesAsync();

    // Act
    var emptyCourses = await context.Courses
        .Where(c => !c.Enrollments.Any())
        .ToListAsync();

    // Assert
    emptyCourses.Count.ShouldBe(2);
    emptyCourses.ShouldContain(c => c.Title == "Physics");
    emptyCourses.ShouldContain(c => c.Title == "Chemistry");
}

[Fact]
public async Task GetAverageGradePerCourse_ReturnsCorrectAveragesAsync()
{
    // Arrange
    using var context = CreateInMemoryContext();
    var course = new Course { Title = "History", Credits = 3 };
    context.Enrollments.AddRange(
        new Enrollment { Course = course, Student = new Student { FullName = "A", Email = "a@t.com", EnrollmentDate = DateTime.UtcNow }, Grade = 80 },
        new Enrollment { Course = course, Student = new Student { FullName = "B", Email = "b@t.com", EnrollmentDate = DateTime.UtcNow }, Grade = 90 }
    );
    await context.SaveChangesAsync();

    // Act
    var averages = await context.Courses
        .Select(c => new
        {
            c.Title,
            AvgGrade = c.Enrollments.Where(e => e.Grade.HasValue).Average(e => e.Grade!.Value)
        })
        .ToListAsync();

    // Assert
    var history = averages.First(a => a.Title == "History");
    history.AvgGrade.ShouldBe(85m);
}
```

> **Hint:** Seed enough data to make queries meaningful -- at least 3 students and 3 courses with various enrollment combinations. Consider edge cases like students with no enrollments and courses with all null grades.

## Grading

| Criteria |
|----------|
| Task 1 — InMemory repository tests |
| Task 2 — SQLite relational tests |
| Task 3 — Testcontainers tests |
| Task 4 — Query tests |
| Test isolation and proper async patterns |

## Submission

- Solution with `Lab5.Data` and `Lab5.Tests` projects
- Both InMemory and SQLite test classes

## References

- [EF Core Testing Overview](https://learn.microsoft.com/en-us/ef/core/testing/) — official guide to choosing a testing strategy
- [Testing with the InMemory Provider](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#inmemory-provider) — limitations and usage
- [Testing with SQLite](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database#sqlite-in-memory) — in-memory mode setup
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — container-based test infrastructure
- [Testcontainers SQL Server Module](https://dotnet.testcontainers.org/modules/mssql/) — SQL Server container configuration
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) — test framework reference
- [Shouldly Documentation](https://docs.shouldly.org/) — assertion library API
- [EF Core Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) — configuring foreign keys, cascade delete
- [EF Core Raw SQL Queries](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries) — `FromSqlRaw`, `ExecuteSqlRawAsync`
