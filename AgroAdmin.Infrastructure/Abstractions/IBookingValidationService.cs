using AgroAdmin.Shared.Dto.Bookings.Requests;
using AgroAdmin.Shared.Dto.Bookings.Responses;

namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface IBookingValidationService
    {
        BookingValidationResult ValidateGuests(CreateBookingDto booking);
        Task<BookingValidationResult> ValidateDatesAsync(CreateBookingDto booking, int? currentBookingId = null);
    }
}