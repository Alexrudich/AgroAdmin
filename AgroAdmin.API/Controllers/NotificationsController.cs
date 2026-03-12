using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController(AppDbContext context) : ControllerBase
{
    [HttpPost("reminders")]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderDto dto)
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

        return Ok(new { id = reminder.Id });
    }

    [HttpGet("reminders")]
    public async Task<ActionResult<List<ScheduledReminder>>> GetActiveReminders()
    {
        return await context.ScheduledReminders
            .Where(r => !r.IsSent)
            .OrderBy(r => r.ScheduledFor)
            .ToListAsync();
    }
}