using System.Data.Common;
using System.Net;
using FunEvents.Api.Data;
using FunEvents.Api.Tests.Infrastructure;
using FunEvents.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FunEvents.Api.Tests;

public sealed class CommitFailureTests(FunEventsApiFactory api)
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public async Task Reserve_WhenCommitSucceedsButAcknowledgementIsLost_ReturnsOriginalReservationOnce(
        int tickets, bool useKey)
    {
        var eventCode = await api.CreateEventAsync(tickets);
        var userCode = await api.CreateUserAsync();
        var interceptor = new LostCommitAcknowledgementInterceptor();
        await using var host = api.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<FunEventsDbContext>(options => options.AddInterceptors(interceptor),
                ServiceLifetime.Singleton)));
        using var client = host.CreateClient();
        var key = useKey ? Guid.NewGuid().ToString() : null;
        interceptor.Arm();

        using var response = await client.ReserveAsync(eventCode, userCode, quantity: 1, key);

        Assert.Equal(1, interceptor.FailuresInjected);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains(ApiHeaders.IdempotentReplayed));
        var reservation = await response.ReadReservationAsync();
        Assert.Equal(eventCode, reservation.EventCode);
        Assert.Equal(userCode, reservation.UserCode);
        Assert.Equal(1, reservation.Quantity);
        Assert.Equal($"/reservations/{reservation.Id}", response.Headers.Location?.OriginalString);
        using var fetched = await client.GetAsync($"/reservations/{reservation.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(reservation, await fetched.ReadReservationAsync());
        Assert.Equal(1, await api.CountReservationsAsync(eventCode));
        Assert.Equal(1, await api.CountReservedTicketsAsync(eventCode));
        Assert.Equal(tickets - 1, await api.GetAvailableTicketsAsync(eventCode));

        if (key is not null)
        {
            using var replay = await client.ReserveAsync(eventCode, userCode, quantity: 1, key);
            Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
            Assert.Equal("true", Assert.Single(replay.Headers.GetValues(ApiHeaders.IdempotentReplayed)));
            Assert.Equal(reservation, await replay.ReadReservationAsync());
            Assert.Equal(1, await api.CountReservationsAsync(eventCode));
            Assert.Equal(tickets - 1, await api.GetAvailableTicketsAsync(eventCode));
        }
    }

    // EF calls this after the real PostgreSQL commit: model a lost acknowledgement, not a rollback.
    private sealed class LostCommitAcknowledgementInterceptor : DbTransactionInterceptor
    {
        private int _armed;
        public int FailuresInjected { get; private set; }

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 1)
            {
                FailuresInjected++;
                throw new NpgsqlException("Simulated lost commit acknowledgement.", new TimeoutException());
            }

            return Task.CompletedTask;
        }
    }
}
