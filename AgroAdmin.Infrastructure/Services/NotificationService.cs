using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Notifications; // Новый namespace
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.Infrastructure.Services;

public class NotificationService(AppDbContext context) : INotificationService
{
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
            Priority = r.Priority
        }).ToList();
    }
}