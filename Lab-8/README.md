# Лабораторна 8 — Тестування продуктивності: профілювання та бенчмарки

## Мета

Навчитися писати мікробенчмарки для коду C#, профілювати виділення пам'яті та знаходити вузькі місця продуктивності за допомогою BenchmarkDotNet.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений .NET 10 SDK або новіший
- Основи C#, включаючи узагальнення, LINQ, async/await та `Span<T>`
- Розуміння типів значень та посилальних типів у .NET
- Знайомство зі збирачем сміття .NET (колекції Gen0 / Gen1 / Gen2)
- Встановлені інструменти CLI `dotnet-counters` та `dotnet-trace` (`dotnet tool install -g dotnet-counters` та `dotnet tool install -g dotnet-trace`)
- Бенчмарки **повинні** запускатися в режимі Release для отримання валідних результатів

## Ключові концепції

| Концепція | Опис |
|-----------|------|
| **Мікробенчмарк** | Точне вимірювання однієї ізольованої операції (наприклад, один виклик методу). BenchmarkDotNet автоматично обробляє розігрів, кількість ітерацій та статистичний аналіз. |
| **MemoryDiagnoser** | Діагностичний засіб BenchmarkDotNet, який звітує про виділені байти та колекції GC на операцію. Необхідний для пошуку прихованих виділень пам'яті. |
| **Розігрів** | Початкові ітерації, які відкидаються, щоб JIT-компілятор мав час оптимізувати код. BenchmarkDotNet робить це автоматично. |
| **Базовий рівень** | Метод бенчмарку, позначений `[Benchmark(Baseline = true)]`. Усі інші методи класу порівнюються з ним, показуючи співвідношення в таблиці результатів. |
| **Навантаження на GC** | Швидкість, з якою об'єкти виділяються та збираються. Високе навантаження на GC означає часті паузи та знижену пропускну здатність. |
| **Span\<T\>** | Виділений на стеку подання з перевіркою меж для безперервної пам'яті. Уникає виділень у купі при нарізці масивів або рядків. |
| **ObjectPool\<T\>** | Пул повторно використовуваних об'єктів, що уникає повторного виділення та навантаження на GC для часто створюваних/знищуваних екземплярів. |
| **ValueTask\<T\>** | Легка альтернатива `Task<T>`, що уникає виділення в купі, коли результат вже доступний синхронно. |
| **Тест на регресію продуктивності** | Тест xUnit, який перевіряє, що операція завершується в межах часового бюджету, виявляючи регресії в CI. |

## Інструменти

- Мова: C#
- Бенчмаркінг: [BenchmarkDotNet](https://benchmarkdotnet.org/)
- Профілювання: `dotnet-counters`, `dotnet-trace`
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)

## Налаштування

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

## Завдання

### Завдання 1 — Мікробенчмарки з BenchmarkDotNet

Створіть класи бенчмарків, які порівнюють:

1. **Конкатенацію рядків**: `string +=` vs `StringBuilder` vs `string.Join` vs `string.Concat` для 100, 1000 та 10000 ітерацій
2. **Пошук у колекціях**: `List<T>.Contains` vs `HashSet<T>.Contains` vs `Dictionary<TKey,TValue>.ContainsKey` для 1000 та 100000 елементів

Кожен бенчмарк повинен:

- Використовувати `[MemoryDiagnoser]` для відстеження виділень пам'яті
- Використовувати `[Params]` для тестування з різними розмірами вхідних даних
- Включати `[Benchmark(Baseline = true)]` для порівняння

**Приклад — бенчмарк конкатенації рядків**

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

**Приклад — запуск бенчмарків (Program.cs)**

