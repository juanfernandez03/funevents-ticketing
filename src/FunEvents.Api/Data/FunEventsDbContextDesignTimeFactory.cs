using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FunEvents.Api.Data;

/// <summary>
/// Used only by <c>dotnet ef</c> to create migrations without starting the app.
/// Generating a migration does not open a connection, so the connection string is a placeholder.
/// </summary>
public sealed class FunEventsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FunEventsDbContext>
{
    public FunEventsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FunEventsDbContext>()
            .UseNpgsql("Host=localhost;Database=funevents")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new FunEventsDbContext(options);
    }
}
