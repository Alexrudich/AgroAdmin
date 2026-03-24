using System.Net.Http.Json;
using AgroAdmin.Shared.Dto.Pricing;

namespace AgroAdmin.Shared.Services;

public class PricingConfigClientService(HttpClient http)
{
    public async Task<PricingConfigurationDto?> GetConfigurationAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<PricingConfigurationDto>("api/pricing/config");
        }
        catch
        {
            return null;
        }
    }

    public async Task<PricingConfigurationDto?> UpdateConfigurationAsync(PricingConfigurationDto config)
    {
        try
        {
            var response = await http.PutAsJsonAsync("api/pricing/config", config);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PricingConfigurationDto>();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}