using FunEvents.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(FunEvents.Api.Tests.Infrastructure.FunEventsApiFactory))]

namespace FunEvents.Api.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a real PostgreSQL in a container, shared by every test.
/// The behaviour under test (row locks, transactions, unique index) only exists in a real database,
/// so an in-memory provider would give false confidence.
/// </summary>
/// <remarks>
/// Test data is written and checked with plain SQL, so the tests treat the API as a black box
/// and do not depend on how it maps its entities.
/// </remarks>
public sealed class FunEventsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as the Aspire AppHost uses by default.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.3").Build();
    private NpgsqlDataSource? _dataSource;

    private NpgsqlDataSource DataSource => _dataSource ?? throw new InvalidOperationException("Fixture not initialised.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_postgres.GetConnectionString());

        // Start the API once, before any test runs: it applies migrations and seeds the database.
        // Starting it lazily from parallel tests could run the seeding twice.
        StartServer();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting($"ConnectionStrings:{DataDependencyInjection.ConnectionName}", _postgres.GetConnectionString());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Creates an event that belongs to a single test, so tests never compete for the same inventory.
    /// It starts in 30 days unless <paramref name="startsIn"/> says otherwise (negative: already started).
    /// </summary>
    public async Task<string> CreateEventAsync(int tickets, string? code = null, TimeSpan? startsIn = null)
    {
        code ??= NewEventCode();
        await ExecuteAsync(
            """
            INSERT INTO events (id, code, name, venue, starts_at_utc, total_tickets, available_tickets)
            VALUES (@id, @code, @name, 'Test venue', now() + @startsIn, @tickets, @tickets)
            """,
            ("id", Guid.CreateVersion7()), ("code", code), ("name", $"Test event {code}"), ("tickets", tickets),
            ("startsIn", startsIn ?? TimeSpan.FromDays(30)));
        return code;
    }

    public async Task<string> CreateUserAsync(string? code = null)
    {
        code ??= NewUserCode();
        await ExecuteAsync(
            "INSERT INTO users (id, code, full_name, email) VALUES (@id, @code, 'Test user', @email)",
            ("id", Guid.CreateVersion7()), ("code", code), ("email", $"{code.ToLowerInvariant()}@example.com"));
        return code;
    }

    public Task<int> GetAvailableTicketsAsync(string eventCode) =>
        ScalarAsync<int>("SELECT available_tickets FROM events WHERE code = @code", ("code", eventCode));

    public async Task<int> CountReservedTicketsAsync(string eventCode) =>
        (int)await ScalarAsync<long>(
            """
            SELECT COALESCE(SUM(r.quantity), 0)
            FROM reservations r JOIN events e ON e.id = r.event_id
            WHERE e.code = @code
            """,
            ("code", eventCode));

    public async Task<int> CountReservationsAsync(string eventCode) =>
        (int)await ScalarAsync<long>(
            "SELECT COUNT(*) FROM reservations r JOIN events e ON e.id = r.event_id WHERE e.code = @code",
            ("code", eventCode));

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(sql, parameters);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"No result for: {sql}"));
    }

    private NpgsqlCommand CreateCommand(string sql, (string Name, object Value)[] parameters)
    {
        var command = DataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    public static string NewEventCode() => UniqueCode("EVT");

    public static string NewUserCode() => UniqueCode("USR");

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}
