using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

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

        var firstNames = new[] { "Александр", "Дмитрий", "Игнат", "Себастьян", "Сергей", "Лаврентий", "Андрей", "Марио", "Евгений", "Николай" };
        var lastNames = new[] { "Иванов", "Петров", "Смирнов", "Кузнецов", "Попов", "Васильев", "Павлов", "Соколов", "Михайлов", "Федоров" };

        // Создаем 25 гостей (больше для пагинации)
        for (int i = 0; i < 25; i++)
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
            .Where(b => b.Guest == null || !b.Guest.FullName.StartsWith("[TEST]"))
            .ToListAsync();

        // Создаем 150 броней (для теста пагинации)
        var targetNewRecords = 150;
        var attempts = 0;

        while (newBookings.Count < targetNewRecords && attempts < 1000)
        {
            attempts++;
            var guest = guests[rand.Next(guests.Count)];

            // Даты от -30 до +90 дней
            var arrival = DateTime.UtcNow.AddDays(rand.Next(-30, 90)).Date;
            var departure = arrival.AddDays(rand.Next(1, 14)); // от 1 до 14 дней
            var unit = (ReservedUnits)rand.Next(0, 3);

            var adults = rand.Next(1, 6);
            var children = rand.Next(0, 4);
            var infants = rand.Next(0, 2);
            var total = adults + children + infants;

            int maxCapacity = (unit == ReservedUnits.WholeHouse) ? 14 : 7;
            if (total > maxCapacity) continue;

            // Объединяем существующие и новые брони для проверки
            var allToCheck = allExisting.Concat(newBookings).ToList();

            // Твоя правильная проверка конфликтов
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
                    isFirstTimeGuest: rand.Next(2) == 0, // 50/50 новые/постоянные
                    adminNotes: rand.Next(100) < 20 ? "Нужны доп. полотенца" : null,
                    feedbackComment: rand.Next(100) < 30 ? "Всё отлично, приедем ещё!" : null
                );

                newBookings.Add(booking);
            }
        }

        context.Bookings.AddRange(newBookings);
        await context.SaveChangesAsync();

        return Ok($"Создано {guests.Count} тестовых гостей и добавлено {newBookings.Count} броней. " +
                  $"Теперь можно тестировать пагинацию и фильтры!");
    }

    [HttpPost("create-admin")]
    public async Task<ActionResult<string>> CreateAdmin(string username, string password)
    {
        if (await context.AdminUsers.AnyAsync())
            return BadRequest("Admin already exists");

        var admin = new AdminUser(username, password);
        context.AdminUsers.Add(admin);
        await context.SaveChangesAsync();

        return Ok("Admin created");
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

    [HttpPost("upload-excel")]
    public async Task<IActionResult> UploadExcel(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Файл не выбран");

        var guestsAdded = 0;
        var bookingsAdded = 0;
        var errors = new List<string>();

        // Настройка лицензии EPPlus (нужно для работы)
        ExcelPackage.License.SetNonCommercialPersonal("AgroAdmin");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        using var package = new ExcelPackage(stream);

        var worksheet = package.Workbook.Worksheets[0]; // первый лист
        var rowCount = worksheet.Dimension?.Rows ?? 0;

        for (int row = 2; row <= rowCount; row++) // с 2, потому что 1 — заголовки
        {
            try
            {
                var guestName = worksheet.Cells[row, 1].Text?.Trim();
                var phone = worksheet.Cells[row, 2].Text?.Trim();
                var arrivalStr = worksheet.Cells[row, 3].Text?.Trim();
                var departureStr = worksheet.Cells[row, 4].Text?.Trim();
                var unitStr = worksheet.Cells[row, 5].Text?.Trim();
                var adultsStr = worksheet.Cells[row, 6].Text?.Trim();
                var childrenStr = worksheet.Cells[row, 7].Text?.Trim();
                var infantsStr = worksheet.Cells[row, 8].Text?.Trim();
                var hasDogStr = worksheet.Cells[row, 9].Text?.Trim();
                var needsSaunaStr = worksheet.Cells[row, 10].Text?.Trim();
                var needsBanquetStr = worksheet.Cells[row, 11].Text?.Trim();
                var notes = worksheet.Cells[row, 12].Text?.Trim();
                var priceStr = worksheet.Cells[row, 13].Text?.Trim();

                // Пропускаем пустые строки
                if (string.IsNullOrWhiteSpace(guestName) && string.IsNullOrWhiteSpace(phone))
                    continue;

                // Поиск или создание гостя
                Guest? guest = null;
                if (!string.IsNullOrWhiteSpace(phone))
                    guest = await context.Guests.FirstOrDefaultAsync(g => g.Phone == phone);

                if (guest == null)
                {
                    guest = new Guest(
                        fullName: string.IsNullOrWhiteSpace(guestName) ? "Без имени" : guestName,
                        phone: string.IsNullOrWhiteSpace(phone) ? "нет телефона" : phone,
                        comment: null
                    );
                    context.Guests.Add(guest);
                    await context.SaveChangesAsync();
                    guestsAdded++;
                }

                // Парсинг дат
                if (!DateOnly.TryParse(arrivalStr, out var arrival))
                {
                    errors.Add($"Строка {row}: неверный формат даты заезда");
                    continue;
                }
                if (!DateOnly.TryParse(departureStr, out var departure))
                {
                    errors.Add($"Строка {row}: неверный формат даты выезда");
                    continue;
                }

                // Парсинг количества людей
                int adults = ParseInt(adultsStr, 1);
                int children = ParseInt(childrenStr, 0);
                int infants = ParseInt(infantsStr, 0);
                int total = adults + children + infants;

                // Парсинг объекта
                var unit = ParseUnit(unitStr);

                // Парсинг булевых значений
                bool hasDog = ParseBool(hasDogStr);
                bool needsSauna = ParseBool(needsSaunaStr);
                bool needsBanquet = ParseBool(needsBanquetStr);

                // Создание брони
                var booking = new Booking(
                    guestId: guest.Id,
                    arrival: arrival.ToDateTime(TimeOnly.MinValue),
                    departure: departure.ToDateTime(TimeOnly.MinValue),
                    unit: unit,
                    totalGuests: total,
                    adults: adults,
                    children: children,
                    infants: infants,
                    hasDog: hasDog,
                    needsSauna: needsSauna,
                    needsBanquetHall: needsBanquet,
                    isFirstTimeGuest: true,
                    adminNotes: notes,
                    feedbackComment: null
                );

                context.Bookings.Add(booking);
                bookingsAdded++;
            }
            catch (Exception ex)
            {
                errors.Add($"Строка {row}: {ex.Message}");
            }
        }

        await context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Загружено {guestsAdded} гостей и {bookingsAdded} броней",
            errors = errors.Any() ? errors : null
        });
    }

    // Вспомогательные методы (принимают string? для безопасной работы)
    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim().ToLower();
        return value == "true" || value == "1" || value == "yes" || value == "да";
    }

    private static ReservedUnits ParseUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ReservedUnits.PondSide;
        return value.Trim() switch
        {
            "0" or "Пруд" or "пруд" or "половинка от пруда" => ReservedUnits.PondSide,
            "1" or "Парковка" or "парковка" or "половинка от парковки" => ReservedUnits.ParkingSide,
            "2" or "Дом" or "дом" or "весь дом" or "целый" => ReservedUnits.WholeHouse,
            _ => ReservedUnits.PondSide
        };
    }
}