using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.NotificationWorker.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var builder = Host.CreateApplicationBuilder(args);

// 1. База данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Инфраструктура
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITelegramService, TelegramService>();

// 3. Планировщик Quartz
builder.Services.AddQuartz(q => {
    // Задача напоминания (создается динамически из RabbitMQ)
    q.AddJob<ReminderJob>(opts => opts.WithIdentity("ReminderJob").StoreDurably());

    // Задача сканирования базы (запускается по расписанию)
    var scannerKey = new JobKey("DatabaseScannerJob");
    q.AddJob<DatabaseScannerJob>(opts => opts.WithIdentity(scannerKey));

    q.AddTrigger(opts => opts
        .ForJob(scannerKey)
        .WithIdentity("DatabaseScannerTrigger")
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(60).RepeatForever()));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 4. Очереди MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitUrl = builder.Configuration["RabbitMQ:Url"]
                        ?? builder.Configuration["RabbitMQ__Url"]
                        ?? "amqp://guest:guest@rabbitmq:5672"; // Fallback для Docker

        cfg.Host(new Uri(rabbitUrl.Trim().TrimEnd('/')));
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();