# Lab 8 — Performance Testing: Profiling and Benchmarks

## Objective

Learn to write microbenchmarks for C# code, profile memory allocations, and identify performance hotspots using BenchmarkDotNet.

## Prerequisites

- .NET 10 SDK or later installed
- C# fundamentals including generics, LINQ, async/await, and `Span<T>`
- Understanding of value types vs reference types in .NET
- Familiarity with the .NET garbage collector (Gen0 / Gen1 / Gen2 collections)
- `dotnet-counters` and `dotnet-trace` CLI tools installed (`dotnet tool install -g dotnet-counters` and `dotnet tool install -g dotnet-trace`)
- Benchmarks **must** be run in Release mode for valid results

## Key Concepts

| Concept | Description |
|---------|-------------|
| **Microbenchmark** | A focused measurement of a single, isolated operation (e.g., one method call). BenchmarkDotNet handles warm-up, iteration counts, and statistical analysis automatically. |
| **MemoryDiagnoser** | A BenchmarkDotNet diagnoser that reports allocated bytes and GC collections per operation. Essential for finding hidden allocations. |
| **Warm-up** | The initial iterations that are discarded so the JIT compiler has time to optimize the code. BenchmarkDotNet does this automatically. |
| **Baseline** | The benchmark method marked with `[Benchmark(Baseline = true)]`. All other methods in the class are compared to it, showing a ratio in the results table. |
| **GC Pressure** | The rate at which objects are allocated and collected. High GC pressure means frequent pauses and reduced throughput. |
| **Span\<T\>** | A stack-allocated, bounds-checked view over contiguous memory. Avoids heap allocations when slicing arrays or strings. |
| **ObjectPool\<T\>** | A pool of reusable objects that avoids repeated allocation and GC pressure for frequently created/destroyed instances. |
| **ValueTask\<T\>** | A lightweight alternative to `Task<T>` that avoids a heap allocation when the result is already available synchronously. |
| **Performance Regression Test** | An xUnit test that asserts an operation completes within a time budget, catching regressions in CI. |

## Tools

