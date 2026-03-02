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
}