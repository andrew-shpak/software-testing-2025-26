using Lectures.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lectures.Api.Tests.Integration;

public class LecturesApiFactory : WebApplicationFactory<Program>
{
    // Returns a client backed by a fresh OrderService instance,
    // ensuring test isolation for the in-memory store.
    public HttpClient CreateIsolatedClient() =>
        WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IOrderService));
                if (descriptor != null) services.Remove(descriptor);

                services.AddSingleton<IOrderService, OrderService>();
            });
        }).CreateClient();
}
