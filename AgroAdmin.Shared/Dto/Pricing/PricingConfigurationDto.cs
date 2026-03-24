namespace AgroAdmin.Shared.Dto.Pricing;

/// <summary>
/// Конфигурация цен для системы калькуляции
/// </summary>
public class PricingConfigurationDto
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; } = true;

    // ===== Правила для разных типов объектов =====

    // Весь дом (вместимость до 13 человек)
    public int MinPricePerNightFullHouse { get; set; }
    public int PricePerAdultFullHouse { get; set; }
    public int IncludedAdultsFullHouse { get; set; } = 2;

    // Половинка у пруда (вместимость 6 человек)
    public int MinPricePerNightPondHalf { get; set; }
    public int PricePerAdultPondHalf { get; set; }
    public int IncludedAdultsPondHalf { get; set; } = 2;

    // Половинка у парковки (вместимость 7 человек)
    public int MinPricePerNightParkingHalf { get; set; }
    public int PricePerAdultParkingHalf { get; set; }
    public int IncludedAdultsParkingHalf { get; set; } = 2;

    // ===== Цены на дополнительные услуги (разовые) =====
    public int SaunaPrice { get; set; }
    public int BanquetHallPrice { get; set; }
    public int DogFee { get; set; }
}