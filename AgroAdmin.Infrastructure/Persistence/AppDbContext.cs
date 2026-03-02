using AgroAdmin.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgroAdmin.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<SaunaOrder> SaunaOrders => Set<SaunaOrder>();

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
            });

            // Конфигурация для SaunaOrder
            modelBuilder.Entity<SaunaOrder>(builder =>
            {
                builder.HasKey(s => s.Id);
            });
        }
    }
}
