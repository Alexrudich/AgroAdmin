using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Shared.Dto.Telegram.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace AgroAdmin.Infrastructure.Services
{
    public class TelegramApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TelegramApiClient> logger)
        : ITelegramApiClient
    {
        private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
        private readonly string _apiUrl = configuration["ApiUrl"] ?? "http://localhost:8080";

        public async Task<List<TelegramBookingDto>> GetNearestBookingsAsync(int days)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<TelegramBookingDto>>(
                    $"{_apiUrl}/api/bookings/nearest?days={days}") ?? new();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get nearest bookings");
                return new();
            }
        }

        public async Task<List<DailyAvailability>> GetAvailabilityAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<DailyAvailability>>(
                    $"{_apiUrl}/api/bookings/availability?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}") ?? new();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get availability");
                return new();
            }
        }
    }
}
