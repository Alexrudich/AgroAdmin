using System.Net.Http.Json;
using AgroAdmin.Shared.Dto.Notifications;

namespace AgroAdmin.Client.Services;

public class NotificationApiClient(HttpClient http)
{
    public async Task<List<ScheduledReminderDto>> GetActiveRemindersAsync()
    {
        return await http.GetFromJsonAsync<List<ScheduledReminderDto>>("api/notifications/reminders")
               ?? new();
    }

    public async Task CreateReminderAsync(CreateReminderDto dto)
    {
        var response = await http.PostAsJsonAsync("api/notifications/reminders", dto);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateReminderAsync(int id, CreateReminderDto dto)
    {
        await http.PutAsJsonAsync($"api/notifications/reminders/{id}", dto);
    }

    public async Task DeleteReminderAsync(int id)
    {
        await http.DeleteAsync($"api/notifications/reminders/{id}");
    }
}