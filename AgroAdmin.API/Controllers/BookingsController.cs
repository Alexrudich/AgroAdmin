using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Bookings.Events;
using AgroAdmin.Shared.Dto.Bookings.Requests;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Dto.Guests;
using AgroAdmin.Shared.Dto.Telegram.Responses;
using AgroAdmin.Shared.Enums;
using AgroAdmin.Shared.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController(
    AppDbContext context,
    ITelegramService telegramService,
    IBookingValidationService validationService,
    IPublishEndpoint publishEndpoint,
    IBookingTelegramService bookingTelegramService,
    ILogger<BookingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResultDto<BookingDto>>> GetAll([FromQuery] BookingFilterDto filter)
    {
        var query = context.Bookings
            .Include(b => b.Guest)
            .Include(b => b.SaunaOrders)
            .AsQueryable();

        // ПРИМЕНЯЕМ ФИЛЬТРЫ
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.ToLower();
            query = query.Where(b =>
                (b.Guest.FullName != null && b.Guest.FullName.ToLower().Contains(searchTerm)) ||
                (b.Guest.Phone != null && b.Guest.Phone.Contains(searchTerm))
            );
        }

        if (filter.ReservedUnit.HasValue)
            query = query.Where(b => b.ReservedUnit == filter.ReservedUnit.Value);

        if (filter.IsFirstTimeGuest.HasValue)
            query = query.Where(b => b.IsFirstTimeGuest == filter.IsFirstTimeGuest.Value);

        if (filter.NeedsSauna.HasValue)
            query = query.Where(b => b.NeedsSauna == filter.NeedsSauna.Value);

        if (filter.NeedsBanquetHall.HasValue)
            query = query.Where(b => b.NeedsBanquetHall == filter.NeedsBanquetHall.Value);

        if (filter.HasDog.HasValue)
            query = query.Where(b => b.HasDog == filter.HasDog.Value);

        if (filter.DateFrom.HasValue)
            query = query.Where(b => b.ArrivalDate >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(b => b.ArrivalDate <= filter.DateTo.Value);

        // ПРИМЕНЯЕМ СОРТИРОВКУ
        query = filter.SortBy?.ToLower() switch
        {
            "guestname" => filter.SortDesc
                ? query.OrderByDescending(b => b.Guest.FullName)
                : query.OrderBy(b => b.Guest.FullName),

            "reservedunit" => filter.SortDesc
                ? query.OrderByDescending(b => b.ReservedUnit)
                : query.OrderBy(b => b.ReservedUnit),

            "arrivaldate" => filter.SortDesc
                ? query.OrderByDescending(b => b.ArrivalDate)
                : query.OrderBy(b => b.ArrivalDate),

            _ => filter.SortDesc
                ? query.OrderByDescending(b => b.ArrivalDate)
                : query.OrderBy(b => b.ArrivalDate)
        };

        // ПОЛУЧАЕМ ОБЩЕЕ КОЛИЧЕСТВО
        var totalCount = await query.CountAsync();

        // ПРИМЕНЯЕМ ПАГИНАЦИЮ
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
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
                CreatedAt = b.CreatedAt,
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

        return Ok(new PagedResultDto<BookingDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        });
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
            CreatedAt = b.CreatedAt,
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
            var errors = new List<string>();
            DateTime? earliestAvailable = null;

            // 1. Валидация количества гостей
            var guestValidation = validationService.ValidateGuests(dto);
            if (!guestValidation.IsValid)
            {
                errors.AddRange(guestValidation.Errors);
            }

            // 2. Валидация доступности дат
            var dateValidation = await validationService.ValidateDatesAsync(dto);
            if (!dateValidation.IsValid)
            {
                errors.AddRange(dateValidation.Errors);
                earliestAvailable = dateValidation.EarliestAvailableDate;
            }

            // 3. Если есть ошибки - возвращаем все сразу
            if (errors.Any())
            {
                var response = new
                {
                    errors = errors,
                    type = "validation"
                };

                // Добавляем earliestAvailable только если он есть
                if (earliestAvailable.HasValue)
                {
                    return Conflict(new
                    {
                        errors = errors,
                        earliestAvailable = earliestAvailable,
                        type = "validation"
                    });
                }

                return BadRequest(response);
            }

            // 4. Ищем или создаем гостя
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

            // 5. Создаем бронь
            var booking = new Booking(
                guestId: guestId,
                arrival: DateTime.SpecifyKind(dto.ArrivalDate, DateTimeKind.Utc),
                departure: DateTime.SpecifyKind(dto.DepartureDate, DateTimeKind.Utc),
                createdAt: DateTime.UtcNow,
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
                feedbackComment: dto.FeedbackComment,
                accommodationCost: dto.AccommodationCost,
                checkInTime: dto.CheckInTime
            );

            context.Bookings.Add(booking);
            await context.SaveChangesAsync();

            // ОТПРАВКА В RABBITMQ
            try
            {
                await publishEndpoint.Publish(new BookingCreatedEvent
                {
                    BookingId = booking.Id,
                    GuestName = guest.FullName,
                    Phone = guest.Phone,
                    ArrivalDate = booking.ArrivalDate,
                    DepartureDate = booking.DepartureDate,
                    Unit = booking.ReservedUnit,
                    NeedsSauna = booking.NeedsSauna,
                    AdminNotes = booking.AdminNotes,
                    CheckInTime = booking.CheckInTime,
                    AccommodationCost = booking.AccommodationCost,
                    TotalGuestsCount = booking.TotalGuestsCount
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not publish BookingCreatedEvent to RabbitMQ");
                // Не прерываем выполнение, так как бронь уже в базе
            }

            // 6. ОТПРАВЛЯЕМ УВЕДОМЛЕНИЕ В TELEGRAM
            _ = Task.Run(async () =>
            {
                try
                {
                    var unitName = dto.ReservedUnit.ToFriendlyString();
                    var checkInTimeStr = dto.CheckInTime != TimeSpan.Zero
                        ? dto.CheckInTime.ToString(@"hh\:mm")
                        : "14:00";

                    var message = $"🔔 <b>Новая бронь!</b>\n" +
                                  $"\n" +
                                  $"👤 <b>Гость:</b> {guest.FullName}\n" +
                                  $"📞 <b>Тел:</b> {guest.Phone}\n" +
                                  $"📅 <b>Даты:</b> {dto.ArrivalDate:dd.MM} — {dto.DepartureDate:dd.MM}\n" +
                                  $"⏰ <b>Заезд:</b> {checkInTimeStr}\n" +
                                  $"🏠 <b>Объект:</b> {unitName}\n" +
                                  $"👥 <b>Состав:</b> {dto.TotalGuestsCount} чел. (👨{dto.AdultsCount} 👦{dto.ChildrenCount} 👶{dto.InfantsCount})\n" +
                                  $"🛠 <b>Допы:</b> {(dto.NeedsSauna ? "🌡️ " : "")}{(dto.NeedsBanquetHall ? "🥂 " : "")}{(dto.HasDog ? "🐕" : "")}".TrimEnd();

                    if (dto.AccommodationCost.HasValue && dto.AccommodationCost.Value > 0)
                    {
                        message += $"\n💰 <b>Стоимость:</b> {dto.AccommodationCost.Value:N0} BYN";
                    }

                    message += $"\n📝 <b>Заметки:</b> {dto.AdminNotes ?? "нет"}";

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
            logger.LogError(ex, "Error creating booking");
            return BadRequest(new
            {
                errors = new[] { "Ошибка при создании брони" },
                type = "error"
            });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingDto dto)
    {
        try
        {
            var booking = await context.Bookings
                .Include(b => b.Guest)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null) return NotFound();

            var errors = new List<string>();
            DateTime? earliestAvailable = null;

            // 1. Валидация количества гостей
            var guestValidation = validationService.ValidateGuests(new CreateBookingDto
            {
                AdultsCount = dto.AdultsCount,
                ChildrenCount = dto.ChildrenCount,
                InfantsCount = dto.InfantsCount,
                ReservedUnit = dto.ReservedUnit
            });

            if (!guestValidation.IsValid)
            {
                errors.AddRange(guestValidation.Errors);
            }

            // 2. Валидация доступности дат (исключая текущую бронь)
            var dateValidation = await validationService.ValidateDatesAsync(new CreateBookingDto
            {
                ArrivalDate = dto.ArrivalDate,
                DepartureDate = dto.DepartureDate,
                ReservedUnit = dto.ReservedUnit
            }, id);

            if (!dateValidation.IsValid)
            {
                errors.AddRange(dateValidation.Errors);
                earliestAvailable = dateValidation.EarliestAvailableDate;
            }

            // 3. Если есть ошибки - возвращаем все сразу
            if (errors.Any())
            {
                var response = new
                {
                    errors = errors,
                    type = "validation"
                };

                if (earliestAvailable.HasValue)
                {
                    return Conflict(new
                    {
                        errors = errors,
                        earliestAvailable = earliestAvailable,
                        type = "validation"
                    });
                }

                return BadRequest(response);
            }

            // 4. Обновляем гостя если нужно
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

            // 5. Обновляем поля брони
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
            booking.GetType().GetProperty("AccommodationCost")?.SetValue(booking, dto.AccommodationCost);
            booking.GetType().GetProperty("CheckInTime")?.SetValue(booking, dto.CheckInTime);

            await context.SaveChangesAsync();

            logger.LogInformation("Booking {Id} updated successfully", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating booking {Id}", id);
            return BadRequest(new
            {
                errors = new[] { "Ошибка при обновлении брони" },
                type = "error"
            });
        }
    }

    [HttpPost("validate")]
    public async Task<ActionResult<BookingValidationResult>> Validate(
        [FromBody] CreateBookingDto dto,
        [FromQuery] int? bookingId = null)
    {
        try
        {
            var errors = new List<string>();
            DateTime? earliestAvailable = null;

            // 1. Валидация количества гостей
            var guestValidation = validationService.ValidateGuests(dto);
            if (!guestValidation.IsValid)
            {
                errors.AddRange(guestValidation.Errors);
            }

            // 2. Валидация доступности дат (передаем bookingId)
            var dateValidation = await validationService.ValidateDatesAsync(dto, bookingId);
            if (!dateValidation.IsValid)
            {
                errors.AddRange(dateValidation.Errors);
                earliestAvailable = dateValidation.EarliestAvailableDate;
            }

            // 3. Возвращаем все ошибки сразу
            return Ok(new BookingValidationResult
            {
                IsValid = !errors.Any(),
                Errors = errors,
                EarliestAvailableDate = earliestAvailable
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating booking");
            return BadRequest(new BookingValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Ошибка валидации" }
            });
        }
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

    [HttpGet("nearest")]
    public async Task<ActionResult<List<TelegramBookingDto>>> GetNearestBookings([FromQuery] int days = 7)
    {
        try
        {
            var today = DateTime.Today;
            var endDate = today.AddDays(days);

            // 1. Получаем данные из БД без switch
            var bookingsData = await context.Bookings
                .Include(b => b.Guest)
                .Where(b => b.ArrivalDate >= today &&
                           b.ArrivalDate <= endDate)
                .OrderBy(b => b.ArrivalDate)
                .ThenBy(b => b.CheckInTime)
                .Take(15)
                .Select(b => new
                {
                    b.Id,
                    b.Guest,
                    b.ArrivalDate,
                    b.DepartureDate,
                    b.TotalGuestsCount,
                    b.ReservedUnit,
                    b.NeedsSauna,
                    b.AccommodationCost,
                    b.AdminNotes
                })
                .ToListAsync();

            // 2. Преобразуем в DTO с вычислением эмодзи в памяти
            var result = bookingsData.Select(b => new TelegramBookingDto
            {
                Id = b.Id,
                GuestName = b.Guest?.FullName ?? "Гость не указан",
                Phone = b.Guest?.Phone,
                ArrivalDate = b.ArrivalDate,
                DepartureDate = b.DepartureDate,
                TotalGuestsCount = b.TotalGuestsCount,
                UnitName = b.ReservedUnit.ToFriendlyString(),
                UnitEmoji = b.ReservedUnit switch
                {
                    ReservedUnits.PondSide => "🌊",
                    ReservedUnits.ParkingSide => "🚗",
                    ReservedUnits.WholeHouse => "🏠",
                    _ => "🏢"
                },
                NeedsSauna = b.NeedsSauna,
                TotalPrice = b.AccommodationCost,
                Notes = b.AdminNotes
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting nearest bookings for Telegram");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("availability")]
    public async Task<ActionResult<List<DailyAvailability>>> GetAvailability(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate == default || endDate == default)
            {
                return BadRequest(new { error = "startDate and endDate are required" });
            }

            var availability = await bookingTelegramService.GetDailyAvailabilityAsync(startDate, endDate);
            return Ok(availability);
        }
        catch (Exception ex)
        { 
            logger.LogError(ex, "Error getting availability");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BookingSummaryDto>> GetSummary(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate == default || endDate == default)
            {
                return BadRequest(new { error = "startDate and endDate are required" });
            }

            var summary = await bookingTelegramService.GetBookingSummaryAsync(startDate, endDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting booking summary");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}