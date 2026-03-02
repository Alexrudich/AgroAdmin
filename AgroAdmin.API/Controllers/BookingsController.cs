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
    // Создание бронирования
    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] BookingDto dto)
    {
        try
        {
            // Используем DDD-конструктор: инкапсуляция и валидация срабатывают здесь
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
            // TODO Тут пробросить DomainException в Middleware"
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