- Language: C#
- Benchmarking: [BenchmarkDotNet](https://benchmarkdotnet.org/)
- Profiling: `dotnet-counters`, `dotnet-trace`
- Framework: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Setup

```bash
dotnet new sln -n Lab8
dotnet new classlib -n Lab8.Core
dotnet new console -n Lab8.Benchmarks
dotnet new classlib -n Lab8.Tests
dotnet sln add Lab8.Core Lab8.Benchmarks Lab8.Tests
dotnet add Lab8.Benchmarks reference Lab8.Core
dotnet add Lab8.Benchmarks package BenchmarkDotNet
dotnet add Lab8.Tests reference Lab8.Core
dotnet add Lab8.Tests package xunit.v3
dotnet add Lab8.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab8.Tests package Shouldly
```

## Tasks

### Task 1 — Microbenchmarks with BenchmarkDotNet

Create benchmark classes that compare:

1. **String concatenation**: `string +=` vs `StringBuilder` vs `string.Join` vs `string.Concat` for 100, 1000, and 10000 iterations
2. **Collection lookup**: `List<T>.Contains` vs `HashSet<T>.Contains` vs `Dictionary<TKey,TValue>.ContainsKey` for 1000 and 100000 elements
3. **Serialization**: `System.Text.Json` vs `Newtonsoft.Json` for a complex nested object
4. **LINQ vs loops**: `Where().Select().ToList()` vs manual `foreach` with conditions

Each benchmark must:

- Use `[MemoryDiagnoser]` to track allocations
- Use `[Params]` for testing with different input sizes
- Include `[Benchmark(Baseline = true)]` for comparison

**Example — String Concatenation Benchmark**

```csharp
using BenchmarkDotNet.Attributes;
using System.Text;

[MemoryDiagnoser]
public class StringConcatenationBenchmarks
{
    [Params(100, 1000, 10000)]
    public int Iterations { get; set; }

    [Benchmark(Baseline = true)]
    public string PlusEquals()
    {
        var result = string.Empty;
        for (var i = 0; i < Iterations; i++)
            result += "a";
        return result;
    }

    [Benchmark]
    public string StringBuilder()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < Iterations; i++)
            sb.Append("a");
        return sb.ToString();
    }

    [Benchmark]
    public string StringJoin()
    {
        var parts = new string[Iterations];
        Array.Fill(parts, "a");
        return string.Join("", parts);
    }

    [Benchmark]
    public string StringConcat()
    {
        var parts = new string[Iterations];
        Array.Fill(parts, "a");
        return string.Concat(parts);
    }
}
```

**Example — Running Benchmarks (Program.cs)**

```csharp
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

```bash
dotnet run -c Release --project Lab8.Benchmarks -- --filter "*StringConcatenation*"
```

**Expected Behavior — String Concatenation**

| Method | N=100 | N=1000 | N=10000 | Allocations |
|--------|-------|--------|---------|-------------|
| `string +=` | Fast | Slow | Very slow | O(n^2) allocations |
| `StringBuilder` | Fast | Fast | Fast | O(n) allocations |
| `string.Join` | Fast | Fast | Fast | O(n) allocations |
| `string.Concat` | Fast | Fast | Fast | O(n) allocations |

> At small sizes the difference is negligible. The quadratic allocation pattern of `+=` becomes dramatic at 10000 iterations.

**Expected Behavior — Collection Lookup**

| Method | N=1000 | N=100000 | Time Complexity |
|--------|--------|----------|-----------------|
| `List<T>.Contains` | Fast | Slow | O(n) |
| `HashSet<T>.Contains` | Fast | Fast | O(1) amortized |
| `Dictionary.ContainsKey` | Fast | Fast | O(1) amortized |

**Minimum test count for Task 1**: 4 benchmark classes (one per comparison).

> **Hint**: Use `BenchmarkSwitcher` in your `Program.cs` so you can run individual benchmarks with `--filter`. Running all benchmarks at once can take over an hour.

### Task 2 — Memory Allocation Analysis

Write code that demonstrates and benchmarks:

1. `Span<T>` vs `Array` slicing — measure zero-copy vs allocation
2. `struct` vs `class` for small data objects — compare GC pressure
3. Object pooling with `ObjectPool<T>` vs creating new instances
4. `ValueTask<T>` vs `Task<T>` for sync-completing async methods

For each comparison, document:

- Allocated bytes per operation
- GC Gen0/Gen1/Gen2 collections
- Which approach is better and why

**Example — Span vs Array Slicing**

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class SpanVsArrayBenchmarks
{
    private byte[] _data = null!;

    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[10_000];
        Random.Shared.NextBytes(_data);
    }

    [Benchmark(Baseline = true)]
    public byte[] ArraySlice()
    {
        // Allocates a new array on every call
        var slice = new byte[100];
        Array.Copy(_data, 5000, slice, 0, 100);
        return slice;
    }

    [Benchmark]
    public int SpanSlice()
    {
        // Zero-allocation: creates a view over existing memory
        var slice = _data.AsSpan(5000, 100);
        var sum = 0;
        foreach (var b in slice)
            sum += b;
        return sum;
    }
}
```

**Example — Struct vs Class**

```csharp
[MemoryDiagnoser]
public class StructVsClassBenchmarks
{
    public class PointClass { public double X; public double Y; }
    public struct PointStruct { public double X; public double Y; }

    [Params(1000, 100_000)]
    public int Count { get; set; }

    [Benchmark(Baseline = true)]
    public double SumWithClass()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
        {
            var p = new PointClass { X = i, Y = i * 2.0 };
            sum += p.X + p.Y;
        }
        return sum;
    }

    [Benchmark]
    public double SumWithStruct()
    {
        var sum = 0.0;
        for (var i = 0; i < Count; i++)
        {
            var p = new PointStruct { X = i, Y = i * 2.0 };
            sum += p.X + p.Y;
        }
        return sum;
    }
}
```

**Expected Behavior — Memory Allocation Comparison**

| Comparison | Heap Allocations | GC Collections | Winner |
|------------|-----------------|----------------|--------|
| `Span<T>` slice vs `Array.Copy` | 0 B vs ~128 B | 0 vs Gen0 | `Span<T>` |
| `struct` vs `class` (small data) | 0 B vs 24+ B per instance | 0 vs Gen0 | `struct` |
| `ObjectPool<T>` vs `new` | Amortized ~0 B vs full allocation | Reduced | `ObjectPool<T>` at scale |
| `ValueTask<T>` vs `Task<T>` (sync path) | 0 B vs ~72 B | 0 vs Gen0 | `ValueTask<T>` |

**Minimum test count for Task 2**: 4 benchmark classes (one per comparison).

> **Hint**: For `ObjectPool<T>`, use `Microsoft.Extensions.ObjectPool`. Install it via `dotnet add Lab8.Benchmarks package Microsoft.Extensions.ObjectPool`.

> **Hint**: For `ValueTask<T>` vs `Task<T>`, create an async method that completes synchronously (e.g., returns a cached value). The `Task<T>` version will still allocate a `Task` object on the heap, while `ValueTask<T>` will not.

### Task 3 — Performance Regression Tests

Write xUnit tests that enforce performance baselines:

```csharp
[Fact]
public void SortAlgorithm_ShouldCompleteWithin50ms_For10000Elements()
{
    var data = GenerateRandomArray(10000);
    var sw = Stopwatch.StartNew();

    SortService.QuickSort(data);

    sw.Stop();
    sw.ElapsedMilliseconds.ShouldBeLessThan(50);
}
```

Create tests for:

1. Sort algorithm completes within time budget
2. Search algorithm completes within time budget
3. Serialization/deserialization within time budget
4. Document why these thresholds were chosen

**Example — Full Regression Test Class**

```csharp
using System.Diagnostics;
using System.Text.Json;
using Shouldly;

public class PerformanceRegressionTests
{
    [Fact]
    public void QuickSort_10000Elements_ShouldCompleteWithin50ms()
    {
        var data = Enumerable.Range(0, 10_000)
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        var sw = Stopwatch.StartNew();
        Array.Sort(data);
        sw.Stop();

        sw.ElapsedMilliseconds.ShouldBeLessThan(50,
            "QuickSort for 10k elements exceeded the 50 ms budget");
    }

    [Fact]
    public void BinarySearch_100000Elements_ShouldCompleteWithin1ms()
    {
        var data = Enumerable.Range(0, 100_000).ToArray();

        var sw = Stopwatch.StartNew();
        Array.BinarySearch(data, 99_999);
        sw.Stop();

        sw.ElapsedMilliseconds.ShouldBeLessThan(1,
            "BinarySearch for 100k elements exceeded the 1 ms budget");
    }

    [Fact]
    public void JsonSerialization_LargeObject_ShouldCompleteWithin20ms()
    {
        var data = Enumerable.Range(0, 1000)
            .Select(i => new { Id = i, Name = $"Item {i}", Value = i * 1.5 })
            .ToList();

        var sw = Stopwatch.StartNew();
        var json = JsonSerializer.Serialize(data);
        var _ = JsonSerializer.Deserialize<List<object>>(json);
        sw.Stop();

        sw.ElapsedMilliseconds.ShouldBeLessThan(20,
            "JSON round-trip for 1000 objects exceeded the 20 ms budget");
    }
}
```

**Minimum test count for Task 3**: 3 test methods (sort, search, serialization).

> **Hint**: Choose thresholds that pass reliably on a typical developer machine but would fail if someone introduced an O(n^2) regression. Run the test 10 times locally to find a stable upper bound, then add a 2-3x safety margin.

> **Hint**: If tests are flaky in CI due to shared runners, consider using `[Trait("Category", "Performance")]` and running them separately from unit tests.

## Grading

| Criteria |
|----------|
| Task 1 — BenchmarkDotNet benchmarks |
| Task 2 — Memory allocation analysis |
| Task 3 — Performance regression tests |
| Results analysis in `REPORT.md` |
| Correct benchmark methodology (warm-up, iterations) |

## Submission

- Solution with all three projects
- `REPORT.md` with benchmark results tables and analysis
- Run benchmarks in Release mode: `dotnet run -c Release --project Lab8.Benchmarks`

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/articles/overview.html)
- [BenchmarkDotNet — MemoryDiagnoser](https://benchmarkdotnet.org/articles/features/memory-diagnoser.html)
- [BenchmarkDotNet — Parameterization](https://benchmarkdotnet.org/articles/features/parameterization.html)
- [Span\<T\> Usage Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines)
- [ObjectPool in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/objectpool)
- [ValueTask\<T\> — Understanding Why and When](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/)
- [GC Fundamentals (.NET)](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/fundamentals)
- [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
- [xUnit v3 Documentation](https://xunit.net/docs/getting-started/v3/cmdline)
- [Shouldly Assertion Library](https://docs.shouldly.org/)
