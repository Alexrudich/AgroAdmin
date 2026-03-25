using AgroAdmin.Shared.Dto.Telegram;
using AgroAdmin.Shared.Dto.Telegram.Responses;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface IBookingTelegramService
{
    /// <summary>
    /// Получить ближайшие бронирования на указанное количество дней
    /// </summary>
    /// <param name="days">Количество дней вперед (по умолчанию 7, максимум 30)</param>
    /// <returns>Список бронирований для Telegram</returns>
    Task<List<TelegramBookingDto>> GetNearestBookingsAsync(int days = 7);

    /// <summary>
    /// Получить свободные слоты для заданного периода
    /// </summary>
    /// <param name="startDate">Начальная дата</param>
    /// <param name="endDate">Конечная дата</param>
    /// <returns>Список свободных периодов</returns>
    Task<List<FreeSlotDto>> GetFreeSlotsAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Получить сводку по бронированиям за месяц
    /// </summary>
    /// <param name="year">Год</param>
    /// <param name="month">Месяц (1-12)</param>
    /// <returns>Сводка по бронированиям</returns>
    Task<BookingSummaryDto> GetBookingSummaryAsync(int year, int month);
}