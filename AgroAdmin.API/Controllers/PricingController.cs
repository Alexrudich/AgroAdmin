using AgroAdmin.Infrastructure.Services;
using AgroAdmin.Infrastructure.Services.Pricing;
using AgroAdmin.Shared.Dto.Pricing.Requests;
using AgroAdmin.Shared.Dto.Pricing.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroAdmin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PricingController(PricingEngine pricingEngine, PricingConfigurationService configService)
    : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<ActionResult<PricingResponseDto>> Calculate([FromBody] PricingRequestDto request)
    {
        var config = await configService.GetActiveConfigurationDtoAsync();

        if (!config.IsEnabled)
        {
            return Ok(new PricingResponseDto
            {
                TotalPrice = 0,
                PricePerNight = 0,
                NightsCount = 0,
                SuggestionText = "Калькуляция цен отключена"
            });
        }

        var result = await pricingEngine.CalculateAsync(request, config);
        return Ok(result);
    }
}