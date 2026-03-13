using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.NotificationWorker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var builder = Host.CreateApplicationBuilder(args);

// 1. База данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Инфраструктура
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITelegramService, TelegramService>();

// 4. Очереди MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitUrl = builder.Configuration["RabbitMQ:Url"]
                        ?? builder.Configuration["RabbitMQ__Url"]
                        ?? "amqp://guest:guest@rabbitmq:5672";

        cfg.Host(new Uri(rabbitUrl.Trim().TrimEnd('/')));
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

host.Run();