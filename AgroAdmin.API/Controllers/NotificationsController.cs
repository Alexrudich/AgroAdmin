using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AgroAdmin.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet("reminders")]
    public async Task<IActionResult> GetActiveReminders()
    {
        Log.Information("[NotificationsAPI] GET reminders. User: {User}, IsAuth: {IsAuth}",
            User.Identity?.Name ?? "None", User.Identity?.IsAuthenticated);

        var reminders = await notificationService.GetActiveRemindersAsync();
        return Ok(reminders);
    }

    [HttpPost("reminders")]
    public async Task<IActionResult> CreateReminder([FromBody] CreateReminderDto dto)
    {
        Log.Information("[NotificationsAPI] POST reminder. User: {User}, Msg: {Msg}",
            User.Identity?.Name, dto.Message);

        await notificationService.CreateReminderAsync(dto);
        return Ok();
    }

    [HttpPut("reminders/{id}")]
    public async Task<IActionResult> UpdateReminder(int id, [FromBody] CreateReminderDto dto)
    {
        Log.Information("[NotificationsAPI] PUT reminder {Id}. User: {User}", id, User.Identity?.Name);
        await notificationService.UpdateReminderAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("reminders/{id}")]
    public async Task<IActionResult> DeleteReminder(int id)
    {
        Log.Information("[NotificationsAPI] DELETE reminder {Id}. User: {User}", id, User.Identity?.Name);
        var reminder = await notificationService.GetByIdAsync(id);
        if (reminder == null) return NotFound();

        await notificationService.DeleteReminderAsync(id);
        return NoContent();
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients()
    {
        Log.Information("[NotificationsAPI] GET recipients. User: {User}", User.Identity?.Name);
        return Ok(await notificationService.GetRecipientsAsync());
    }

    [HttpPost("recipients")]
    public async Task<IActionResult> AddRecipient([FromBody] TelegramRecipientDto dto)
    {
        Log.Information("[NotificationsAPI] POST recipient {Name}. User: {User}", dto.Name, User.Identity?.Name);
        await notificationService.AddRecipientAsync(dto);
        return Ok();
    }

    [HttpDelete("recipients/{id}")]
    public async Task<IActionResult> DeleteRecipient(int id)
    {
        Log.Information("[NotificationsAPI] DELETE recipient {Id}. User: {User}", id, User.Identity?.Name);
        await notificationService.DeleteRecipientAsync(id);
        return NoContent();
    }
}
