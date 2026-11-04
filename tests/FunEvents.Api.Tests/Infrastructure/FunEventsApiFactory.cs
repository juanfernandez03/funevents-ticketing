using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(FunEvents.Api.Tests.Infrastructure.FunEventsApiFactory))]

namespace FunEvents.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a real PostgreSQL in a container, shared by every test.
/// The behaviour under test (row locks, transactions, unique index) only exists in a real database,
/// so an in-memory provider would give false confidence.
/// </summary>
public sealed class FunEventsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as the Aspire AppHost uses by default.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Start the API once, before any test runs. Starting it lazily from parallel tests
        // could build the host twice.
        StartServer();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("ConnectionStrings:funevents", _postgres.GetConnectionString());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
