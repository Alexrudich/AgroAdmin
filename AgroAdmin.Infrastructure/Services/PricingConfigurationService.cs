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
            // Возвращаем дефолтную конфигурацию, если в БД ничего нет
            return GetDefaultConfiguration();
        }

        return new PricingConfigurationDto
        {
            Id = config.Id,
            IsEnabled = config.IsEnabled,
            MinPricePerNightFullHouse = config.MinPricePerNightFullHouse,
            PricePerAdultFullHouse = config.PricePerAdultFullHouse,
            IncludedAdultsFullHouse = config.IncludedAdultsFullHouse,
            MinPricePerNightPondHalf = config.MinPricePerNightPondHalf,
            PricePerAdultPondHalf = config.PricePerAdultPondHalf,
            IncludedAdultsPondHalf = config.IncludedAdultsPondHalf,
            MinPricePerNightParkingHalf = config.MinPricePerNightParkingHalf,
            PricePerAdultParkingHalf = config.PricePerAdultParkingHalf,
            IncludedAdultsParkingHalf = config.IncludedAdultsParkingHalf,
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
            MinPricePerNightPondHalf = 150,
            PricePerAdultPondHalf = 40,
            IncludedAdultsPondHalf = 2,
            MinPricePerNightParkingHalf = 180,
            PricePerAdultParkingHalf = 45,
            IncludedAdultsParkingHalf = 2,
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
                dto.MinPricePerNightPondHalf,
                dto.PricePerAdultPondHalf,
                dto.MinPricePerNightParkingHalf,
                dto.PricePerAdultParkingHalf,
                dto.SaunaPrice,
                dto.BanquetHallPrice,
                dto.DogFee,
                dto.IncludedAdultsFullHouse,
                dto.IncludedAdultsPondHalf,
                dto.IncludedAdultsParkingHalf,
                updatedBy);

            context.PricingConfigurations.Add(newConfig);
            await context.SaveChangesAsync();
            return newConfig;
        }

        existing.Update(
            dto.MinPricePerNightFullHouse,
            dto.PricePerAdultFullHouse,
            dto.MinPricePerNightPondHalf,
            dto.PricePerAdultPondHalf,
            dto.MinPricePerNightParkingHalf,
            dto.PricePerAdultParkingHalf,
            dto.SaunaPrice,
            dto.BanquetHallPrice,
            dto.DogFee,
            dto.IncludedAdultsFullHouse,
            dto.IncludedAdultsPondHalf,
            dto.IncludedAdultsParkingHalf,
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
}