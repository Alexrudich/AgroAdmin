using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Domain.Models;
using AgroAdmin.Shared.Enums;

namespace AgroAdmin.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SeedController(AppDbContext context) : ControllerBase
{
    private const int TargetBookingCount = 50;

    [HttpPost("generate")]
    public async Task<ActionResult<string>> GenerateTestData()
    {
        var existingCount = await context.Bookings.CountAsync();
        if (existingCount >= TargetBookingCount)
        {
            return BadRequest($"В базе уже {existingCount} записей. Генерация отменена.");
        }

        var needed = TargetBookingCount - existingCount;
        var rand = new Random();

        var firstNames = new[] { "Александр", "Дмитрий", "Елена", "Ольга", "Сергей", "Татьяна", "Андрей", "Мария" };
        var lastNames = new[] { "Иванов", "Петров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Павлов", "Соколов" };

        var bookings = new List<Booking>();

        for (var i = 0; i < needed; i++)
        {
            var firstName = firstNames[rand.Next(firstNames.Length)];
            var lastName = lastNames[rand.Next(lastNames.Length)];

            // Генерируем даты: заезд в ближайшие 30 дней
            var arrival = DateTime.UtcNow.AddDays(rand.Next(-10, 30)).Date;
            var departure = arrival.AddDays(rand.Next(1, 5));

            var unit = (ReservedUnits)rand.Next(0, 3);
            var testGuestName = $"[TEST] {firstName} {lastName}";

            var adults = rand.Next(1, 4);
            var children = rand.Next(0, 3);
            var infants = rand.Next(0, 2);
            var total = adults + children + infants;
            var hasDog = rand.Next(100) < 30;
            var needsSauna = rand.Next(100) < 70;
            var needsHall = rand.Next(100) < 10;
            var isFirstTimeGuest = rand.Next(100) < 40;
            var adminNotes = rand.Next(100) < 30 ? "Нужен мангал и дрова" : null;
            var guestPhone = $"+37529{rand.Next(1000000, 9999999)}";

            var booking = new Booking(
                guestName: testGuestName,
                arrival: arrival,
                departure: departure,
                unit: unit,
                totalGuests: total,
                adults: adults,
                children: children,
                infants: infants,
                hasDog: hasDog,
                needsSauna: needsSauna,
                needsBanquetHall: needsHall,
                isFirstTimeGuest: isFirstTimeGuest,
                adminNotes: adminNotes,
                guestPhone: guestPhone
            );

            if (rand.Next(100) < 50)
            {
                booking.AddSaunaOrder(arrival.AddHours(18), 3);
            }

            bookings.Add(booking);
        }

        context.Bookings.AddRange(bookings);
        await context.SaveChangesAsync();

        return Ok($"Добавлено {needed} записей. Всего в базе: {TargetBookingCount}");
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<string>> ClearTestData()
    {
        var count = await context.Bookings
            .Where(b => b.GuestName.StartsWith("[TEST]"))
            .ExecuteDeleteAsync();

        return Ok($"Удалено {count} тестовых бронирований. Реальные люди (без префикса) не пострадали.");
    }

}
