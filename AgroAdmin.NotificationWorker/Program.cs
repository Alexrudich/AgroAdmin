using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Services;
using MassTransit;
using Quartz;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var builder = Host.CreateApplicationBuilder(args);

// 1. Настройка Quartz
builder.Services.AddQuartz(q => {
    q.AddJob<AgroAdmin.NotificationWorker.Jobs.ReminderJob>(opts => opts.WithIdentity("ReminderJob"));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 2. Настройка MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitUrl = builder.Configuration["RabbitMQ:Url"];
        cfg.Host(new Uri(rabbitUrl!));
        cfg.ConfigureEndpoints(context);
    });
});

// 3. Регистрация ОРИГИНАЛЬНОГО сервиса из Infrastructure
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITelegramService, TelegramService>();

var host = builder.Build();
host.Run();