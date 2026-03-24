using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Pricing;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;

namespace AgroAdmin.Infrastructure.Services.Pricing;

public class PricingEngine(IEnumerable<IPricingStrategy> strategies)
{
    public async Task<PricingResponseDto> CalculateAsync(PricingRequestDto request, PricingConfigurationDto config)
    {
        var nights = (request.DepartureDate - request.ArrivalDate).Days;
        if (nights <= 0) nights = 1;

        decimal totalPerNight = 0;
        decimal totalOneTime = 0;
        var details = new List<PricingDetailDto>();

        foreach (var strategy in strategies)
        {
            var detail = await strategy.CalculateAsync(request, config);

            if (detail != null)
            {
                details.Add(detail);

                // Определяем тип услуги по имени стратегии
                if (strategy.Name == "Проживание")
                {
                    totalPerNight += detail.Amount;
                }
                else
                {
                    totalOneTime += detail.Amount;
                }
            }
        }

        var totalPrice = (totalPerNight * nights) + totalOneTime;

        var suggestionText = $"{totalPerNight:F0} BYN/ночь × {nights} ночей = {totalPerNight * nights:F0} BYN";

        if (totalOneTime > 0)
        {
            suggestionText += $" + разово: {totalOneTime:F0} BYN = {totalPrice:F0} BYN";
        }

        return new PricingResponseDto
        {
            TotalPrice = totalPrice,
            PricePerNight = totalPerNight,
            NightsCount = nights,
            Details = details,
            SuggestionText = suggestionText
        };
    }
}