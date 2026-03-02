using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Extensions;

public static class EnumExtensions
{
    public static string ToFriendlyString(this ReservedUnits unit)
    {
        return unit switch
        {
            ReservedUnits.PondSide => "Половинка у пруда",
            ReservedUnits.ParkingSide => "Половинка у парковки",
            ReservedUnits.WholeHouse => "Весь дом",
            _ => "Неизвестный объект"
        };
    }
}