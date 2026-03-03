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
        var allExisting = await context.Bookings.ToListAsync();
        var rand = new Random();
        var newBookings = new List<Booking>();

        var firstNames = new[] { "Александр", "Дмитрий", "Елена", "Ольга", "Сергей", "Татьяна", "Андрей", "Мария" };
        var lastNames = new[] { "Иванов", "Петров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Павлов", "Соколов" };

        var attempts = 0;
        // Уменьшим плотность, чтобы оставить "пустые даты" для наглядности
        var targetNewRecords = 25;

        while (newBookings.Count < targetNewRecords && attempts < 1000)
        {
            attempts++;

            // 1. Генерируем случайный период (на 60 дней вперед для разряженности)
            var arrival = DateTime.UtcNow.AddDays(rand.Next(-5, 50)).Date;
            var departure = arrival.AddDays(rand.Next(1, 4));
            var unit = (ReservedUnits)rand.Next(0, 3);

            // 2. Генерируем состав гостей СРАЗУ, чтобы проверить вместимость
            var adults = rand.Next(1, 6);
            var children = rand.Next(0, 4);
            var infants = rand.Next(0, 2);
            var total = adults + children + infants;

            // ПРАВИЛО 4: Валидация вместимости
            int maxCapacity = (unit == ReservedUnits.WholeHouse) ? 14 : 7;
            if (total > maxCapacity) continue; // Пропускаем, если людей слишком много для этого типа

            // 3. ХИРУРГИЧЕСКАЯ ПРОВЕРКА НА ПЕРЕСЕЧЕНИЕ (Overlap)
            var combined = allExisting.Concat(newBookings).ToList();

            bool isBusy = combined.Any(b =>
                // Проверка пересечения дат
                (arrival < b.DepartureDate && departure > b.ArrivalDate) &&
                // Логика конфликта объектов (Правила 1-2)
                (
                    b.ReservedUnit == unit ||
                    b.ReservedUnit == ReservedUnits.WholeHouse ||
                    unit == ReservedUnits.WholeHouse
                )
            );

            if (!isBusy)
            {
                // Шанс 20% пропустить итерацию, чтобы создать "дыры" в календаре
                if (rand.Next(100) < 20) continue;

                var firstName = firstNames[rand.Next(firstNames.Length)];
                var lastName = lastNames[rand.Next(lastNames.Length)];

                var booking = new Booking(
                    guestName: $"[TEST] {firstName} {lastName}",
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
                    isFirstTimeGuest: rand.Next(100) < 40,
                    adminNotes: rand.Next(100) < 20 ? "Нужны доп. полотенца" : null,
                    guestPhone: $"+37529{rand.Next(1000000, 9999999)}"
                );

                newBookings.Add(booking);
            }
        }

        context.Bookings.AddRange(newBookings);
        await context.SaveChangesAsync();

        return Ok($"Добавлено {newBookings.Count} броней. Пустые дни оставлены для отладки UI.");
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
