using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Constants;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Services;
using AgroAdmin.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotUpdate = Telegram.Bot.Types.Update;

namespace AgroAdmin.Infrastructure.Services;

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramApiClient _apiClient;
    private readonly string _defaultChatId;
    private CancellationTokenSource? _receivingCts;

    public TelegramService(
        IConfiguration configuration,
        ILogger<TelegramService> logger,
        IServiceScopeFactory scopeFactory,
        ITelegramApiClient apiClient)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _apiClient = apiClient;

        var botToken = configuration["Telegram:BotToken"]
                       ?? throw new InvalidOperationException("Telegram:BotToken not configured");
        _defaultChatId = configuration["Telegram:ChatId"]
                         ?? throw new InvalidOperationException("Telegram:ChatId not configured");

        _botClient = new TelegramBotClient(botToken);
    }

    public async Task SendMessageAsync(string message, string? targetChatId = null)
    {
        try
        {
            var idsToProcess = targetChatId ?? _defaultChatId;
            _logger.LogInformation("Sending Telegram message to chats: {ChatIds}", idsToProcess);

            var chatIds = idsToProcess.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var successCount = 0;

            foreach (var chatId in chatIds)
            {
                var trimmedChatId = chatId.Trim();
                try
                {
                    await _botClient.SendTextMessageAsync(
                        chatId: trimmedChatId,
                        text: message,
                        parseMode: ParseMode.Html);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send to chat {ChatId}", trimmedChatId);
                }
            }

            _logger.LogInformation("Telegram sent. Success: {SuccessCount}/{TotalCount}",
                successCount, chatIds.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in SendMessageAsync");
        }
    }

    public async Task SendBookingNotificationAsync(BookingDto booking)
    {
        var message = TelegramMessageFormatter.FormatBookingMessage(booking);
        await SendMessageAsync(message);
    }

    public async Task StartReceivingAsync(CancellationToken cancellationToken)
    {
        _receivingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
            ThrowPendingUpdates = true
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            _receivingCts.Token);

        _logger.LogInformation("Telegram bot started receiving updates");
        await Task.CompletedTask;
    }

    public async Task StopReceivingAsync()
    {
        if (_receivingCts != null)
        {
            _receivingCts.Cancel();
            _receivingCts.Dispose();
            _logger.LogInformation("Telegram bot stopped receiving updates");
        }
        await Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, TelegramBotUpdate update, CancellationToken ct)
    {
        try
        {
            if (update.Message?.Text is { } messageText)
            {
                var chatId = update.Message.Chat.Id;

                if (!await IsAuthorizedAsync(chatId, ct))
                {
                    await botClient.SendTextMessageAsync(chatId, "⛔ У вас нет доступа к этому боту.", cancellationToken: ct);
                    return;
                }

                if (!await CheckRateLimitAsync(chatId, ct))
                {
                    await botClient.SendTextMessageAsync(chatId, "⚠️ Слишком много запросов. Подождите немного.", cancellationToken: ct);
                    return;
                }

                if (messageText.StartsWith('/'))
                {
                    await HandleCommandAsync(botClient, update.Message, ct);
                }
            }
            else if (update.CallbackQuery is { } callbackQuery)
            {
                await HandleCallbackQueryAsync(botClient, callbackQuery, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: {apiEx.ErrorCode} - {apiEx.Message}",
            _ => exception.Message
        };
        _logger.LogError(exception, "Telegram bot error: {Error}", errorMessage);
        return Task.CompletedTask;
    }

    private async Task<bool> IsAuthorizedAsync(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipient = await dbContext.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString(), ct);

        if (recipient == null) return false;

        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.UpdateRecipientStatsAsync(chatId, false);

        return recipient.IsActive;
    }

    private async Task<bool> CheckRateLimitAsync(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipient = await dbContext.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString(), ct);

        if (recipient == null) return false;

        var today = DateTime.UtcNow.Date;
        if (recipient.LastCommandAt?.Date != today)
        {
            recipient.CommandCountToday = 0;
        }

        if (recipient.CommandCountToday >= 30) return false;

        recipient.CommandCountToday++;
        recipient.LastCommandAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return true;
    }

    private async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var command = message.Text?.Split(' ')[0].ToLower();

        _logger.LogInformation("Received command {Command} from {ChatId}", command, chatId);

        using var scope = _scopeFactory.CreateScope();

        switch (command)
        {
            case "/start":
                await botClient.SendTextMessageAsync(chatId, TelegramMessages.Welcome, parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case "/help":
                await botClient.SendTextMessageAsync(chatId, TelegramMessages.Help, parseMode: ParseMode.Markdown, cancellationToken: ct);
                break;

            case "/nearestbookings":
                await ShowNearestBookingsAsync(botClient, chatId, message, ct);
                break;

            case "/checkfreeslots":
                await CheckFreeSlotsAsync(botClient, message, ct);
                break;

            case "/createfullbackup":
                await CreateFullBackupAsync(botClient, chatId, ct);
                break;

            case "/bookingsummary":
                await ShowBookingSummaryAsync(botClient, chatId, ct);
                break;
            case "/health":
                await ShowHealthAsync(botClient, chatId, ct);
                break;

            default:
                await botClient.SendTextMessageAsync(chatId, "❓ Неизвестная команда. Используйте /help.", cancellationToken: ct);
                break;
        }
    }

    private async Task ShowNearestBookingsAsync(ITelegramBotClient botClient, long chatId, Message message, CancellationToken ct)
    {
        try
        {
            var parts = message.Text?.Split(' ');
            var days = 7;
            if (parts?.Length > 1 && int.TryParse(parts[1], out var parsedDays))
            {
                days = Math.Clamp(parsedDays, 1, 30);
            }

            var bookings = await _apiClient.GetNearestBookingsAsync(days);
            var response = TelegramMessageFormatter.FormatNearestBookings(bookings, days);
            await botClient.SendTextMessageAsync(chatId, response, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ShowNearestBookingsAsync");
            await botClient.SendTextMessageAsync(chatId, "❌ Ошибка при получении списка бронирований.", cancellationToken: ct);
        }
    }

    private async Task CheckFreeSlotsAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        // Если есть параметры — показываем предупреждение, что теперь только кнопки
        var parts = message.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts?.Length >= 2)
        {
            await botClient.SendTextMessageAsync(
                chatId,
                "📱 *Теперь выбор периода осуществляется через кнопки*\n\n" +
                "Нажмите на одну из кнопок ниже, чтобы проверить свободные даты.",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
            return;
        }

        // Меню с кнопками
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Текущий месяц", "period_current_month"),
                InlineKeyboardButton.WithCallbackData("📆 Следующий месяц", "period_next_month")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 2 недели", "period_two_weeks"),
                InlineKeyboardButton.WithCallbackData("📅 30 дней", "period_30_days"),
                InlineKeyboardButton.WithCallbackData("📆 90 дней", "period_90_days")
            }
        });

        await botClient.SendTextMessageAsync(
            chatId,
            "🏠 *Выберите период для проверки свободных дат:*\n\n" +
            "Будут показаны дни, когда полностью свободны все объекты.",
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: ct);
    }

    private async Task ShowAvailability(ITelegramBotClient botClient, long chatId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        try
        {
            var availability = await _apiClient.GetAvailabilityAsync(startDate, endDate);
            if (!availability.Any())
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Не удалось получить данные о загрузке", cancellationToken: ct);
                return;
            }

            var message = TelegramMessageFormatter.FormatAvailabilitySummary(availability, startDate, endDate);
            await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ShowAvailability");
            await botClient.SendTextMessageAsync(chatId, "❌ Ошибка при получении данных", cancellationToken: ct);
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
    {
        if (callbackQuery.Message == null)
        {
            _logger.LogWarning("Callback query without message");
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ошибка", cancellationToken: ct);
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;

        if (data?.StartsWith("period_") == true)
        {
            await HandlePeriodCallbackAsync(botClient, callbackQuery, chatId, data, ct);
        }
        else if (data?.StartsWith("summary_") == true)
        {
            await HandleSummaryCallbackAsync(botClient, callbackQuery, chatId, data, ct);
        }
    }

    private async Task HandlePeriodCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId, string data, CancellationToken ct)
    {
        var (startDate, endDate) = TelegramDateHelper.GetPeriodDates(data);

        if (startDate == default || endDate == default)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ошибка выбора периода", cancellationToken: ct);
            return;
        }

        await ShowAvailability(botClient, chatId, startDate, endDate, ct);
        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);

        try
        {
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message!.MessageId, cancellationToken: ct);
        }
        catch { }
    }

    private async Task HandleSummaryCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, long chatId, string data, CancellationToken ct)
    {
        var (startDate, endDate) = TelegramDateHelper.GetSummaryDates(data);

        if (startDate == default || endDate == default)
        {
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Ошибка выбора периода", cancellationToken: ct);
            return;
        }

        // Создаем scope для доступа к Scoped сервису
        using var scope = _scopeFactory.CreateScope();
        var bookingTelegramService = scope.ServiceProvider.GetRequiredService<IBookingTelegramService>();
        var summary = await bookingTelegramService.GetBookingSummaryAsync(startDate, endDate);
        var message = TelegramMessageFormatter.FormatBookingSummary(summary);

        await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: ct);
        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);

        try
        {
            await botClient.DeleteMessageAsync(chatId, callbackQuery.Message!.MessageId, cancellationToken: ct);
        }
        catch { }
    }

    private async Task ShowBookingSummaryAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        // Показываем меню с кнопками для выбора периода
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Текущий месяц", "summary_current_month")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("◀️ Предыдущий месяц", "summary_prev_month"),
                InlineKeyboardButton.WithCallbackData("Следующий месяц ▶️", "summary_next_month")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("7 дней", "summary_7_days"),
                InlineKeyboardButton.WithCallbackData("30 дней", "summary_30_days"),
                InlineKeyboardButton.WithCallbackData("90 дней", "summary_90_days")
            }
        });

        await botClient.SendTextMessageAsync(
            chatId,
            "📊 *Выберите период для сводки по бронированиям:*",
            parseMode: ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: ct);
    }

    private async Task CreateFullBackupAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        var statusMessage = await botClient.SendTextMessageAsync(
            chatId,
            "🔄 *Создание бэкапа...*\nЭто может занять несколько минут.",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();

            var result = await backupService.CreateManualBackupAsync();

            if (result.Success)
            {
                var message = result.Message;

                if (!string.IsNullOrEmpty(result.FileLink))
                {
                    message += $"\n\n🔗 [Скачать]({result.FileLink})";
                }

                await botClient.EditMessageTextAsync(
                    chatId,
                    statusMessage.MessageId,
                    message,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
            }
            else
            {
                await botClient.EditMessageTextAsync(
                    chatId,
                    statusMessage.MessageId,
                    $"❌ *{result.Message}*",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateFullBackupAsync");
            await botClient.EditMessageTextAsync(
                chatId,
                statusMessage.MessageId,
                "❌ Критическая ошибка при создании бэкапа. Проверьте логи.",
                cancellationToken: ct);
        }
    }

    private async Task ShowHealthAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var healthService = scope.ServiceProvider.GetRequiredService<IHealthService>();

            var message = await healthService.FormatForTelegramAsync();
            await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ShowHealthAsync");
            await botClient.SendTextMessageAsync(chatId, "❌ Ошибка при проверке здоровья системы", cancellationToken: ct);
        }
    }
}