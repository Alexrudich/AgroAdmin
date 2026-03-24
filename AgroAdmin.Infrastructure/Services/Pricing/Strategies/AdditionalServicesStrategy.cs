using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Pricing;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;

namespace AgroAdmin.Infrastructure.Services.Pricing.Strategies;

public class AdditionalServicesStrategy : IPricingStrategy
{
    public string Name => "Дополнительные услуги";

    public Task<PricingDetailDto?> CalculateAsync(PricingRequestDto request, PricingConfigurationDto config)
    {
        decimal total = 0;
        var details = new List<string>();

        if (request.NeedsSauna && config.SaunaPrice > 0)
        {
            total += config.SaunaPrice;
            details.Add("Сауна");
        }

        if (request.NeedsBanquetHall && config.BanquetHallPrice > 0)
        {
            total += config.BanquetHallPrice;
            details.Add("Банкетный зал");
        }

        if (request.HasDog && config.DogFee > 0)
        {
            total += config.DogFee;
            details.Add("Собака");
        }

        if (total == 0) return Task.FromResult<PricingDetailDto?>(null);

        return Task.FromResult<PricingDetailDto?>(new PricingDetailDto
        {
            Name = string.Join(", ", details),
            Amount = total
        });
    }
}