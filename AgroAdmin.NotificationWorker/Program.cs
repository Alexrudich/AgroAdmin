using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Services;
using MassTransit;
using Quartz;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var builder = Host.CreateApplicationBuilder(args);

// --- ОТЛАДКА ПЕРЕМЕННЫХ ---
var url = builder.Configuration["RabbitMQ:Url"]
          ?? builder.Configuration["RabbitMQ__Url"]
          ?? Environment.GetEnvironmentVariable("RabbitMQ__Url");

// --- НАСТРОЙКА QUARTZ ---
builder.Services.AddQuartz(q => {
    q.AddJob<AgroAdmin.NotificationWorker.Jobs.ReminderJob>(opts => opts
        .WithIdentity("ReminderJob")
        .StoreDurably());
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// --- НАСТРОЙКА MASSTRANSIT ---
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        if (string.IsNullOrEmpty(url))
        {
            // Не падаем сразу, а пробуем дефолт для Docker, если мы внутри сети
            url = "amqp://guest:guest@rabbitmq:5672";
            Console.WriteLine("⚠️ WARNING: Config URL is empty. Using fallback: " + url);
        }

        cfg.Host(new Uri(url.Trim().TrimEnd('/')));
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITelegramService, TelegramService>();

var host = builder.Build();
host.Run();