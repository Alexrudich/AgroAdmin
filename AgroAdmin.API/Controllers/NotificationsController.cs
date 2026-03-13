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

    [HttpPut("reminders/{id}")]
    public async Task<IActionResult> UpdateReminder(int id, [FromBody] CreateReminderDto dto)
    {
        await notificationService.UpdateReminderAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("reminders/{id}")]
    public async Task<IActionResult> DeleteReminder(int id)
    {
        var reminder = await notificationService.GetByIdAsync(id);
        if (reminder == null) return NotFound();

        await notificationService.DeleteReminderAsync(id);
        return NoContent();
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> GetRecipients() => Ok(await notificationService.GetRecipientsAsync());

    [HttpPost("recipients")]
    public async Task<IActionResult> AddRecipient([FromBody] TelegramRecipientDto dto)
    {
        await notificationService.AddRecipientAsync(dto);
        return Ok();
    }

    [HttpDelete("recipients/{id}")]
    public async Task<IActionResult> DeleteRecipient(int id)
    {
        await notificationService.DeleteRecipientAsync(id);
        return NoContent();
    }
}