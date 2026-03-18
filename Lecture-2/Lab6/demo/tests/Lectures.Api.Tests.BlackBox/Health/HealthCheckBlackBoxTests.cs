using System.Net;
using Lectures.Api.Tests.BlackBox.Fixtures;
using Shouldly;

namespace Lectures.Api.Tests.BlackBox.Health;

public class HealthCheckBlackBoxTests : IClassFixture<BlackBoxApiFixture>
{
    private readonly HttpClient _client;

    public HealthCheckBlackBoxTests(BlackBoxApiFixture fixture)
    {
        _client = fixture.HttpClient;
    }

    // --- Shallow (liveness) ---

    [Fact]
    public async Task LivenessCheck_ReturnsHealthyAsync()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("Healthy");
    }

    // --- Deep (readiness) ---

    [Fact]
    public async Task ReadinessCheck_ReturnsHealthyAsync()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldBe("Healthy");
    }
}
