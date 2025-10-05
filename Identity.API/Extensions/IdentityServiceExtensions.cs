using System.Security.Authentication;
using Common.SeedWork;
using Identity.Domain.AggregatesModel.RoleAggregates;
using Identity.Domain.AggregatesModel.UserAggregates;
using Identity.Domain.Interfaces.Infrastructures;
using Identity.Domain.Interfaces.Messagings;
using Identity.Domain.Interfaces.Services;
using Identity.Infrastructure;
using Identity.Infrastructure.Implements.Infrastructures;
using Identity.Infrastructure.Implements.Messagings;
using Identity.Infrastructure.Implements.Services;
using Identity.Infrastructure.SeedWork;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Extensions;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseLazyLoadingProxies().UseSqlite(config.GetConnectionString("IdentityConnection")));
        services.AddDataProtection();

        services.AddIdentityCore<User>(_ =>
           {
               // add identity options here
           })
           .AddRoles<Role>()
           .AddEntityFrameworkStores<ApplicationDbContext>()
           .AddSignInManager<SignInManager<User>>()
           .AddRoleManager<RoleManager<Role>>()
           .AddDefaultTokenProviders();

        // Authenticate with Google
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddGoogle(options =>
        {
            options.ClientId = config.GetValue<string>("Authentication:Google:ClientId")!;
            options.ClientSecret = config.GetValue<string>("Authentication:Google:ClientSecret")!;
        });

        // services.AddMassTransit(x =>
        // {
        //     x.UsingRabbitMq((context, cfg) =>
        //     {
        //         cfg.Host(config.GetSection("RabitMQSettings").GetValue<string>("Hostname"), 5671, config.GetSection("RabitMQSettings").GetValue<string>("VHost"), h =>
        //         {
        //             h.Username(config.GetSection("RabitMQSettings").GetValue<string>("Username")!);
        //             h.Password(config.GetSection("RabitMQSettings").GetValue<string>("Password")!);
        //             h.UseSsl(s =>
        //             {
        //                 s.Protocol = SslProtocols.Tls12;
        //             });
        //         });

        //         cfg.ConfigureEndpoints(context);
        //     });
        // });

        // Inject Services
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserService, UserService>();

        // Inject Publisher
        // services.AddScoped<ISyncUserPortalPublisher, SyncUserPortalPublisher>();

        return services;
    }
}