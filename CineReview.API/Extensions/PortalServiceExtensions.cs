using System.Security.Authentication;
using CineReview.Infrastructure.Messaging.Comsumers;
using Common.Implements.Messaging;
using Common.Interfaces.Messaging;
using Common.SeedWork;
using Email.Models;
using Email.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Portal.Domain.Interfaces.Infrastructures;
using Portal.Infrastructure;
using Portal.Infrastructure.Implements.Infrastructures;
using Portal.Infrastructure.SeedWork;

namespace CineReview.API.Extensions;

public static class PortalServiceExtensions
{
    public static IServiceCollection AddPortalServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(opt =>
        {
            opt.UseLazyLoadingProxies().UseSqlite(config.GetConnectionString("PortalConnection"));
            opt.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.NonTransactionalMigrationOperationWarning, RelationalEventId.PendingModelChangesWarning));
        });

        // services.AddStackExchangeRedisCache(options =>
        // {
        //     options.Configuration = config.GetConnectionString("RedisConnection");
        //     options.InstanceName = "Portal";
        // });
        // services.AddDistributedMemoryCache();

        //     services.AddScoped<IRedisService>(x => new RedisService(x.GetRequiredService<IDistributedCache>(), new RedisOptions
        //     {
        //         ConnectionString = config.GetConnectionString("RedisConnection") ?? string.Empty,
        //         Host = config.GetSection("RedisSettings").GetValue<string>("Host") ?? string.Empty,
        //         Port = config.GetSection("RedisSettings").GetValue<string>("Port") ?? string.Empty,
        //         InstanceName = "Portal"
        //     }));

        services.AddMassTransit(x =>
       {
           x.AddConsumer<SendEmailComsumer>();
           x.AddConsumer<SyncUserPortalComsumer>();
           x.AddConsumer<ServiceLogComsumer>();

           x.UsingRabbitMq((context, cfg) =>
           {
               cfg.Host(config.GetSection("RabitMQSettings").GetValue<string>("Hostname"), 5671, config.GetSection("RabitMQSettings").GetValue<string>("VHost"), h =>
               {
                   h.Username(config.GetSection("RabitMQSettings").GetValue<string>("Username")!);
                   h.Password(config.GetSection("RabitMQSettings").GetValue<string>("Password")!);
                   h.UseSsl(s =>
                   {
                       s.Protocol = SslProtocols.Tls12;
                   });
               });

               cfg.ConfigureEndpoints(context);
           });
       });

        // var mongoDbConnectionString = config.GetSection("MongoDbSettings").GetValue<string>("ConnectionString")!;
        // var mongoDbCollectionName = config.GetSection("MongoDbSettings").GetValue<string>("CollectionName")!;

        // // Configure Serilog
        // Log.Logger = new LoggerConfiguration()
        //     .MinimumLevel.Information()
        //     .WriteTo.MongoDB(mongoDbConnectionString, collectionName: mongoDbCollectionName)
        //     .CreateLogger();

        // Portal registers publishers for MassTransit
        services.AddScoped<ISendMailPublisher, SendMailPublisher>();
        services.AddScoped<IServiceLogPublisher, ServiceLogPublisher>();

        // Inject Services
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();

        #region Email Service
        var appSettingsConfig = config.GetSection("SmtpSettings");
        var options = new EmailOptions
        {
            Environment = appSettingsConfig.GetValue<string>("Environment"),
            SmtpServer = appSettingsConfig.GetValue<string>("Host"),
            SmtpPort = appSettingsConfig.GetValue<int>("Port"),
            SmtpUser = appSettingsConfig.GetValue<string>("Username"),
            SmtpPassword = appSettingsConfig.GetValue<string>("Password"),
            MailFrom = appSettingsConfig.GetValue<string>("SenderEmail"),
            SenderName = appSettingsConfig.GetValue<string>("SenderName"),
        };
        services.AddScoped<IEmailService>(x =>
            new EmailService(options)
        );
        #endregion

        return services;
    }
}