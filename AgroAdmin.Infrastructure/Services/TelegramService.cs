using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.Infrastructure.Services;

public class TelegramService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TelegramService> logger)
    : ITelegramService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string _botToken = configuration["Telegram:BotToken"] ?? throw new InvalidOperationException("Telegram:BotToken not configured");
    private readonly string _chatId = configuration["Telegram:ChatId"] ?? throw new InvalidOperationException("Telegram:ChatId not configured");

    public async Task SendMessageAsync(string message)
    {
        try
        {
            logger.LogInformation("Sending Telegram message to chats: {ChatIds}", _chatId);

            var chatIds = _chatId.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var successCount = 0;

            foreach (var chatId in chatIds)
            {
                var trimmedChatId = chatId.Trim();
                try
                {
                    var url = $"https://api.telegram.org/bot{_botToken}/sendMessage?chat_id={trimmedChatId}&text={Uri.EscapeDataString(message)}&parse_mode=HTML";
                    var response = await _httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                        logger.LogDebug("Message sent successfully to chat {ChatId}", trimmedChatId);
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        logger.LogWarning("Telegram send failed for chat {ChatId}. Status: {StatusCode}, Error: {Error}",
                            trimmedChatId, response.StatusCode, error);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Exception while sending to chat {ChatId}", trimmedChatId);
                }
            }

            logger.LogInformation("Telegram messages sent. Success: {SuccessCount}/{TotalCount}",
                successCount, chatIds.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Critical error in SendMessageAsync");
        }
    }

    public async Task SendBookingNotificationAsync(BookingDto booking)
    {
        var message = FormatBookingMessage(booking);
        await SendMessageAsync(message);
    }

    private string FormatBookingMessage(BookingDto booking)
    {
        var unitName = booking.ReservedUnit.ToFriendlyString();

        var unitEmoji = booking.ReservedUnit switch
        {
            ReservedUnits.PondSide => "🌊",
            ReservedUnits.ParkingSide => "🚗",
            ReservedUnits.WholeHouse => "🏠",
            _ => "🏢"
        };

        return $"""
                <b>Новое бронирование!</b>

                👤 Гость: {booking.Guest?.FullName}
                📞 Телефон: {booking.Guest?.Phone}
                📅 Даты: {booking.ArrivalDate:dd.MM.yyyy} — {booking.DepartureDate:dd.MM.yyyy}
                🏠 Объект: {unitEmoji} {unitName}
                👥 Гостей: {booking.TotalGuestsCount}
                🐕 Собака: {(booking.HasDog ? "✅" : "❌")}
                🌡️ Баня: {(booking.NeedsSauna ? "✅" : "❌")}
                🥂 Зал: {(booking.NeedsBanquetHall ? "✅" : "❌")}
                """;
    }
}