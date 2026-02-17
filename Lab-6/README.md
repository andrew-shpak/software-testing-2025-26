# Lab 6 — Database Testing: Data Integrity and Transactions

## Objective

Test transactional behavior, data seeding, migration correctness, and repository patterns with real database constraints.

## Prerequisites

Before starting this lab, make sure you have:

- .NET 8+ SDK installed (`dotnet --version`)
- Docker installed and running (required for Testcontainers PostgreSQL)
- A working understanding of database transactions, ACID properties, and EF Core `SaveChanges` behavior
- Familiarity with EF Core migrations, seed data, and interceptors/`SaveChanges` override
- Completed Lab 5 (database testing with InMemory, SQLite, and Testcontainers)

## Key Concepts

### ACID Transactions

A transaction groups multiple database operations into a single atomic unit. Either all operations succeed (commit) or all are rolled back. In this lab, the `TransferService` must guarantee that money is never lost: if the sender's deduction succeeds but the receiver's credit fails, both changes must be reverted.

### Optimistic vs. Pessimistic Concurrency

When multiple threads or requests modify the same row simultaneously, conflicts can occur. EF Core supports optimistic concurrency via `[ConcurrencyCheck]` or row version columns. The database rejects updates where the row has changed since it was last read. This is critical for preventing negative balances under concurrent transfers.

### Data Seeding

EF Core's `HasData()` method in `OnModelCreating` allows you to define seed data that is applied when migrations run. Tests should verify that seeded data exists after migration and that schema changes (adding/removing columns) preserve existing data.

### Audit Trails via SaveChanges Override

By overriding `SaveChanges` or using EF Core interceptors, you can automatically record every entity change (create, update, delete) into an `AuditLog` table. The audit entry must be written in the same transaction as the entity change, ensuring consistency.

## Tools

- Language: C#
- ORM: [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- Test DB: SQLite in-memory / [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Setup

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

## Tasks

### Task 1 — Transaction Testing with Testcontainers

Create a `BankAccountService` that manages transfers between accounts:

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

Use Testcontainers to run a real PostgreSQL instance:

```csharp
private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
    .WithImage("postgres:16-alpine")
    .Build();
```

Write tests that verify:

1. Successful transfer decreases sender balance and increases receiver balance
2. Transfer with insufficient funds is rejected and both balances remain unchanged (rollback)
3. Transfer to non-existing account fails and sender balance is unchanged
4. Transfer of zero or negative amount is rejected
5. Concurrent transfers from the same account do not cause negative balance

**Minimum test count: 7 tests**

> **Prerequisite**: Docker must be installed and running.

#### Example: Testcontainers PostgreSQL Setup

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

        // Seed test accounts
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

#### Example: Transaction Tests with Shouldly

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

    // Reload from database to verify persisted state
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
    var result = await _service.TransferAsync(alice.Id, bob.Id, 5000m); // Alice only has 1000

    // Assert
    result.Success.ShouldBeFalse();

    // Verify rollback — balances unchanged
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
    alice.Balance.ShouldBe(1000m); // unchanged
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
    // Arrange — Alice has 1000, attempt two 600 transfers concurrently
    var alice = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Alice");
    var bob = await _context.BankAccounts.FirstAsync(a => a.OwnerName == "Bob");

    // Act — run two transfers in parallel
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

    // Assert — at most one should succeed
    var successCount = results.Count(r => r.Success);
    successCount.ShouldBeLessThanOrEqualTo(1);

    // Verify Alice's balance is never negative
    await _context.Entry(alice).ReloadAsync();
    alice.Balance.ShouldBeGreaterThanOrEqualTo(0m);
}
```

#### Expected Behavior Table

| Scenario | Expected Result | Sender Balance | Receiver Balance |
|---|---|---|---|
| Transfer 200 (sender has 1000) | Success | 800 | +200 |
| Transfer 5000 (sender has 1000) | Failure (rollback) | 1000 (unchanged) | unchanged |
| Transfer to non-existing account | Failure | unchanged | N/A |
| Transfer negative amount | Failure (rejected) | unchanged | unchanged |
| Transfer zero amount | Failure (rejected) | unchanged | unchanged |
| Two concurrent 600 transfers (sender has 1000) | At most one succeeds | >= 0 | varies |

> **Hint:** For concurrent transfer testing, each parallel task must use its own `DbContext` instance (EF Core contexts are not thread-safe). Use `IDbContextFactory<AppDbContext>` or manually create new contexts with the same connection string. Consider using `[ConcurrencyCheck]` on the `Balance` property or a `[Timestamp]` row version column.

### Task 2 — Data Seeding and Migration Tests

Create a seed data configuration and test:

1. Verify seed data is applied correctly after migration
2. Verify adding a new required column with default value migrates existing data
3. Test that removing a column does not lose data in remaining columns
4. Verify index creation improves query plan (use `EXPLAIN` with SQLite)

**Minimum test count: 4 tests**

#### Example: Seed Data and Migration Tests with Shouldly

```csharp
[Fact]
public async Task SeedData_AppliedAfterMigrationAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        // Act — EnsureCreated applies the schema and seed data
        await context.Database.EnsureCreatedAsync();

        // Assert
        var accounts = await context.BankAccounts.ToListAsync();
        accounts.ShouldNotBeEmpty();
        accounts.ShouldContain(a => a.AccountNumber == "ACC-SEED-001");
    }
}

