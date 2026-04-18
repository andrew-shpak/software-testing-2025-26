# Лабораторна 1 — Модульне тестування: Основи

> **Lab → 2 points**

## Мета

Вивчити основи модульного тестування в C#: написання тестових випадків за допомогою xUnit v3, використання перевірок (assertions), структурування тестів за шаблоном Arrange-Act-Assert (AAA) та досягнення значущого покриття коду.

**Тривалість:** 60 хвилин

## Передумови

- Встановлений .NET 10+ SDK
- Базові знання C# (класи, методи, узагальнення)
- IDE: Visual Studio / Rider / VS Code з розширенням для C#

## Інструменти

- Мова: C#
- Фреймворк: [xUnit v3](https://xunit.net/) (`xunit.v3`)
- Перевірки: `Xunit.Assert` / [Shouldly](https://docs.shouldly.org/)

## Ключові поняття

- **Модульний тест** — тест, що перевіряє одну одиницю поведінки ізольовано
- **Шаблон AAA** — Arrange (підготовка), Act (виконання), Assert (перевірка)
- **`[Fact]`** — позначає тестовий метод без параметрів
- **`[Theory]`** — позначає параметризований тест з `[InlineData]`, `[MemberData]` або `[ClassData]`
- **Покриття коду** — відсоток коду, що виконується під час тестів

## Налаштування

```bash
dotnet new sln -n Lab1
dotnet new classlib -n Lab1.Core
dotnet new classlib -n Lab1.Tests
dotnet sln add Lab1.Core Lab1.Tests
dotnet add Lab1.Tests reference Lab1.Core
dotnet add Lab1.Tests package xunit.v3
dotnet add Lab1.Tests package Microsoft.NET.Test.Sdk
dotnet add Lab1.Tests package Shouldly
```

Запуск тестів:

```bash
dotnet test --verbosity normal
dotnet test --collect:"XPlat Code Coverage"
```

## Завдання

### Завдання 1 — Калькулятор

Створіть клас `Calculator` у `Lab1.Core` з методами:

- `double Add(double a, double b)`
- `double Subtract(double a, double b)`
- `double Multiply(double a, double b)`
- `double Divide(double a, double b)` — кидає `DivideByZeroException`, коли `b == 0`

Напишіть тести у `CalculatorTests.cs`, що покривають:

1. Звичайні випадки для кожної операції
2. Від'ємні числа
3. Точність обчислень з плаваючою комою (`0.1 + 0.2`)
4. Ділення на нуль кидає `DivideByZeroException`

**Приклад структури тесту:**

```csharp
public class CalculatorTests
{
    private readonly Calculator _sut = new();

    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsSum()
    {
        // Arrange
        double a = 2, b = 3;

        // Act
        var result = _sut.Add(a, b);

        // Assert
        result.ShouldBe(5);
    }

    [Theory]
    [InlineData(10, 5, 2)]
    [InlineData(-10, 2, -5)]
    [InlineData(0, 1, 0)]
    public void Divide_ValidInputs_ReturnsQuotient(double a, double b, double expected)
    {
        _sut.Divide(a, b).ShouldBe(expected);
    }

    [Fact]
    public void Divide_ByZero_ThrowsDivideByZeroException()
    {
        Should.Throw<DivideByZeroException>(() => _sut.Divide(10, 0));
    }
}
```

**Мінімальна кількість тестів:** 8 тестів (2 на операцію)

### Завдання 2 — Утиліти для рядків

Створіть клас `StringUtils` з методами:

- `string Capitalize(string input)` — робить першу літеру кожного слова великою
- `string Reverse(string input)` — перевертає рядок
- `bool IsPalindrome(string input)` — перевірка на паліндром без урахування регістру
- `string Truncate(string input, int maxLength)` — обрізає та додає `"..."` за потреби

Напишіть тести, що покривають:

1. Звичайні вхідні дані
2. `null` та порожні рядки (мають кидати `ArgumentNullException` або повертати порожній рядок)
3. Рядки з одного символу
4. Обробка Unicode та пробілів

**Очікувана поведінка:**

| Метод | Вхідні дані | Очікуваний результат |
|--------|-------|-----------------|
| `Capitalize` | `"hello world"` | `"Hello World"` |
| `Capitalize` | `"HELLO"` | `"Hello"` |
| `Reverse` | `"abcd"` | `"dcba"` |
| `IsPalindrome` | `"Racecar"` | `true` |
| `IsPalindrome` | `"Hello"` | `false` |
| `Truncate` | `"Hello World", 5` | `"Hello..."` |
| `Truncate` | `"Hi", 10` | `"Hi"` |

**Підказка:** Використовуйте `[Theory]` з `[InlineData]` для табличних тестів.

**Мінімальна кількість тестів:** 12 тестів (3 на метод)

### Завдання 3 — Утиліти для колекцій

Створіть клас `CollectionUtils` з методами:

- `double Average(IEnumerable<double> numbers)` — повертає середнє значення; кидає `InvalidOperationException` для порожньої колекції
- `T Max<T>(IEnumerable<T> items) where T : IComparable<T>` — повертає максимальний елемент; кидає `InvalidOperationException` для порожньої колекції
- `IEnumerable<T> Distinct<T>(IEnumerable<T> items)` — повертає унікальні елементи зі збереженням порядку
- `IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> items, int size)` — розбиває колекцію на частини заданого розміру; кидає `ArgumentOutOfRangeException` для `size <= 0`

Напишіть тести, що покривають:

1. Звичайні вхідні дані
2. Порожні колекції (відповідні винятки)
3. Колекція з одного елемента
4. `null` вхідні дані (мають кидати `ArgumentNullException`)
5. Граничні значення (від'ємні числа, великі колекції, розмір частини більший за кількість елементів)

**Очікувана поведінка:**

| Метод | Вхідні дані | Очікуваний результат |
|--------|-------|-----------------|
| `Average` | `[1, 2, 3, 4, 5]` | `3.0` |
| `Average` | `[]` | `InvalidOperationException` |
| `Max` | `[3, 1, 4, 1, 5]` | `5` |
| `Max` | `["apple", "cherry", "banana"]` | `"cherry"` |
| `Distinct` | `[1, 2, 2, 3, 1]` | `[1, 2, 3]` |
| `Chunk` | `[1, 2, 3, 4, 5], 2` | `[[1, 2], [3, 4], [5]]` |
| `Chunk` | `[1, 2], 5` | `[[1, 2]]` |

**Підказка:** Використовуйте `[Theory]` з `[MemberData]` для тестів з колекціями, оскільки `[InlineData]` не підтримує масиви.

**Мінімальна кількість тестів:** 10 тестів

## Запуск покриття коду

### 1. Додайте пакет Coverlet

```bash
dotnet add Lab1.Tests package coverlet.collector
```

### 2. Запустіть тести зі збором покриття

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
```

### 3. Згенеруйте HTML-звіт

Встановіть ReportGenerator (одноразово):

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

Згенеруйте звіт:

```bash
reportgenerator \
  -reports:./coverage/**/coverage.cobertura.xml \
  -targetdir:./coverage/report \
  -reporttypes:"Html;TextSummary"
```

### 4. Перегляньте результати

```bash
# Текстовий підсумок у терміналі
cat ./coverage/report/Summary.txt
```

Відкрийте `./coverage/report/index.html` у браузері для детального звіту з посторінковим розбиттям.

> **Примітка:** Додайте `coverage/` до `.gitignore`, щоб не комітити згенеровані звіти.

## Оцінювання

| Критерії |
|----------|
| Завдання 1 — Тести калькулятора |
| Завдання 2 — Тести утиліт для рядків |
| Завдання 3 — Тести утиліт для колекцій |
| Якість тестів (шаблон AAA, описові назви, використання `[Theory]`) |
| Покриття коду >= 80% |

## Здача роботи

- Рішення з проєктами `Lab1.Core` та `Lab1.Tests`
- Виконайте `dotnet test --collect:"XPlat Code Coverage"` та додайте звіт про покриття
- Мінімум 30 тестів загалом по всіх завданнях

## Посилання

- [Документація xUnit v3](https://xunit.net/docs/getting-started/v3/cmdline)
- [Документація Shouldly](https://docs.shouldly.org/)
- [Найкращі практики модульного тестування — Microsoft](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
