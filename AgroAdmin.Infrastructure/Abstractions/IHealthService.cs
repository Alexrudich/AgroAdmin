using AgroAdmin.Shared.Dto.Health;

namespace AgroAdmin.Infrastructure.Abstractions
{
    public interface IHealthService
    {
        Task<HealthCheckDto> CheckAllAsync();
        Task<string> FormatForTelegramAsync();
    }
}
