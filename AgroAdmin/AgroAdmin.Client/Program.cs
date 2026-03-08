using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AgroAdmin.Shared.Services;

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

            await builder.Build().RunAsync();
        }
    }
}