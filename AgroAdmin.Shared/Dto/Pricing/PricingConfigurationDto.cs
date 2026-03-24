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

    // Половинка (единая конфигурация)
    public int MinPricePerNightHalf { get; set; }
    public int PricePerAdultHalf { get; set; }
    public int IncludedAdultsHalf { get; set; } = 2;

    // ===== Цены на дополнительные услуги (разовые) =====
    public int SaunaPrice { get; set; }
    public int BanquetHallPrice { get; set; }
    public int DogFee { get; set; }
}