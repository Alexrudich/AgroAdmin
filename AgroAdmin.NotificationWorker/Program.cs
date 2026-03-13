using AgroAdmin.Infrastructure.Abstractions;
using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.Infrastructure.Services;
using AgroAdmin.NotificationWorker.Consumers;
using AgroAdmin.NotificationWorker.Jobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;
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
    // Задача сканирования базы (запускается по расписанию)
    var scannerKey = new JobKey("DatabaseScannerJob");
    q.AddJob<DatabaseScannerJob>(opts => opts.WithIdentity(scannerKey).StoreDurably());

    q.AddTrigger(opts => opts
        .ForJob(scannerKey)
        .WithIdentity("DatabaseScannerTrigger")
        .StartNow()
        .WithSimpleSchedule(x => x.WithIntervalInSeconds(30).RepeatForever()));

    // Задача напоминания (создается динамически из RabbitMQ)
    q.AddJob<ReminderJob>(opts => opts.WithIdentity("ReminderJob").StoreDurably());
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
                        ?? "amqp://guest:guest@rabbitmq:5672";

        cfg.Host(new Uri(rabbitUrl.Trim().TrimEnd('/')));
        cfg.ConfigureEndpoints(context);
    });
});

// === ДИАГНОСТИКА ДО Build() ===
Console.WriteLine("===== CONFIG CHECK =====");
Console.WriteLine($"RabbitMQ:Url: {builder.Configuration["RabbitMQ:Url"]}");
Console.WriteLine($"RabbitMQ__Url: {builder.Configuration["RabbitMQ__Url"]}");
Console.WriteLine($"Connection string: {builder.Configuration.GetConnectionString("DefaultConnection")?.Substring(0, 30)}...");
Console.WriteLine("========================");

var host = builder.Build();

// === ДИАГНОСТИКА ПОСЛЕ Build() НО ДО Run() ===
try
{
    using (var scope = host.Services.CreateScope())
    {
        var schedulerFactory = scope.ServiceProvider.GetService<ISchedulerFactory>();
        if (schedulerFactory == null)
        {
            Console.WriteLine("!!!!! ISchedulerFactory not registered !!!!!");
        }
        else
        {
            var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
            var jobKeys = scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup()).GetAwaiter().GetResult();

            Console.WriteLine("===== QUARTZ DIAGNOSTICS =====");
            Console.WriteLine($"Scheduler: {scheduler.SchedulerName}");
            Console.WriteLine($"IsStarted: {scheduler.IsStarted}");
            Console.WriteLine($"Jobs found: {jobKeys.Count}");

            foreach (var key in jobKeys)
            {
                Console.WriteLine($"- {key.Name} in group {key.Group}");
                var triggers = scheduler.GetTriggersOfJob(key).GetAwaiter().GetResult();
                Console.WriteLine($"  Triggers: {triggers.Count}");
            }
            Console.WriteLine("==============================");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"!!!!! QUARTZ ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

host.Run();