using AgroAdmin.Domain.Models;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Shared.Dto.Pricing;
using Microsoft.EntityFrameworkCore;

namespace AgroAdmin.Infrastructure.Services;

public class PricingConfigurationService(AppDbContext context)
{
    public async Task<PricingConfiguration?> GetActiveConfigurationAsync()
    {
        return await context.PricingConfigurations
            .Where(p => p.IsEnabled)
            .OrderByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<PricingConfigurationDto> GetActiveConfigurationDtoAsync()
    {
        var config = await GetActiveConfigurationAsync();

        if (config == null)
        {
            return GetDefaultConfiguration();
        }

        return new PricingConfigurationDto
        {
            Id = config.Id,
            IsEnabled = config.IsEnabled,
            MinPricePerNightFullHouse = config.MinPricePerNightFullHouse,
            PricePerAdultFullHouse = config.PricePerAdultFullHouse,
            IncludedAdultsFullHouse = config.IncludedAdultsFullHouse,
            MinPricePerNightHalf = config.MinPricePerNightHalf,
            PricePerAdultHalf = config.PricePerAdultHalf,
            IncludedAdultsHalf = config.IncludedAdultsHalf,
            SaunaPrice = config.SaunaPrice,
            BanquetHallPrice = config.BanquetHallPrice,
            DogFee = config.DogFee
        };
    }

    private PricingConfigurationDto GetDefaultConfiguration()
    {
        return new PricingConfigurationDto
        {
            IsEnabled = true,
            MinPricePerNightFullHouse = 200,
            PricePerAdultFullHouse = 50,
            IncludedAdultsFullHouse = 2,
            MinPricePerNightHalf = 165, // среднее между 150 и 180
            PricePerAdultHalf = 42,      // среднее между 40 и 45
            IncludedAdultsHalf = 2,
            SaunaPrice = 100,
            BanquetHallPrice = 150,
            DogFee = 15
        };
    }

    public async Task<PricingConfiguration> CreateOrUpdateAsync(PricingConfigurationDto dto, string? updatedBy = null)
    {
        var existing = await GetActiveConfigurationAsync();

        if (existing == null)
        {
            var newConfig = new PricingConfiguration(
                dto.MinPricePerNightFullHouse,
                dto.PricePerAdultFullHouse,
                dto.MinPricePerNightHalf,
                dto.PricePerAdultHalf,
                dto.SaunaPrice,
                dto.BanquetHallPrice,
                dto.DogFee,
                dto.IncludedAdultsFullHouse,
                dto.IncludedAdultsHalf,
                updatedBy);

            context.PricingConfigurations.Add(newConfig);
            await context.SaveChangesAsync();
            return newConfig;
        }

        existing.Update(
            dto.MinPricePerNightFullHouse,
            dto.PricePerAdultFullHouse,
            dto.MinPricePerNightHalf,
            dto.PricePerAdultHalf,
            dto.SaunaPrice,
            dto.BanquetHallPrice,
            dto.DogFee,
            dto.IncludedAdultsFullHouse,
            dto.IncludedAdultsHalf,
            updatedBy);

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task EnsureDefaultConfigurationAsync()
    {
        var existing = await GetActiveConfigurationAsync();
        if (existing == null)
        {
            var defaultConfig = GetDefaultConfiguration();
            await CreateOrUpdateAsync(defaultConfig, "system");
        }
    }

    public async Task<PricingConfiguration> UpdateConfigurationAsync(PricingConfigurationDto dto, string? updatedBy = null)
    {
        return await CreateOrUpdateAsync(dto, updatedBy);
    }
}