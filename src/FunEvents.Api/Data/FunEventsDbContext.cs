using FunEvents.Api.Data.Entities;
using FunEvents.Contracts.Events;
using FunEvents.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace FunEvents.Api.Data;

public sealed class FunEventsDbContext(DbContextOptions<FunEventsDbContext> options) : DbContext(options)
{
    public DbSet<EventEntity> Events => Set<EventEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<ReservationEntity> Reservations => Set<ReservationEntity>();

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

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(u => u.Code).IsUnique();
            entity.Property(u => u.Code).HasMaxLength(ReservationLimits.UserCodeMaxLength);
            entity.Property(u => u.FullName).HasMaxLength(200);
            entity.Property(u => u.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<ReservationEntity>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("ck_reservations_quantity", "quantity > 0"));

            entity.HasOne(r => r.Event).WithMany().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
