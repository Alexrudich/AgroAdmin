using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Dto.Telegram.Responses;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _defaultChatId;
    private CancellationTokenSource? _receivingCts;

    public TelegramService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;

        var botToken = configuration["Telegram:BotToken"]
                       ?? throw new InvalidOperationException("Telegram:BotToken not configured");
        _defaultChatId = configuration["Telegram:ChatId"]
                         ?? throw new InvalidOperationException("Telegram:ChatId not configured");

        _botClient = new TelegramBotClient(botToken);
    }

    // Существующий метод отправки сообщений (оставляем как есть)
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
        var message = FormatBookingMessage(booking);
        await SendMessageAsync(message);
    }

    // НОВЫЙ МЕТОД: Запуск получения команд
    public async Task StartReceivingAsync(CancellationToken cancellationToken)
    {
        _receivingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[]
            {
                UpdateType.Message,
                UpdateType.CallbackQuery
            },
            ThrowPendingUpdates = true
        };

        _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            _receivingCts.Token
        );

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

    // Обработка входящих обновлений
    private async Task HandleUpdateAsync(ITelegramBotClient botClient, TelegramBotUpdate update, CancellationToken ct)
    {
        try
        {
            if (update.Message?.Text is { } messageText)
            {
                var chatId = update.Message.Chat.Id;

                // Проверяем авторизацию
                if (!await IsAuthorizedAsync(chatId, ct))
                {
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "⛔ У вас нет доступа к этому боту. Обратитесь к администратору.",
                        cancellationToken: ct);
                    return;
                }

                // Проверяем rate limit
                if (!await CheckRateLimitAsync(chatId, ct))
                {
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "⚠️ Слишком много запросов. Подождите немного.",
                        cancellationToken: ct);
                    return;
                }

                // Обрабатываем команду
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

    // Проверка авторизации
    private async Task<bool> IsAuthorizedAsync(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipient = await dbContext.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString(), ct);

        if (recipient == null) return false;

        // Обновляем статистику активности
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.UpdateRecipientStatsAsync(chatId, false);

        return recipient.IsActive;
    }

    // Проверка роли для команд
    private async Task<bool> HasPermissionAsync(long chatId, string requiredRole, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipient = await dbContext.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString(), ct);

        if (recipient == null) return false;

        return recipient.IsActive && recipient.Role switch
        {
            "Admin" => true,
            "Manager" => requiredRole != "Admin",
            "Viewer" => requiredRole == "Viewer",
            _ => false
        };
    }

    // Rate limiting
    private async Task<bool> CheckRateLimitAsync(long chatId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var recipient = await dbContext.TelegramRecipients
            .FirstOrDefaultAsync(r => r.ChatId == chatId.ToString(), ct);

        if (recipient == null) return false;

        var today = DateTime.UtcNow.Date;

        // Сбрасываем счетчик если новый день
        if (recipient.LastCommandAt?.Date != today)
        {
            recipient.CommandCountToday = 0;
        }

        // Лимит: 30 команд в день
        if (recipient.CommandCountToday >= 30)
        {
            return false;
        }

        recipient.CommandCountToday++;
        recipient.LastCommandAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        return true;
    }

    // Обработка команд (продолжение следует)
    private async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var command = message.Text?.Split(' ')[0].ToLower();

        _logger.LogInformation("Received command {Command} from {ChatId}", command, chatId);

        using var scope = _scopeFactory.CreateScope();

        switch (command)
        {
            case "/start":
                await SendWelcomeMessageAsync(botClient, chatId, ct);
                break;

            case "/help":
                await SendHelpMessageAsync(botClient, chatId, ct);
                break;

            case "/nearestbookings":
                await ShowNearestBookingsAsync(scope, botClient, chatId, ct);
                break;

            case "/checkfreeslots":
                await CheckFreeSlotsAsync(scope, botClient, message, ct);
                break;

            case "/createfullbackup":
                await CreateFullBackupAsync(scope, botClient, chatId, ct);
                break;

            case "/bookingsummary":
                await ShowBookingSummaryAsync(scope, botClient, chatId, ct);
                break;

            case "/checkcapacity":
                await CheckCapacityAsync(scope, botClient, chatId, ct);
                break;

            default:
                await botClient.SendTextMessageAsync(
                    chatId,
                    "❓ Неизвестная команда. Используйте /help для списка команд.",
                    cancellationToken: ct);
                break;
        }
    }

    // Вспомогательные методы (пока заглушки)
    private async Task SendWelcomeMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        var welcomeMessage =
            "🌿 *Добро пожаловать в AgroAdmin Bot!*\n\n" +
            "Я помогу вам управлять усадьбой прямо из Telegram.\n\n" +
            "*Доступные команды:*\n" +
            "/help - показать список команд\n" +
            "/nearestBookings - ближайшие бронирования\n" +
            "/checkFreeSlots [дата_начала] [дата_конца] - свободные даты\n" +
            "/createFullBackup - создать полный бэкап\n" +
            "/bookingSummary - сводка за текущий месяц\n" +
            "/checkCapacity - отчет по загрузке\n\n" +
            "🔔 Вы будете получать уведомления о новых бронированиях и бэкапах.";

        await botClient.SendTextMessageAsync(
            chatId,
            welcomeMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    private async Task SendHelpMessageAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct)
    {
        var helpMessage =
            "📖 *Справка по командам*\n\n" +
            "*/nearestBookings* - показать бронирования на ближайшие 7 дней\n" +
            "*/checkFreeSlots 2026-04-01 2026-04-30* - проверить свободные даты\n" +
            "*/createFullBackup* - создать полный бэкап всех данных\n" +
            "*/bookingSummary* - сводка по бронированиям за текущий месяц\n" +
            "*/checkCapacity* - график загрузки на неделю\n" +
            "*/help* - показать эту справку";

        await botClient.SendTextMessageAsync(
            chatId,
            helpMessage,
            parseMode: ParseMode.Markdown,
            cancellationToken: ct);
    }

    // Обработчики команд (пока заглушки, реализуем позже)
    private async Task ShowNearestBookingsAsync(IServiceScope scope, ITelegramBotClient botClient, long chatId,
        CancellationToken ct)
    {
        try
        {
            // Получаем параметр days из команды
            // Нужно передать message, чтобы достать текст команды
            // Пока используем chatId, но message нам нужен для парсинга

            var days = 7; // значение по умолчанию

            // Вызываем API
            var httpClient = _httpClientFactory.CreateClient();
            var apiUrl = _configuration["ApiUrl"] ?? "http://localhost:8080";

            var response = await httpClient.GetFromJsonAsync<List<TelegramBookingDto>>(
                $"{apiUrl}/api/bookings/nearest?days={days}", ct);

            if (response == null || response.Count == 0)
            {
                await botClient.SendTextMessageAsync(
                    chatId,
                    $"📭 *Нет бронирований на ближайшие {days} дней.*\n\n" +
                    "Используйте /checkFreeSlots чтобы посмотреть свободные даты.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct);
                return;
            }

            var message = $"📅 *Ближайшие бронирования ({days} дней):*\n\n";

            foreach (var booking in response)
            {
                var nights = (booking.DepartureDate - booking.ArrivalDate).Days;
                var nightsText = nights switch
                {
                    1 => "ночь",
                    <= 4 => "ночи",
                    _ => "ночей"
                };

                message += $"*{booking.UnitEmoji} {booking.GuestName}*\n";
                message += $"   📅 {booking.ArrivalDate:dd.MM} — {booking.DepartureDate:dd.MM} ({nights} {nightsText})\n";
                message += $"   👥 {booking.TotalGuestsCount} чел.";

                if (booking.NeedsSauna)
                    message += " 🌡️";

                if (booking.TotalPrice.HasValue && booking.TotalPrice.Value > 0)
                    message += $"\n   💰 {booking.TotalPrice.Value:N0} BYN";

                if (!string.IsNullOrEmpty(booking.Phone))
                    message += $"\n   📞 {booking.Phone}";

                message += "\n\n";
            }

            message += $"📊 *Итого:* {response.Count} бронирований";

            await botClient.SendTextMessageAsync(
                chatId,
                message,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ShowNearestBookingsAsync");
            await botClient.SendTextMessageAsync(
                chatId,
                "❌ Ошибка при получении списка бронирований. Попробуйте позже.",
                cancellationToken: ct);
        }
    }

    private async Task CheckFreeSlotsAsync(IServiceScope scope, ITelegramBotClient botClient, Message message,
     CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var parts = message.Text?.Split(' ');

        // Если есть параметры — обрабатываем как ручной ввод
        if (parts?.Length >= 2)
        {
            await ProcessDateRangeInput(botClient, message, ct);
            return;
        }

        // Иначе показываем меню выбора периода
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("📅 Текущий месяц", "period_current_month"),
            InlineKeyboardButton.WithCallbackData("📆 Следующий месяц", "period_next_month")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("📊 Следующие 2 недели", "period_two_weeks"),
            InlineKeyboardButton.WithCallbackData("✏️ Ввести даты вручную", "period_manual")
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

    private async Task ProcessDateRangeInput(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var chatId = message.Chat.Id;
        var parts = message.Text?.Split(' ');

        if (parts?.Length >= 2 && DateTime.TryParse(parts[1], out var startDate))
        {
            DateTime endDate;
            if (parts.Length >= 3 && DateTime.TryParse(parts[2], out endDate))
            {
                if (endDate < startDate) (startDate, endDate) = (endDate, startDate);
            }
            else
            {
                endDate = startDate.AddDays(30);
            }

            if ((endDate - startDate).Days > 90)
            {
                endDate = startDate.AddDays(90);
            }

            // Вызываем существующую логику с датами
            await ShowAvailability(botClient, chatId, startDate, endDate, ct);
            return;
        }

        await botClient.SendTextMessageAsync(
            chatId,
            "❌ Неверный формат дат.\n\n" +
            "Используйте:\n" +
            "/checkFreeSlots 2026-04-01\n" +
            "/checkFreeSlots 2026-04-01 2026-04-30",
            cancellationToken: ct);
    }

    private async Task ShowAvailability(ITelegramBotClient botClient, long chatId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        try
        {
            // Вызываем API
            var httpClient = _httpClientFactory.CreateClient();
            var apiUrl = _configuration["ApiUrl"] ?? "http://localhost:8080";

            var availability = await httpClient.GetFromJsonAsync<List<DailyAvailability>>(
                $"{apiUrl}/api/bookings/availability?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}", ct);

            if (availability == null || !availability.Any())
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Не удалось получить данные о загрузке", cancellationToken: ct);
                return;
            }

            // Форматируем результат в стиле "Вариант 4"
            var message = FormatAvailabilitySummary(availability, startDate, endDate);

            await botClient.SendTextMessageAsync(chatId, message, parseMode: ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ShowAvailability");
            await botClient.SendTextMessageAsync(chatId, "❌ Ошибка при получении данных", cancellationToken: ct);
        }
    }

    private async Task CreateFullBackupAsync(IServiceScope scope, ITelegramBotClient botClient, long chatId,
        CancellationToken ct)
    {
        await botClient.SendTextMessageAsync(chatId, "🔄 Функция в разработке...", cancellationToken: ct);
    }

    private async Task ShowBookingSummaryAsync(IServiceScope scope, ITelegramBotClient botClient, long chatId,
        CancellationToken ct)
    {
        await botClient.SendTextMessageAsync(chatId, "🔄 Функция в разработке...", cancellationToken: ct);
    }

    private async Task CheckCapacityAsync(IServiceScope scope, ITelegramBotClient botClient, long chatId,
        CancellationToken ct)
    {
        await botClient.SendTextMessageAsync(chatId, "🔄 Функция в разработке...", cancellationToken: ct);
    }

    // В HandleCallbackQueryAsync
    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken ct)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;

        if (data?.StartsWith("period_") == true)
        {
            var today = DateTime.Today;
            DateTime startDate;
            DateTime endDate;

            switch (data)
            {
                case "period_current_month":
                    startDate = today;
                    endDate = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    break;

                case "period_next_month":
                    var nextMonth = today.AddMonths(1);
                    startDate = new DateTime(nextMonth.Year, nextMonth.Month, 1);
                    endDate = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    break;

                case "period_two_weeks":
                    startDate = today;
                    endDate = today.AddDays(14);
                    break;

                case "period_manual":
                    await botClient.SendTextMessageAsync(
                        chatId,
                        "✏️ *Введите даты вручную*\n\n" +
                        "Формат: `/checkFreeSlots 2026-04-01 2026-04-30`\n\n" +
                        "Примеры:\n" +
                        "`/checkFreeSlots 2026-04-01` — покажет месяц\n" +
                        "`/checkFreeSlots 2026-04-01 2026-05-15` — покажет указанный период",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct);
                    await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);
                    return;

                default:
                    return;
            }

            await ShowAvailability(botClient, chatId, startDate, endDate, ct);
            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: ct);

            // Удаляем сообщение с кнопками
            try
            {
                await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken: ct);
            }
            catch { }
        }
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

        var checkInTime = booking.CheckInTime != TimeSpan.Zero
            ? booking.CheckInTime.ToString(@"hh\:mm")
            : "14:00";

        var costLine = string.Empty;
        if (booking.AccommodationCost.HasValue && booking.AccommodationCost.Value > 0)
        {
            costLine = $"\n💰 Стоимость: {booking.AccommodationCost.Value:N0} BYN";
        }

        return $"""
                <b>Новое бронирование!</b>

                👤 Гость: {booking.Guest?.FullName}
                📞 Телефон: {booking.Guest?.Phone}
                📅 Даты: {booking.ArrivalDate:dd.MM.yyyy} — {booking.DepartureDate:dd.MM.yyyy}
                ⏰ Заезд: {checkInTime}
                🏠 Объект: {unitEmoji} {unitName}
                👥 Гостей: {booking.TotalGuestsCount} (взр: {booking.AdultsCount}, дети: {booking.ChildrenCount}, мл: {booking.InfantsCount})
                🐕 Собака: {(booking.HasDog ? "✅" : "❌")}
                🌡️ Баня: {(booking.NeedsSauna ? "✅" : "❌")}
                🥂 Зал: {(booking.NeedsBanquetHall ? "✅" : "❌")}
                {costLine}
                """;
    }
    private string FormatAvailabilitySummary(List<DailyAvailability> availability, DateTime startDate, DateTime endDate)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🏠 *СВОДКА ЗАГРУЗКИ*");
        sb.AppendLine($"📅 {startDate:dd.MM.yyyy} — {endDate:dd.MM.yyyy}\n");

        // 1. Полностью свободные даты (все 3 объекта)
        var fullyFreeDates = availability.Where(d => d.IsFullyFree).Select(d => d.Date).ToList();
        sb.AppendLine(FormatDateRanges("🟢 *Полностью свободно* (все 3 объекта)", fullyFreeDates));

        // 2. Частично свободные даты
        var partiallyFree = availability.Where(d => d.IsPartiallyFree).ToList();
        if (partiallyFree.Any())
        {
            sb.AppendLine($"\n🟡 *Частично занято* (свободен 1-2 объекта):");

            // Группируем по типу занятости
            var onlyPondFree = partiallyFree.Where(d => d.IsPondSideFree && !d.IsParkingSideFree).Select(d => d.Date).ToList();
            var onlyParkingFree = partiallyFree.Where(d => !d.IsPondSideFree && d.IsParkingSideFree).Select(d => d.Date).ToList();
            var bothFreeButWholeOccupied = partiallyFree.Where(d => d.IsPondSideFree && d.IsParkingSideFree && !d.IsWholeHouseFree).Select(d => d.Date).ToList();

            if (onlyPondFree.Any())
                sb.AppendLine(FormatDateRanges("   🌊 Только PondSide", onlyPondFree));

            if (onlyParkingFree.Any())
                sb.AppendLine(FormatDateRanges("   🚗 Только ParkingSide", onlyParkingFree));

            if (bothFreeButWholeOccupied.Any())
                sb.AppendLine(FormatDateRanges("   🏠 Занят WholeHouse, свободны обе половинки", bothFreeButWholeOccupied));
        }

        // 3. Полностью занятые даты
        var fullyOccupied = availability.Where(d => d.IsFullyOccupied).Select(d => d.Date).ToList();
        if (fullyOccupied.Any())
        {
            sb.AppendLine($"\n🔴 *Полностью занято* (все 3 объекта):");
            sb.AppendLine(FormatDateRanges("   ", fullyOccupied));
        }

        // 4. Статистика
        var totalDays = availability.Count;
        var freeDays = fullyFreeDates.Count;
        var freePercent = totalDays > 0 ? (freeDays * 100.0 / totalDays) : 0;

        sb.AppendLine($"\n📊 *Статистика:*");
        sb.AppendLine($"   • Всего дней: {totalDays}");
        sb.AppendLine($"   • Полностью свободных: {freeDays} ({freePercent:F0}%)");
        sb.AppendLine($"   • Частично занятых: {partiallyFree.Count}");
        sb.AppendLine($"   • Полностью занятых: {fullyOccupied.Count}");

        return sb.ToString();
    }

    private string FormatDateRanges(string title, List<DateTime> dates)
    {
        if (!dates.Any()) return "";

        var ranges = new List<string>();
        var sorted = dates.OrderBy(d => d).ToList();

        DateTime? rangeStart = null;
        DateTime? prevDate = null;

        foreach (var date in sorted)
        {
            if (rangeStart == null)
            {
                rangeStart = date;
            }
            else if (prevDate.HasValue && (date - prevDate.Value).Days > 1)
            {
                // Разрыв — закрываем текущий диапазон
                ranges.Add(FormatSingleRange(rangeStart.Value, prevDate.Value));
                rangeStart = date;
            }

            prevDate = date;
        }

        // Добавляем последний диапазон
        if (rangeStart.HasValue && prevDate.HasValue)
        {
            ranges.Add(FormatSingleRange(rangeStart.Value, prevDate.Value));
        }

        if (string.IsNullOrEmpty(title))
            return string.Join(", ", ranges);

        return $"{title}: {string.Join(", ", ranges)}";
    }

    private string FormatSingleRange(DateTime start, DateTime end)
    {
        if (start == end)
            return $"{start:dd.MM}";

        return $"{start:dd.MM}—{end:dd.MM}";
    }
}