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

        // In-memory only. Registering a distributed cache (e.g. Redis) makes it the second level, no code change.
        builder.Services.AddHybridCache();
        // Singleton: it opens its own scope per lookup, so it never holds a request's DbContext.
        builder.Services.AddSingleton<CodeLookup>();

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
