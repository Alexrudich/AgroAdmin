using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto;
using AgroAdmin.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController(AppDbContext context, ITelegramService telegramService, ILogger<BookingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAll()
    {
        var bookings = await context.Bookings
            .Include(b => b.Guest)
            .Include(b => b.SaunaOrders)
            .OrderByDescending(b => b.ArrivalDate)
            .Select(b => new BookingDto
            {
                Id = b.Id,
                Guest = new GuestDto
                {
                    Id = b.Guest.Id,
                    FullName = b.Guest.FullName,
                    Phone = b.Guest.Phone,
                    CreatedAt = b.Guest.CreatedAt,
                    Comment = b.Guest.Comment
                },
                ArrivalDate = b.ArrivalDate,
                DepartureDate = b.DepartureDate,
                ReservedUnit = b.ReservedUnit,
                TotalGuestsCount = b.TotalGuestsCount,
                AdultsCount = b.AdultsCount,
                ChildrenCount = b.ChildrenCount,
                InfantsCount = b.InfantsCount,
                HasDog = b.HasDog,
                NeedsSauna = b.NeedsSauna,
                NeedsBanquetHall = b.NeedsBanquetHall,
                IsFirstTimeGuest = b.IsFirstTimeGuest,
                AdminNotes = b.AdminNotes,
                FeedbackComment = b.FeedbackComment
            })
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetById(int id)
    {
        // Include Guest!
        var b = await context.Bookings
            .Include(b => b.Guest)  // Подгружаем гостя
            .FirstOrDefaultAsync(b => b.Id == id);

        if (b == null) return NotFound();

        var dto = new BookingDto
        {
            Id = b.Id,
            Guest = new GuestDto
            {
                Id = b.Guest.Id,
                FullName = b.Guest.FullName,
                Phone = b.Guest.Phone,
                CreatedAt = b.Guest.CreatedAt,
                Comment = b.Guest.Comment
            },
            ArrivalDate = b.ArrivalDate,
            DepartureDate = b.DepartureDate,
            ReservedUnit = b.ReservedUnit,
            TotalGuestsCount = b.TotalGuestsCount,
            AdultsCount = b.AdultsCount,
            ChildrenCount = b.ChildrenCount,
            InfantsCount = b.InfantsCount,
            HasDog = b.HasDog,
            NeedsSauna = b.NeedsSauna,
            NeedsBanquetHall = b.NeedsBanquetHall,
            IsFirstTimeGuest = b.IsFirstTimeGuest,
            AdminNotes = b.AdminNotes,
            FeedbackComment = b.FeedbackComment
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateBookingDto dto)
    {
        try
        {
            // 1. Ищем или создаем гостя
            Guest? guest = null;
            int guestId;

            if (dto.Guest?.Id > 0)
            {
                guest = await context.Guests.FindAsync(dto.Guest.Id);
            }

            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.Phone))
            {
                guest = await context.Guests.FirstOrDefaultAsync(g => g.Phone == dto.Guest.Phone);
            }

            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.FullName))
            {
                guest = await context.Guests.FirstOrDefaultAsync(g => g.FullName == dto.Guest.FullName);
            }

            if (guest != null)
            {
                guestId = guest.Id;
                if (dto.Guest != null)
                {
                    guest.UpdateInfo(
                        dto.Guest.FullName ?? guest.FullName,
                        dto.Guest.Phone ?? guest.Phone,
                        guest.Comment
                    );
                }
            }
            else
            {
                var newGuest = new Guest(
                    dto.Guest?.FullName ?? "Без имени",
                    dto.Guest?.Phone ?? "нет телефона",
                    dto.Guest?.Comment
                );
                context.Guests.Add(newGuest);
                await context.SaveChangesAsync();
                guestId = newGuest.Id;
                guest = newGuest;
            }

            // 2. Создаем бронь
            var booking = new Booking(
                guestId: guestId,
                arrival: dto.ArrivalDate,
                departure: dto.DepartureDate,
                unit: dto.ReservedUnit,
                totalGuests: dto.TotalGuestsCount,
                adults: dto.AdultsCount,
                children: dto.ChildrenCount,
                infants: dto.InfantsCount,
                hasDog: dto.HasDog,
                needsSauna: dto.NeedsSauna,
                needsBanquetHall: dto.NeedsBanquetHall,
                isFirstTimeGuest: dto.IsFirstTimeGuest,
                adminNotes: dto.AdminNotes,
                feedbackComment: dto.FeedbackComment
            );

            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            // 3. ОТПРАВЛЯЕМ УВЕДОМЛЕНИЕ В TELEGRAM (не блокируем ответ)
            _ = Task.Run(async () =>
            {
                try
                {
                    var unitName = dto.ReservedUnit.ToFriendlyString();

                    var message = $"""
                        🔔 <b>Новая бронь!</b>
                        
                        👤 <b>Гость:</b> {guest.FullName}
                        📞 <b>Тел:</b> {guest.Phone}
                        📅 <b>Даты:</b> {dto.ArrivalDate:dd.MM} — {dto.DepartureDate:dd.MM}
                        🏠 <b>Объект:</b> {unitName}
                        👥 <b>Состав:</b> {dto.TotalGuestsCount} чел. (👨{dto.AdultsCount} 👦{dto.ChildrenCount} 👶{dto.InfantsCount})
                        🛠 <b>Допы:</b> {(dto.NeedsSauna ? "🌡️" : "")} {(dto.NeedsBanquetHall ? "🥂" : "")} {(dto.HasDog ? "🐕" : "")}
                        📝 <b>Заметка:</b> {dto.AdminNotes ?? "нет"}
                        """;

                    await telegramService.SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Telegram notification");
                }
            });

            return Ok(booking.Id);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingDto dto)
    {
        var booking = await context.Bookings
            .Include(b => b.Guest)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        // Обновляем гостя если нужно
        if (dto.Guest != null)
        {
            var guest = booking.Guest;
            if (guest.FullName != dto.Guest.FullName || guest.Phone != dto.Guest.Phone)
            {
                guest.UpdateInfo(
                    dto.Guest.FullName ?? guest.FullName,
                    dto.Guest.Phone ?? guest.Phone,
                    guest.Comment
                );
            }
        }

        // Обновляем поля брони
        booking.GetType().GetProperty("ArrivalDate")?.SetValue(booking, DateTime.SpecifyKind(dto.ArrivalDate, DateTimeKind.Utc));
        booking.GetType().GetProperty("DepartureDate")?.SetValue(booking, DateTime.SpecifyKind(dto.DepartureDate, DateTimeKind.Utc));
        booking.GetType().GetProperty("ReservedUnit")?.SetValue(booking, dto.ReservedUnit);
        booking.GetType().GetProperty("TotalGuestsCount")?.SetValue(booking, dto.TotalGuestsCount);
        booking.GetType().GetProperty("AdultsCount")?.SetValue(booking, dto.AdultsCount);
        booking.GetType().GetProperty("ChildrenCount")?.SetValue(booking, dto.ChildrenCount);
        booking.GetType().GetProperty("InfantsCount")?.SetValue(booking, dto.InfantsCount);
        booking.GetType().GetProperty("HasDog")?.SetValue(booking, dto.HasDog);
        booking.GetType().GetProperty("NeedsSauna")?.SetValue(booking, dto.NeedsSauna);
        booking.GetType().GetProperty("NeedsBanquetHall")?.SetValue(booking, dto.NeedsBanquetHall);
        booking.GetType().GetProperty("IsFirstTimeGuest")?.SetValue(booking, dto.IsFirstTimeGuest);
        booking.GetType().GetProperty("AdminNotes")?.SetValue(booking, dto.AdminNotes);
        booking.GetType().GetProperty("FeedbackComment")?.SetValue(booking, dto.FeedbackComment);

        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        // 1. Находим бронь с гостем (чтобы отправить уведомление)
        var booking = await context.Bookings
            .Include(b => b.Guest)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null)
            return NotFound();

        // 2. Сохраняем данные для уведомления ДО удаления
        var guestName = booking.Guest?.FullName ?? "Неизвестный гость";
        var arrivalDate = booking.ArrivalDate;
        var departureDate = booking.DepartureDate;
        var unitName = booking.ReservedUnit.ToFriendlyString();

        // 3. Удаляем
        context.Bookings.Remove(booking);
        await context.SaveChangesAsync();

        // 4. Отправляем уведомление в Telegram (в фоне)
        _ = Task.Run(async () =>
        {
            try
            {
                var message = $"""
                               ❌ <b>Бронь отменена!</b>

                               👤 <b>Гость:</b> {guestName}
                               📅 <b>Даты:</b> {arrivalDate:dd.MM.yyyy} — {departureDate:dd.MM.yyyy}
                               🏠 <b>Объект:</b> {unitName}
                               """;

                await telegramService.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send Telegram notification for deleted booking {BookingId}", id);
            }
        });

        return NoContent();
    }

}