using AgroAdmin.Shared.Dto.Pricing;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;

namespace AgroAdmin.Infrastructure.Abstractions;

public interface IPricingStrategy
{
    string Name { get; }
    Task<PricingDetailDto?> CalculateAsync(PricingRequestDto request, PricingConfigurationDto config);
}