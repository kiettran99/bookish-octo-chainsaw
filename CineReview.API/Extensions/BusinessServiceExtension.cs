using CineReview.Application.Implements.Infrastructures;
using CineReview.Application.Interfaces.Infrastructures;

namespace CineReview.API.Extensions;

public static class BusinessServiceExtension
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        // Inject Services
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ICommunicationScoreService, CommunicationScoreService>();

        return services;
    }
}
