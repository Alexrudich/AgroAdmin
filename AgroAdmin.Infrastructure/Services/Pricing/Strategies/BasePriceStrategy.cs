using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Pricing;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;
using AgroAdmin.Shared.Enums;

namespace AgroAdmin.Infrastructure.Services.Pricing.Strategies;

public class BasePriceStrategy : IPricingStrategy
{
    public string Name => "Проживание";

    public Task<PricingDetailDto?> CalculateAsync(PricingRequestDto request, PricingConfigurationDto config)
    {
        int minPrice;
        int pricePerAdult;
        int includedAdults;

        switch (request.ReservedUnit)
        {
            case ReservedUnits.PondSide:
                minPrice = config.MinPricePerNightPondHalf;
                pricePerAdult = config.PricePerAdultPondHalf;
                includedAdults = config.IncludedAdultsPondHalf;
                break;
            case ReservedUnits.ParkingSide:
                minPrice = config.MinPricePerNightParkingHalf;
                pricePerAdult = config.PricePerAdultParkingHalf;
                includedAdults = config.IncludedAdultsParkingHalf;
                break;
            case ReservedUnits.WholeHouse:
            default:
                minPrice = config.MinPricePerNightFullHouse;
                pricePerAdult = config.PricePerAdultFullHouse;
                includedAdults = config.IncludedAdultsFullHouse;
                break;
        }

        var totalPayable = request.AdultsCount + request.ChildrenCount;
        var additionalGuests = Math.Max(0, totalPayable - includedAdults);
        decimal pricePerNight = minPrice + (additionalGuests * pricePerAdult);

        if (pricePerNight == 0) return Task.FromResult<PricingDetailDto?>(null);

        return Task.FromResult<PricingDetailDto?>(new PricingDetailDto
        {
            Name = $"Проживание ({GetUnitName(request.ReservedUnit)})",
            Amount = pricePerNight
        });
    }

    private string GetUnitName(ReservedUnits unit)
    {
        return unit switch
        {
            ReservedUnits.PondSide => "Половинка у пруда",
            ReservedUnits.ParkingSide => "Половинка у парковки",
            _ => "Весь дом"
        };
    }
}