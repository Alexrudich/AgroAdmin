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
        logger.LogInformation("=== GetAll started at {Time} ===", DateTime.Now);

        try
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

            logger.LogInformation("GetAll completed, found {Count} bookings", bookings.Count);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERROR in GetAll: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] BookingDto dto)
    {
        logger.LogInformation("=== Create started at {Time} ===", DateTime.Now);
        logger.LogInformation("Create request data: {@Dto}", dto);

        try
        {
            // 1. Ищем или создаем гостя
            Guest? guest = null;
            int guestId;

            logger.LogInformation("Searching for guest with Id: {Id}, Phone: {Phone}, Name: {Name}",
                dto.Guest?.Id, dto.Guest?.Phone, dto.Guest?.FullName);

            if (dto.Guest?.Id > 0)
            {
                guest = await context.Guests.FindAsync(dto.Guest.Id);
                logger.LogInformation("Guest found by Id: {Found}", guest != null);
            }

            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.Phone))
            {
                guest = await context.Guests.FirstOrDefaultAsync(g => g.Phone == dto.Guest.Phone);
                logger.LogInformation("Guest found by Phone: {Found}", guest != null);
            }

            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.FullName))
            {
                guest = await context.Guests.FirstOrDefaultAsync(g => g.FullName == dto.Guest.FullName);
                logger.LogInformation("Guest found by Name: {Found}", guest != null);
            }

            if (guest != null)
            {
                guestId = guest.Id;
                logger.LogInformation("Using existing guest ID: {GuestId}, Name: {Name}", guestId, guest.FullName);

                if (dto.Guest != null)
                {
                    guest.UpdateInfo(
                        dto.Guest.FullName ?? guest.FullName,
                        dto.Guest.Phone ?? guest.Phone,
                        guest.Comment
                    );
                    logger.LogInformation("Updated guest info");
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
                logger.LogInformation("Created new guest ID: {GuestId}", guestId);
            }

            // 2. Создаем бронь
            logger.LogInformation("Creating booking for guest {GuestId}", guestId);

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
            logger.LogInformation("Booking created with ID: {BookingId}", booking.Id);

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
                    logger.LogInformation("Telegram notification sent for booking {BookingId}", booking.Id);
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
            logger.LogError(ex, "ERROR in Create: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetById(int id)
    {
        logger.LogInformation("GetById called for ID: {Id}", id);

        try
        {
            var b = await context.Bookings
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (b == null)
            {
                logger.LogWarning("Booking with ID {Id} not found", id);
                return NotFound();
            }

            logger.LogInformation("Booking found: ID {Id}, Guest: {GuestName}", id, b.Guest?.FullName);

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
        catch (Exception ex)
        {
            logger.LogError(ex, "ERROR in GetById for ID {Id}: {Message}", id, ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BookingDto dto)
    {
        logger.LogInformation("Update called for ID: {Id}", id);
        logger.LogInformation("Update data: {@Dto}", dto);

        try
        {
            var booking = await context.Bookings
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                logger.LogWarning("Booking with ID {Id} not found for update", id);
                return NotFound();
            }

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
                    logger.LogInformation("Guest info updated for ID {GuestId}", guest.Id);
                }
            }

            // Обновляем бронь
            var arrivalUtc = DateTime.SpecifyKind(dto.ArrivalDate, DateTimeKind.Utc);
            var departureUtc = DateTime.SpecifyKind(dto.DepartureDate, DateTimeKind.Utc);

            // Ручное обновление полей
            booking.GetType().GetProperty("ArrivalDate")?.SetValue(booking, arrivalUtc);
            booking.GetType().GetProperty("DepartureDate")?.SetValue(booking, departureUtc);
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
            logger.LogInformation("Booking ID {Id} updated successfully", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERROR in Update for ID {Id}: {Message}", id, ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        logger.LogInformation("Delete called for ID: {Id}", id);

        try
        {
            var booking = await context.Bookings
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                logger.LogWarning("Booking with ID {Id} not found for delete", id);
                return NotFound();
            }

            var guestName = booking.Guest?.FullName ?? "Неизвестный гость";
            var arrivalDate = booking.ArrivalDate;
            var departureDate = booking.DepartureDate;
            var unitName = booking.ReservedUnit.ToFriendlyString();

            context.Bookings.Remove(booking);
            await context.SaveChangesAsync();
            logger.LogInformation("Booking ID {Id} deleted successfully", id);

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
                    logger.LogInformation("Telegram notification sent for deleted booking {Id}", id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send Telegram notification for deleted booking {Id}", id);
                }
            });

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ERROR in Delete for ID {Id}: {Message}", id, ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}