```csharp
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

```bash
dotnet run -c Release --project Lab8.Benchmarks -- --filter "*StringConcatenation*"
```

**Очікувана поведінка — конкатенація рядків**

| Метод | N=100 | N=1000 | N=10000 | Виділення пам'яті |
|-------|-------|--------|---------|-------------------|
| `string +=` | Швидко | Повільно | Дуже повільно | O(n^2) виділень |
| `StringBuilder` | Швидко | Швидко | Швидко | O(n) виділень |
| `string.Join` | Швидко | Швидко | Швидко | O(n) виділень |
| `string.Concat` | Швидко | Швидко | Швидко | O(n) виділень |

> При малих розмірах різниця незначна. Квадратичний шаблон виділень `+=` стає драматичним при 10000 ітерацій.

**Очікувана поведінка — пошук у колекціях**

| Метод | N=1000 | N=100000 | Часова складність |
|-------|--------|----------|-------------------|
| `List<T>.Contains` | Швидко | Повільно | O(n) |
| `HashSet<T>.Contains` | Швидко | Швидко | O(1) амортизовано |
| `Dictionary.ContainsKey` | Швидко | Швидко | O(1) амортизовано |

**Мінімальна кількість тестів для Завдання 1**: 2 класи бенчмарків (по одному на порівняння).

> **Підказка**: Використовуйте `BenchmarkSwitcher` у вашому `Program.cs`, щоб можна було запускати окремі бенчмарки з `--filter`. Запуск усіх бенчмарків одночасно може зайняти понад годину.

### Завдання 2 — Аналіз виділення пам'яті

Напишіть код, який демонструє та порівнює:

1. `Span<T>` vs нарізка `Array` — вимірювання zero-copy проти виділення пам'яті
2. `struct` vs `class` для малих об'єктів даних — порівняння навантаження на GC

Для кожного порівняння задокументуйте:

- Виділені байти на операцію
- Колекції GC Gen0/Gen1/Gen2
- Який підхід кращий і чому

**Приклад — Span vs нарізка масиву**

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
        // Виділяє новий масив при кожному виклику
        var slice = new byte[100];
        Array.Copy(_data, 5000, slice, 0, 100);
        return slice;
    }

    [Benchmark]
    public int SpanSlice()
    {
        // Нульове виділення: створює подання над існуючою пам'яттю
        var slice = _data.AsSpan(5000, 100);
        var sum = 0;
        foreach (var b in slice)
            sum += b;
        return sum;
    }
}
```

**Приклад — Struct vs Class**

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

**Очікувана поведінка — порівняння виділення пам'яті**

| Порівняння | Виділення в купі | Колекції GC | Переможець |
|------------|-----------------|-------------|------------|
| Нарізка `Span<T>` vs `Array.Copy` | 0 Б vs ~128 Б | 0 vs Gen0 | `Span<T>` |
| `struct` vs `class` (малі дані) | 0 Б vs 24+ Б на екземпляр | 0 vs Gen0 | `struct` |

**Мінімальна кількість тестів для Завдання 2**: 2 класи бенчмарків (по одному на порівняння).

### Завдання 3 — Тести на регресію продуктивності

Напишіть тести xUnit, які забезпечують базові показники продуктивності:

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

Створіть тести для:

1. Алгоритм сортування завершується в межах часового бюджету
2. Алгоритм пошуку завершується в межах часового бюджету
3. Серіалізація/десеріалізація в межах часового бюджету
4. Задокументуйте, чому обрані ці порогові значення

**Приклад — повний клас тестів на регресію**

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

**Мінімальна кількість тестів для Завдання 3**: 3 тестових методи (сортування, пошук, серіалізація).

> **Підказка**: Обирайте порогові значення, які стабільно проходять на типовій машині розробника, але були б невдалими, якщо хтось введе регресію O(n^2). Запустіть тест 10 разів локально, щоб знайти стабільну верхню межу, а потім додайте запас безпеки 2-3x.

> **Підказка**: Якщо тести нестабільні в CI через спільні раннери, розгляньте використання `[Trait("Category", "Performance")]` та запуск їх окремо від модульних тестів.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Бенчмарки BenchmarkDotNet |
| Завдання 2 — Аналіз виділення пам'яті |
| Завдання 3 — Тести на регресію продуктивності |
| Аналіз результатів у `REPORT.md` |
| Коректна методологія бенчмарків (розігрів, ітерації) |

## Здача роботи

- Рішення з усіма трьома проєктами
- `REPORT.md` з таблицями результатів бенчмарків та аналізом
- Запуск бенчмарків у режимі Release: `dotnet run -c Release --project Lab8.Benchmarks`

## Посилання

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
