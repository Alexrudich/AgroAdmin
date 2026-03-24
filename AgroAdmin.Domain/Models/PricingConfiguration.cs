namespace AgroAdmin.Domain.Models;

public class PricingConfiguration
{
    public int Id { get; private set; }
    public bool IsEnabled { get; private set; } = true;

    // Весь дом
    public int MinPricePerNightFullHouse { get; private set; }
    public int PricePerAdultFullHouse { get; private set; }
    public int IncludedAdultsFullHouse { get; private set; } = 2;

    // Половинка у пруда
    public int MinPricePerNightPondHalf { get; private set; }
    public int PricePerAdultPondHalf { get; private set; }
    public int IncludedAdultsPondHalf { get; private set; } = 2;

    // Половинка у парковки
    public int MinPricePerNightParkingHalf { get; private set; }
    public int PricePerAdultParkingHalf { get; private set; }
    public int IncludedAdultsParkingHalf { get; private set; } = 2;

    // Дополнительные услуги (разовые)
    public int SaunaPrice { get; private set; }
    public int BanquetHallPrice { get; private set; }
    public int DogFee { get; private set; }

    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    private PricingConfiguration() { }

    public PricingConfiguration(
        int minPricePerNightFullHouse,
        int pricePerAdultFullHouse,
        int minPricePerNightPondHalf,
        int pricePerAdultPondHalf,
        int minPricePerNightParkingHalf,
        int pricePerAdultParkingHalf,
        int saunaPrice,
        int banquetHallPrice,
        int dogFee,
        int includedAdultsFullHouse = 2,
        int includedAdultsPondHalf = 2,
        int includedAdultsParkingHalf = 2,
        string? updatedBy = null)
    {
        IsEnabled = true;

        MinPricePerNightFullHouse = minPricePerNightFullHouse;
        PricePerAdultFullHouse = pricePerAdultFullHouse;
        IncludedAdultsFullHouse = includedAdultsFullHouse;

        MinPricePerNightPondHalf = minPricePerNightPondHalf;
        PricePerAdultPondHalf = pricePerAdultPondHalf;
        IncludedAdultsPondHalf = includedAdultsPondHalf;

        MinPricePerNightParkingHalf = minPricePerNightParkingHalf;
        PricePerAdultParkingHalf = pricePerAdultParkingHalf;
        IncludedAdultsParkingHalf = includedAdultsParkingHalf;

        SaunaPrice = saunaPrice;
        BanquetHallPrice = banquetHallPrice;
        DogFee = dogFee;

        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Update(
        int minPricePerNightFullHouse,
        int pricePerAdultFullHouse,
        int minPricePerNightPondHalf,
        int pricePerAdultPondHalf,
        int minPricePerNightParkingHalf,
        int pricePerAdultParkingHalf,
        int saunaPrice,
        int banquetHallPrice,
        int dogFee,
        int includedAdultsFullHouse,
        int includedAdultsPondHalf,
        int includedAdultsParkingHalf,
        string? updatedBy = null)
    {
        MinPricePerNightFullHouse = minPricePerNightFullHouse;
        PricePerAdultFullHouse = pricePerAdultFullHouse;
        IncludedAdultsFullHouse = includedAdultsFullHouse;

        MinPricePerNightPondHalf = minPricePerNightPondHalf;
        PricePerAdultPondHalf = pricePerAdultPondHalf;
        IncludedAdultsPondHalf = includedAdultsPondHalf;

        MinPricePerNightParkingHalf = minPricePerNightParkingHalf;
        PricePerAdultParkingHalf = pricePerAdultParkingHalf;
        IncludedAdultsParkingHalf = includedAdultsParkingHalf;

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