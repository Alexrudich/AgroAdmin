using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.Infrastructure.Services;

public class NotificationService(AppDbContext context) : INotificationService
{
    public async Task<ScheduledReminderDto?> GetByIdAsync(int id)
    {
        var reminder = await context.ScheduledReminders.FindAsync(id);
        if (reminder == null) return null;

        return new ScheduledReminderDto
        {
            Id = reminder.Id,
            Message = reminder.Message,
            ScheduledFor = reminder.ScheduledFor,
            IsSent = reminder.IsSent,
            Priority = reminder.Priority,
            TargetChatId = reminder.TargetChatId
        };
    }

    public async Task<List<ScheduledReminderDto>> GetActiveRemindersAsync()
    {
        var entities = await context.ScheduledReminders
            .Where(r => !r.IsSent)
            .OrderBy(r => r.ScheduledFor)
            .ToListAsync();

        return entities.Select(r => new ScheduledReminderDto
        {
            Id = r.Id,
            Message = r.Message,
            ScheduledFor = r.ScheduledFor,
            IsSent = r.IsSent,
            Priority = r.Priority,
            TargetChatId = r.TargetChatId
        }).ToList();
    }

    public async Task CreateReminderAsync(CreateReminderDto dto)
    {
        var reminder = new ScheduledReminder
        {
            Message = dto.Message,
            ScheduledFor = DateTime.SpecifyKind(dto.ScheduledFor, DateTimeKind.Utc),
            Priority = dto.Priority,
            TargetChatId = dto.TargetChatId,
            IsSent = false
        };

        context.ScheduledReminders.Add(reminder);
        await context.SaveChangesAsync();
    }

    public async Task UpdateReminderAsync(int id, CreateReminderDto dto)
    {
        var reminder = await context.ScheduledReminders.FindAsync(id);
        if (reminder != null)
        {
            reminder.Message = dto.Message;
            reminder.ScheduledFor = DateTime.SpecifyKind(dto.ScheduledFor, DateTimeKind.Utc);
            reminder.Priority = dto.Priority;
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteReminderAsync(int id)
    {
        var reminder = await context.ScheduledReminders.FindAsync(id);
        if (reminder != null)
        {
            context.ScheduledReminders.Remove(reminder);
            await context.SaveChangesAsync();
        }
    }

    public async Task<List<TelegramRecipientDto>> GetRecipientsAsync()
    {
        return await context.TelegramRecipients
            .Select(t => new TelegramRecipientDto
            {
                Id = t.Id,
                Name = t.Name,
                ChatId = t.ChatId,
                IsDefault = t.IsDefault,
                IsActive = t.IsActive,
                Role = t.Role,
                LastActiveAt = t.LastActiveAt,
                CreatedAt = t.CreatedAt,
                CommandCountToday = t.CommandCountToday
            })
            .ToListAsync();
    }

    public async Task AddRecipientAsync(TelegramRecipientDto dto)
    {
        var recipient = new TelegramRecipient
        {
            Name = dto.Name,
            ChatId = dto.ChatId,
            IsDefault = dto.IsDefault,
            IsActive = dto.IsActive,
            Role = dto.Role,
            CreatedAt = DateTime.UtcNow,
            CommandCountToday = 0
        };

        context.TelegramRecipients.Add(recipient);
        await context.SaveChangesAsync();
    }

    public async Task DeleteRecipientAsync(int id)
    {
        var recipient = await context.TelegramRecipients.FindAsync(id);
        if (recipient != null)
        {
            context.TelegramRecipients.Remove(recipient);
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAsSentAsync(int id)
    {
        var reminder = await context.ScheduledReminders.FindAsync(id);
        if (reminder != null)
        {
            reminder.IsSent = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateRecipientAsync(int id, TelegramRecipientDto dto)
    {
        var recipient = await context.TelegramRecipients.FindAsync(id);
        if (recipient != null)
        {
            recipient.Name = dto.Name;
            recipient.ChatId = dto.ChatId;
            recipient.IsDefault = dto.IsDefault;
            recipient.IsActive = dto.IsActive;
            recipient.Role = dto.Role;
            // LastActiveAt и CommandCountToday обновляются автоматически при использовании бота
            await context.SaveChangesAsync();
        }
    }

    public async Task UpdateRecipientStatsAsync(long chatId, bool incrementCommandCount = true)
    {
        var recipient = await context.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString());

        if (recipient != null)
        {
            recipient.LastActiveAt = DateTime.UtcNow;
            if (incrementCommandCount)
            {
                recipient.CommandCountToday++;
            }
            await context.SaveChangesAsync();
        }
    }
}