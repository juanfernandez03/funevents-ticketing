using FunEvents.Api.UseCases.Events;

namespace FunEvents.Api.UseCases;

public static class UseCasesDependencyInjection
{
    /// <summary>Use cases are concrete classes: one per action, no mediator, no interfaces.</summary>
    public static IServiceCollection AddUseCases(this IServiceCollection services) => services
        .AddSingleton(TimeProvider.System)
        .AddScoped<GetEvent>();
}
