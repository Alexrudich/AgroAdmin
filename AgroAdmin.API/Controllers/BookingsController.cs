using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Dto;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController(AppDbContext context) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] BookingDto dto)
    {
        try
        {
            // 1. Ищем или создаем гостя
            int guestId;

            Guest? guest = null;

            // Если есть выбранный гость с Id
            if (dto.Guest?.Id > 0)
            {
                guest = await context.Guests.FindAsync(dto.Guest.Id);
            }

            // Если не нашли по Id, ищем по телефону
            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.Phone))
            {
                guest = await context.Guests
                    .FirstOrDefaultAsync(g => g.Phone == dto.Guest.Phone);
            }

            // Если не нашли по телефону, ищем по имени
            if (guest == null && !string.IsNullOrEmpty(dto.Guest?.FullName))
            {
                guest = await context.Guests
                    .FirstOrDefaultAsync(g => g.FullName == dto.Guest.FullName);
            }

            if (guest != null)
            {
                guestId = guest.Id;
                // Обновляем данные гостя если изменились
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
                // Создаем нового гостя
                var newGuest = new Guest(
                    dto.Guest?.FullName ?? "Без имени",
                    dto.Guest?.Phone ?? "нет телефона",
                    dto.Guest?.Comment
                );
                context.Guests.Add(newGuest);
                await context.SaveChangesAsync();
                guestId = newGuest.Id;
            }

            // 2. Создаем бронь с guestId
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

            return Ok(booking.Id);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }


    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAll()
    {
        var bookings = await context.Bookings
            .Include(b => b.Guest)  // ВАЖНО Подгружаем гостя
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

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BookingDto dto)
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

        // Обновляем бронь
        var arrivalUtc = DateTime.SpecifyKind(dto.ArrivalDate, DateTimeKind.Utc);
        var departureUtc = DateTime.SpecifyKind(dto.DepartureDate, DateTimeKind.Utc);

        // Ручное обновление полей (можно использовать AutoMapper позже)
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
        return NoContent();
    }


    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var affected = await context.Bookings
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync();

        return affected > 0 ? NoContent() : NotFound();
    }

}