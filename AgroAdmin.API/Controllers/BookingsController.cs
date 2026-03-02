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
            // Вызываем наш "умный" конструктор из Domain
            // Он сам проверит даты и количество гостей
            var booking = new Booking(
                dto.GuestName,
                dto.ArrivalDate,
                dto.DepartureDate,
                dto.ReservedUnit,
                dto.TotalGuestsCount,
                dto.AdultsCount,
                dto.ChildrenCount,
                dto.InfantsCount,
                dto.HasDog,
                dto.NeedsSauna,
                dto.NeedsBanquetHall,
                dto.IsFirstTimeGuest,
                dto.AdminNotes,
                dto.GuestPhone
            );

            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            return Ok(booking.Id);
        }
        catch (Exception ex)
        {
            // Если даты неверны или гостей 0, Domain выбросит Exception,
            // и мы вернем его текст клиенту как ошибку 400
            return BadRequest(ex.Message);
        }
    }


    [HttpGet]
    public async Task<ActionResult<IEnumerable<Booking>>> GetAll()
    {
        var bookings = await context.Bookings
            .Include(b => b.SaunaOrders)
            .OrderByDescending(b => b.ArrivalDate)
            .ToListAsync();

        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetById(int id)
    {
        var b = await context.Bookings.FindAsync(id);
        if (b == null) return NotFound();

        // Мапим Domain -> DTO
        var dto = new BookingDto
        {
            Id = b.Id,
            GuestName = b.GuestName,
            GuestPhone = b.GuestPhone,
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
            AdminNotes = b.AdminNotes
        };

        return Ok(dto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, BookingDto dto)
    {
        var booking = await context.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        // Обновляем поля через Reflection или вручную (для надежности)
        // Не забываем про SpecifyKind для Postgres!
        var arrivalUtc = DateTime.SpecifyKind(dto.ArrivalDate, DateTimeKind.Utc);
        var departureUtc = DateTime.SpecifyKind(dto.DepartureDate, DateTimeKind.Utc);

        // Используем маппинг (в идеале тут AutoMapper, но пока так)
        context.Entry(booking).CurrentValues.SetValues(dto);

        // Принудительно ставим UTC для дат
        context.Entry(booking).Property(b => b.ArrivalDate).CurrentValue = arrivalUtc;
        context.Entry(booking).Property(b => b.DepartureDate).CurrentValue = departureUtc;

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