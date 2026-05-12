using Microsoft.EntityFrameworkCore;
using Ticketly.Api.Models;

namespace Ticketly.Api.Data;

public class TicketlyDbContext(DbContextOptions<TicketlyDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    public DbSet<TicketType> TicketTypes => Set<TicketType>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(ticketEvent => ticketEvent.Name).HasMaxLength(200).IsRequired();
            entity.Property(ticketEvent => ticketEvent.Venue).HasMaxLength(200);
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.Property(ticketType => ticketType.Name).HasMaxLength(200).IsRequired();
            entity.Property(ticketType => ticketType.Currency).HasMaxLength(3).IsRequired();
            entity.Property(ticketType => ticketType.Price).HasPrecision(18, 2);
            entity.HasOne<Event>()
                .WithMany()
                .HasForeignKey(ticketType => ticketType.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.Property(reservation => reservation.CustomerEmail).HasMaxLength(320).IsRequired();
            entity.HasOne<TicketType>()
                .WithMany()
                .HasForeignKey(reservation => reservation.TicketTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
