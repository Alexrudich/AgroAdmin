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
    [HttpPost("generate")]
    public async Task<ActionResult<string>> GenerateTestData()
    {
        // Проверяем, есть ли уже тестовые данные
        var existingTestGuests = await context.Guests
            .Where(g => g.FullName.StartsWith("[TEST]"))
            .CountAsync();

        var existingTestBookings = await context.Bookings
            .Include(b => b.Guest)
            .Where(b => b.Guest != null && b.Guest.FullName.StartsWith("[TEST]"))
            .CountAsync();

        if (existingTestGuests > 0 || existingTestBookings > 0)
        {
            return Ok($"Тестовые данные уже существуют: {existingTestGuests} гостей, {existingTestBookings} броней. " +
                      "Если нужно создать заново, сначала вызовите clear.");
        }

        var rand = new Random();
        var newGuests = new List<Guest>();
        var newBookings = new List<Booking>();

        var firstNames = new[] { "Александр", "Дмитрий", "Игнат", "Себастьян", "Сергей", "Лаврентий", "Андрей", "Мария" };
        var lastNames = new[] { "Иванов", "Петров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Павлов", "Соколов" };

        // Создаем гостей
        for (int i = 0; i < 15; i++)
        {
            var firstName = firstNames[rand.Next(firstNames.Length)];
            var lastName = lastNames[rand.Next(lastNames.Length)];
            var fullName = $"[TEST] {firstName} {lastName}";
            var phone = $"+37529{rand.Next(1000000, 9999999)}";
            var comment = rand.Next(100) < 20 ? "Постоянный клиент" : null;

            newGuests.Add(new Guest(fullName, phone, comment));
        }

        await context.Guests.AddRangeAsync(newGuests);
        await context.SaveChangesAsync();

        // Создаем брони
        var guests = await context.Guests.Where(g => g.FullName.StartsWith("[TEST]")).ToListAsync();
        var allExisting = await context.Bookings
            .Include(b => b.Guest)
            .Where(b => b.Guest == null || !b.Guest.FullName.StartsWith("[TEST]")) // Исключаем тестовые из проверки
            .ToListAsync();

        var targetNewRecords = 25;
        var attempts = 0;

        while (newBookings.Count < targetNewRecords && attempts < 1000)
        {
            attempts++;
            var guest = guests[rand.Next(guests.Count)];
            var arrival = DateTime.UtcNow.AddDays(rand.Next(-5, 50)).Date;
            var departure = arrival.AddDays(rand.Next(1, 4));
            var unit = (ReservedUnits)rand.Next(0, 3);

            var adults = rand.Next(1, 6);
            var children = rand.Next(0, 4);
            var infants = rand.Next(0, 2);
            var total = adults + children + infants;

            int maxCapacity = (unit == ReservedUnits.WholeHouse) ? 14 : 7;
            if (total > maxCapacity) continue;

            // Объединяем существующие и новые брони для проверки
            var allToCheck = allExisting.Concat(newBookings).ToList();

            // ПРАВИЛЬНАЯ ПРОВЕРКА КОНФЛИКТОВ
            bool isBusy;

            if (unit == ReservedUnits.WholeHouse)
            {
                // Весь дом - нельзя если ЛЮБАЯ бронь есть на эти даты
                isBusy = allToCheck.Any(b =>
                    arrival < b.DepartureDate && departure > b.ArrivalDate
                );
            }
            else
            {
                // Половинка - нельзя если:
                // 1. Есть бронь на эту же половинку
                // 2. Есть бронь на весь дом
                isBusy = allToCheck.Any(b =>
                    arrival < b.DepartureDate && departure > b.ArrivalDate &&
                    (
                        b.ReservedUnit == unit ||                    // та же половинка
                        b.ReservedUnit == ReservedUnits.WholeHouse    // или весь дом
                    )
                );
            }

            if (!isBusy)
            {
                if (rand.Next(100) < 20) continue;

                var booking = new Booking(
                    guestId: guest.Id,
                    arrival: arrival,
                    departure: departure,
                    unit: unit,
                    totalGuests: total,
                    adults: adults,
                    children: children,
                    infants: infants,
                    hasDog: rand.Next(100) < 20,
                    needsSauna: rand.Next(100) < 50,
                    needsBanquetHall: rand.Next(100) < 15,
                    isFirstTimeGuest: false,
                    adminNotes: rand.Next(100) < 20 ? "Нужны доп. полотенца" : null,
                    feedbackComment: rand.Next(100) < 30 ? "Всё отлично, приедем ещё!" : null
                );

                newBookings.Add(booking);
            }
        }

        context.Bookings.AddRange(newBookings);
        await context.SaveChangesAsync();

        return Ok($"Создано {guests.Count} тестовых гостей и добавлено {newBookings.Count} броней.");
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<string>> ClearTestData()
    {
        // Находим всех тестовых гостей (у которых имя начинается с [TEST])
        var testGuests = await context.Guests
            .Where(g => g.FullName.StartsWith("[TEST]"))
            .ToListAsync();

        var testGuestIds = testGuests.Select(g => g.Id).ToList();

        // Удаляем их брони
        var deletedBookings = await context.Bookings
            .Where(b => testGuestIds.Contains(b.GuestId))
            .ExecuteDeleteAsync();

        // Удаляем самих гостей
        var deletedGuests = await context.Guests
            .Where(g => g.FullName.StartsWith("[TEST]"))
            .ExecuteDeleteAsync();

        return Ok($"Удалено {deletedGuests} тестовых гостей и {deletedBookings} их бронирований.");
    }
}
