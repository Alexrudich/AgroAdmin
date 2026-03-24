namespace AgroAdmin.Domain.Models;

public class PricingConfiguration
{
    public int Id { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    // Весь дом
    public int MinPricePerNightFullHouse { get; private set; }
    public int PricePerAdultFullHouse { get; private set; }
    public int IncludedAdultsFullHouse { get; private set; } = 2;

    // Половинка (общая)
    public int MinPricePerNightHalf { get; private set; }
    public int PricePerAdultHalf { get; private set; }
    public int IncludedAdultsHalf { get; private set; } = 2;

    // Дополнительные услуги
    public int SaunaPrice { get; private set; }
    public int BanquetHallPrice { get; private set; }
    public int DogFee { get; private set; }

    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    private PricingConfiguration() { }

    public PricingConfiguration(
        int minPricePerNightFullHouse,
        int pricePerAdultFullHouse,
        int minPricePerNightHalf,
        int pricePerAdultHalf,
        int saunaPrice,
        int banquetHallPrice,
        int dogFee,
        int includedAdultsFullHouse = 2,
        int includedAdultsHalf = 2,
        string? updatedBy = null)
    {
        IsEnabled = true;

        MinPricePerNightFullHouse = minPricePerNightFullHouse;
        PricePerAdultFullHouse = pricePerAdultFullHouse;
        IncludedAdultsFullHouse = includedAdultsFullHouse;

        MinPricePerNightHalf = minPricePerNightHalf;
        PricePerAdultHalf = pricePerAdultHalf;
        IncludedAdultsHalf = includedAdultsHalf;

        SaunaPrice = saunaPrice;
        BanquetHallPrice = banquetHallPrice;
        DogFee = dogFee;

        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Update(
        int minPricePerNightFullHouse,
        int pricePerAdultFullHouse,
        int minPricePerNightHalf,
        int pricePerAdultHalf,
        int saunaPrice,
        int banquetHallPrice,
        int dogFee,
        int includedAdultsFullHouse,
        int includedAdultsHalf,
        string? updatedBy = null)
    {
        MinPricePerNightFullHouse = minPricePerNightFullHouse;
        PricePerAdultFullHouse = pricePerAdultFullHouse;
        IncludedAdultsFullHouse = includedAdultsFullHouse;

        MinPricePerNightHalf = minPricePerNightHalf;
        PricePerAdultHalf = pricePerAdultHalf;
        IncludedAdultsHalf = includedAdultsHalf;

        SaunaPrice = saunaPrice;
        BanquetHallPrice = banquetHallPrice;
        DogFee = dogFee;

        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Disable(string? updatedBy = null)
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Enable(string? updatedBy = null)
    {
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}