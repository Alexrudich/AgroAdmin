using AgroAdmin.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.Infrastructure.Persistence
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<SaunaOrder> SaunaOrders => Set<SaunaOrder>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация для Booking
            modelBuilder.Entity<Booking>(builder =>
            {
                builder.HasKey(b => b.Id);

                // Говорим EF работать с приватным полем _saunaOrders напрямую
                builder.Metadata.FindNavigation(nameof(Booking.SaunaOrders))
                    ?.SetPropertyAccessMode(PropertyAccessMode.Field);

                // Настройка связи: Один букинг -> Много саун
                builder.HasMany(b => b.SaunaOrders)
                    .WithOne()
                    .HasForeignKey(s => s.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Настройка нового поля FeedbackComment (если нужно)
                builder.Property(b => b.FeedbackComment)
                    .HasMaxLength(1000);
            });

            // Конфигурация для SaunaOrder
            modelBuilder.Entity<SaunaOrder>(builder =>
            {
                builder.HasKey(s => s.Id);
            });

            // Конфигурация для Guest
            modelBuilder.Entity<Guest>(builder =>
            {
                builder.HasKey(g => g.Id);

                builder.Property(g => g.FullName)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(g => g.Phone)
                    .IsRequired()
                    .HasMaxLength(20);

                builder.Property(g => g.Comment)
                    .HasMaxLength(500);

                builder.Property(g => g.CreatedAt)
                    .IsRequired();
            });

            // Конфигурация для AdminUser
            modelBuilder.Entity<AdminUser>(builder =>
            {
                builder.HasKey(a => a.Id);
                builder.Property(a => a.Username)
                    .IsRequired()
                    .HasMaxLength(50);
                builder.Property(a => a.PasswordHash)
                    .IsRequired();
                builder.HasIndex(a => a.Username)
                    .IsUnique();
            });
        }
    }
}