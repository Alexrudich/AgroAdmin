using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Shared.Extensions;

public static class ReservedUnitsExtensions
{
    /// <summary>
    /// Возвращает эмодзи для типа объекта
    /// </summary>
    /// <param name="unit">Тип объекта</param>
    /// <returns>Эмодзи, соответствующий объекту</returns>
    public static string GetEmoji(this ReservedUnits unit) => unit switch
    {
        ReservedUnits.PondSide => "🌊",
        ReservedUnits.ParkingSide => "🚗",
        ReservedUnits.WholeHouse => "🏠",
        _ => "🏢"
    };

    /// <summary>
    /// Возвращает название объекта на русском языке
    /// </summary>
    public static string GetRussianName(this ReservedUnits unit) => unit switch
    {
        ReservedUnits.PondSide => "Половинка у пруда",
        ReservedUnits.ParkingSide => "Половинка у парковки",
        ReservedUnits.WholeHouse => "Весь дом",
        _ => "Неизвестный объект"
    };
}