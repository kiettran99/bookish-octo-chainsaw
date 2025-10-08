using Hangfire;

namespace CineReview.API.Extensions;

public static class HangFireServiceExtension
{
    public static IServiceCollection AddHangFireServices(this IServiceCollection services, IConfiguration config)
    {
        // Add Hangfire services - In memory
        GlobalConfiguration.Configuration.UseInMemoryStorage();
        services.AddHangfire(x => x.UseInMemoryStorage());
        services.AddHangfireServer();

        return services;
    }
}
