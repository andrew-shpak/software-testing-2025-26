using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace Lectures.Api.Tests.BlackBox.Fixtures;

public class BlackBoxApiFixture : IAsyncLifetime
{
    private readonly INetwork _network = new NetworkBuilder()
        .WithName($"blackbox-{Guid.NewGuid():N}")
        .Build();

    private readonly PostgreSqlContainer _postgres;
    private IFutureDockerImage _apiImage = null!;
    private IContainer _apiContainer = null!;

    public HttpClient HttpClient { get; private set; } = null!;

    private const string PostgresAlias = "postgres";
    private const string DbName = "lectures";
    private const string DbUser = "postgres";
    private const string DbPassword = "postgres";

    public BlackBoxApiFixture()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:18")
            .WithNetwork(_network)
            .WithNetworkAliases(PostgresAlias)
            .WithDatabase(DbName)
            .WithUsername(DbUser)
            .WithPassword(DbPassword)
            .WithCleanUp(true)
            .Build();
    }

    private static string FindSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.slnx").Length > 0 || dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not find solution directory (*.sln or *.slnx) walking up from " + AppContext.BaseDirectory);
    }

    public async ValueTask InitializeAsync()
    {
        await _network.CreateAsync();

        var solutionDir = FindSolutionDirectory();

        _apiImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDir)
            .WithDockerfile("Dockerfile")
            .WithCleanUp(true)
            .Build();

        // Build image and start Postgres in parallel
        await Task.WhenAll(
            _apiImage.CreateAsync(),
            _postgres.StartAsync());

        // Postgres is ready — now start the API
        var connectionString =
            $"Host={PostgresAlias};Port=5432;Database={DbName};Username={DbUser};Password={DbPassword}";

        _apiContainer = new ContainerBuilder()
            .WithImage(_apiImage)
            .WithNetwork(_network)
            .WithPortBinding(8080, true)
            .WithEnvironment("ConnectionStrings__DefaultConnection", connectionString)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPort(8080)
                    .ForPath("/health/ready")))
            .WithCleanUp(true)
            .Build();

        await _apiContainer.StartAsync();

        var host = _apiContainer.Hostname;
        var port = _apiContainer.GetMappedPublicPort(8080);
        HttpClient = new HttpClient { BaseAddress = new Uri($"http://{host}:{port}") };
    }

    public async ValueTask DisposeAsync()
    {
        HttpClient.Dispose();

        await _apiContainer.DisposeAsync();
        await _apiImage.DisposeAsync();
        await _postgres.DisposeAsync();
        await _network.DeleteAsync();
    }
}
