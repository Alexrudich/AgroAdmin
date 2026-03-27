using AgroAdmin.Shared.Dto.Bookings.Requests;
using AgroAdmin.Shared.Dto.Bookings.Responses;
using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Services;

public static class BookingValidationRules
{
    private record UnitCapacity(int MaxAdults, int MaxInfants, int TotalBeds, bool AllowsExtraMattress);

    private static UnitCapacity GetCapacityForUnit(ReservedUnits unit) => unit switch
    {
        ReservedUnits.PondSide => new(7, 1, 7, true),
        ReservedUnits.ParkingSide => new(7, 0, 7, true),
        ReservedUnits.WholeHouse => new(13, 1, 14, true),
        _ => throw new ArgumentException($"Unknown unit: {unit}")
    };

    public static BookingValidationResult ValidateGuests(CreateBookingDto booking)
    {
        var result = new BookingValidationResult { IsValid = true };
        var capacity = GetCapacityForUnit(booking.ReservedUnit);

        var totalGuests = booking.AdultsCount + booking.ChildrenCount;

        if (totalGuests > capacity.TotalBeds)
        {
            result.IsValid = false;
            result.Errors.Add($"Максимальное количество гостей: {capacity.TotalBeds} (у вас {totalGuests})");

            if (capacity.AllowsExtraMattress && totalGuests <= capacity.TotalBeds + 1)
            {
                result.Errors.Add("Можно добавить надувной матрас");
            }
        }

        if (booking.InfantsCount > capacity.MaxInfants)
        {
            result.IsValid = false;
            result.Errors.Add($"Максимальное количество младенцев: {capacity.MaxInfants}");
        }

        return result;
    }
}