[Fact]
public async Task IndexCreation_VisibleInQueryPlanAsync()
{
    // Arrange
    var (context, connection) = CreateSqliteContext();
    using (connection)
    using (context)
    {
        // Create index
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_BankAccounts_AccountNumber ON BankAccounts(AccountNumber)");

        // Seed data
        for (int i = 0; i < 100; i++)
        {
            context.BankAccounts.Add(new BankAccount
            {
                AccountNumber = $"ACC-{i:D4}", OwnerName = $"Owner {i}", Balance = 100m
            });
        }
        await context.SaveChangesAsync();

        // Act — run EXPLAIN to see query plan
        var plan = await context.Database
            .SqlQueryRaw<string>("EXPLAIN QUERY PLAN SELECT * FROM BankAccounts WHERE AccountNumber = 'ACC-0050'")
            .ToListAsync();

        // Assert — plan should mention the index
        var planText = string.Join(" ", plan);
        planText.ShouldContain("IX_BankAccounts_AccountNumber");
    }
}
```

> **Hint:** For migration tests, you can use `context.Database.MigrateAsync()` instead of `EnsureCreated()`. To test schema changes, create separate migration snapshots or use `ExecuteSqlRawAsync` to simulate `ALTER TABLE` operations directly.

### Task 3 — Audit Trail Testing

Implement an audit trail using EF Core interceptors or `SaveChanges` override:

```csharp
public class AuditLog
{
    public int Id { get; set; }
    public string EntityName { get; set; }
    public string Action { get; set; }       // "Created", "Updated", "Deleted"
    public string Changes { get; set; }       // JSON of changed properties
    public DateTime Timestamp { get; set; }
}
```

Write tests that verify:

1. Creating an entity generates an audit log entry with `Action = "Created"`
2. Updating an entity logs changed properties
3. Deleting an entity generates a log with `Action = "Deleted"`
4. Audit log is written in the same transaction as the entity change

**Minimum test count: 5 tests**

#### Example: SaveChanges Override for Auditing

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

#### Example: Audit Trail Tests with Shouldly

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

        // Clear audit logs from creation
        context.AuditLogs.RemoveRange(context.AuditLogs);
        await context.SaveChangesAsync(); // this will also generate logs, handle accordingly

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
        // Act — create an entity (audit log should be saved atomically)
        context.BankAccounts.Add(new BankAccount
        {
            AccountNumber = "AUDIT-004", OwnerName = "Atomic", Balance = 100m
        });
        await context.SaveChangesAsync();

        // Assert — both entity and audit log exist (single transaction)
        var accountExists = await context.BankAccounts.AnyAsync(a => a.AccountNumber == "AUDIT-004");
        var auditExists = await context.AuditLogs.AnyAsync(l => l.EntityName == "BankAccount" && l.Action == "Created");

        accountExists.ShouldBeTrue();
        auditExists.ShouldBeTrue();
    }
}
```

#### Expected Behavior Table — Audit Trail

| Operation | AuditLog.Action | AuditLog.EntityName | AuditLog.Changes |
|---|---|---|---|
| Add new BankAccount | `"Created"` | `"BankAccount"` | JSON with all property values |
| Update OwnerName | `"Updated"` | `"BankAccount"` | JSON containing `"OwnerName"` |
| Delete BankAccount | `"Deleted"` | `"BankAccount"` | JSON with deleted entity's values |

> **Hint:** Be careful with the `SaveChanges` override -- do not generate audit logs for `AuditLog` entity changes themselves, or you will create an infinite loop. Filter out `AuditLog` entries in the `ChangeTracker` query. Also note that the `AuditLog` entries are added to the same `SaveChanges` batch, so they participate in the same database transaction automatically.

## Grading

| Criteria |
|----------|
| Task 1 — Transaction tests |
| Task 2 — Seeding and migration tests |
| Task 3 — Audit trail tests |
| Correct transaction rollback verification |
| Test isolation and cleanup |

## Submission

- Solution with `Lab6.Data` and `Lab6.Tests` projects
- All transactional tests must prove atomicity (both sides of transaction verified)

## References

- [EF Core Transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions) — explicit transactions, `BeginTransaction`, `SaveChanges` behavior
- [EF Core Concurrency Conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) — optimistic concurrency, `[ConcurrencyCheck]`, row versions
- [EF Core Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) — `HasData()` in `OnModelCreating`
- [EF Core Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors) — `SaveChangesInterceptor`, auditing patterns
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — container-based test infrastructure
- [Testcontainers PostgreSQL Module](https://dotnet.testcontainers.org/modules/postgres/) — PostgreSQL container configuration
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline) — test framework reference
- [Shouldly Documentation](https://docs.shouldly.org/) — assertion library API
- [Npgsql EF Core Provider](https://www.npgsql.org/efcore/) — PostgreSQL-specific EF Core features
