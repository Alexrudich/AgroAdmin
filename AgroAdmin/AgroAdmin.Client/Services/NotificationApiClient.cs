using System.Net.Http.Json;
using AgroAdmin.Shared.Dto.Notifications;

namespace AgroAdmin.Client.Services;

public class NotificationApiClient(HttpClient http)
{
    public async Task CreateReminderAsync(CreateReminderDto dto)
    {
        var response = await http.PostAsJsonAsync("api/notifications/reminders", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ScheduledReminderDto>> GetActiveRemindersAsync()
    {
        return await http.GetFromJsonAsync<List<ScheduledReminderDto>>("api/notifications/reminders")
               ?? new();
    }
}