using AgroAdmin.Client.Services;
using AgroAdmin.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace AgroAdmin.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddScoped(sp => new HttpClient
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });

            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<BookingFormService>();
            builder.Services.AddScoped<NotificationApiClient>();
            await builder.Build().RunAsync();
        }
    }
}