using AutoFixture;
using Lab6.Api.Domain;

namespace Lab6.Api.Tests.Fixtures;

public class OrderCustomization : ICustomization
{
    private int _counter;

    public void Customize(IFixture fixture)
    {
        fixture.Customize<Order>(composer => composer
            .Without(o => o.Id)
            .With(o => o.CustomerName, () => $"Customer_{Interlocked.Increment(ref _counter)}")
            .With(o => o.CustomerEmail, () => $"user{Interlocked.Increment(ref _counter)}@test.com")
            .With(o => o.ProductName, () => $"Product_{Interlocked.Increment(ref _counter)}")
            .With(o => o.Quantity, () => Random.Shared.Next(1, 1001))
            .With(o => o.UnitPrice, () => Math.Round((decimal)(Random.Shared.NextDouble() * 999) + 0.01m, 2))
            .With(o => o.Status, OrderStatus.Pending)
            .With(o => o.CreatedAt, () => DateTime.UtcNow));
    }
}
