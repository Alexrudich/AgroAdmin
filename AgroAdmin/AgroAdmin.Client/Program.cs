using AgroAdmin.Client.Handlers;
using AgroAdmin.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace AgroAdmin.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // Регистрируем CookieHandler
            builder.Services.AddTransient<CookieHandler>();

            // Настраиваем HttpClient с поддержкой кук
            builder.Services.AddHttpClient("AgroAdmin", client =>
            {
                client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
            }).AddHttpMessageHandler<CookieHandler>();

            // Оставляем старый HttpClient для обратной совместимости
            builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AgroAdmin"));

            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<BookingFormService>();
            builder.Services.AddScoped<PricingConfigClientService>();

            await builder.Build().RunAsync();
        }
    }
}