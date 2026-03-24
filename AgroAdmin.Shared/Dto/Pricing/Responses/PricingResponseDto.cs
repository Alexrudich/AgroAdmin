namespace AgroAdmin.Shared.Dto.Pricing.Responses;

public class PricingResponseDto
{
    public decimal TotalPrice { get; set; }
    public decimal PricePerNight { get; set; }
    public int NightsCount { get; set; }
    public List<PricingDetailDto> Details { get; set; } = new();
    public string SuggestionText { get; set; } = string.Empty;
}

