using AgroAdmin.Infrastructure.Persistence;
using AgroAdmin.NotificationWorker.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

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