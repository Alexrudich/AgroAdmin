using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace AgroAdmin.API.Services;

public class TelegramService(IConfiguration config, HttpClient httpClient)
{
    public async Task SendNotification(string message)
    {
        var token = config["Telegram:Token"]?.Trim();
        var chatIdsRaw = config["Telegram:ChatId"]?.Trim(); // Получаем строку "ID1,ID2"

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatIdsRaw)) return;

        // Разделяем строку на массив ID
        var chatIds = chatIdsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var builder = new UriBuilder("https", "api.telegram.org")
        {
            Path = $"bot{token}/sendMessage"
        };

        foreach (var id in chatIds)
        {
            try
            {
                var payload = new
                {
                    chat_id = id.Trim(),
                    text = message,
                    parse_mode = "Markdown"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Отправляем конкретному пользователю
                var response = await httpClient.PostAsync(builder.Uri, content);

                // Лог для отладки
                var body = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Sent to {id}: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error sending to {id}: {ex.Message}");
            }
        }
    }
}