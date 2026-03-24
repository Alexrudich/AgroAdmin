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
        public DbSet<ScheduledReminder> ScheduledReminders => Set<ScheduledReminder>();
        public DbSet<TelegramRecipient> TelegramRecipients => Set<TelegramRecipient>();
        public DbSet<GuestGroup> GuestGroups => Set<GuestGroup>();
        public DbSet<GuestGroupMember> GuestGroupMembers => Set<GuestGroupMember>();
        public DbSet<BackupInfo> BackupInfos => Set<BackupInfo>();
        public DbSet<PricingConfiguration> PricingConfigurations => Set<PricingConfiguration>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация для Booking
            modelBuilder.Entity<Booking>(builder =>
            {
                builder.HasKey(b => b.Id);

                builder.Metadata.FindNavigation(nameof(Booking.SaunaOrders))
                    ?.SetPropertyAccessMode(PropertyAccessMode.Field);

                // Настройка связи: Один букинг -> Много саун
                builder.HasMany(b => b.SaunaOrders)
                    .WithOne()
                    .HasForeignKey(s => s.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

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
            modelBuilder.Entity<GuestGroup>(builder =>
            {
                builder.HasKey(g => g.Id);
                builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
            });

            modelBuilder.Entity<GuestGroupMember>(builder =>
            {
                builder.HasKey(g => new { g.GuestId, g.GroupId });

                builder.HasOne(g => g.Guest)
                    .WithMany(g => g.GroupMembers)
                    .HasForeignKey(g => g.GuestId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(g => g.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(g => g.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            // Конфигурация для ScheduledReminder
            modelBuilder.Entity<ScheduledReminder>(builder =>
            {
                builder.HasKey(r => r.Id);

                builder.Property(r => r.Message)
                    .IsRequired()
                    .HasMaxLength(2000);

                builder.Property(r => r.TargetChatId)
                    .HasMaxLength(500);

                builder.HasIndex(r => new { r.ScheduledFor, r.IsSent });
            });

            // Конфигурация для TelegramRecipient
            modelBuilder.Entity<TelegramRecipient>(builder =>
            {
                builder.HasKey(t => t.Id);
                builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
                builder.Property(t => t.ChatId).IsRequired().HasMaxLength(50);
            });

            // Конфигурация для BackupInfo
            modelBuilder.Entity<BackupInfo>(builder =>
            {
                builder.HasKey(b => b.Id);
            });

            // Конфигурация для PricingConfiguration
            modelBuilder.Entity<PricingConfiguration>(builder =>
            {
                builder.HasKey(p => p.Id);

                builder.Property(p => p.UpdatedBy)
                    .HasMaxLength(100);
            });
        }
    }
}