using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.Data;

public static class DataDependencyInjection
{
    /// <summary>Name of the connection string, matching the database resource in the AppHost.</summary>
    public const string ConnectionName = "funevents";

    public static IHostApplicationBuilder AddData(this IHostApplicationBuilder builder)
    {
        // Aspire integration: connection string from configuration, pooling, retries, health check and tracing.
        builder.AddNpgsqlDbContext<FunEventsDbContext>(ConnectionName, configureDbContextOptions: options => options
            .UseSnakeCaseNamingConvention()
            .UseSeeding(SeedData.Seed)
            .UseAsyncSeeding(SeedData.SeedAsync));

        return builder;
    }

    /// <summary>Applies pending migrations and seeds the database. Meant for local development and tests.</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FunEventsDbContext>();
        await db.Database.MigrateAsync();
    }
}
