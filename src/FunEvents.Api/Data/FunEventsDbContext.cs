using FunEvents.Api.Data.Entities;
using FunEvents.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.Data;

public sealed class FunEventsDbContext(DbContextOptions<FunEventsDbContext> options) : DbContext(options)
{
    public DbSet<EventEntity> Events => Set<EventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventEntity>(entity =>
        {
            // Last line of defence against overselling: the database rejects a negative counter.
            entity.ToTable(table => table.HasCheckConstraint(
                "ck_events_available_tickets",
                "available_tickets >= 0 AND available_tickets <= total_tickets"));

            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(EventLimits.CodeMaxLength);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Venue).HasMaxLength(200);
        });
    }
}
