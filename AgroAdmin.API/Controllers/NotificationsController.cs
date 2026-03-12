using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroAdmin.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpPost("reminders")]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderDto dto)
    {
        await notificationService.CreateReminderAsync(dto);
        return Ok();
    }

    [HttpGet("reminders")]
    public async Task<IActionResult> GetActiveReminders()
    {
        var reminders = await notificationService.GetActiveRemindersAsync();
        return Ok(reminders);
    }
}