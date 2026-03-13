using AgroAdmin.Shared.Dto.Bookings;

namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface IBookingValidationService
    {
        BookingValidationResult ValidateGuests(CreateBookingDto booking);
        Task<BookingValidationResult> ValidateDatesAsync(CreateBookingDto booking, int? currentBookingId = null);
    }
}