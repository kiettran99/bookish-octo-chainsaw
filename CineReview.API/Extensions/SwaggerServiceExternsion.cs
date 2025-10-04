using Microsoft.OpenApi.Models;

namespace CineReview.API.Extensions;

public static class SwaggerServiceExternsion
{
    private static readonly string[] value = ["Bearer"];

    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
                   {
                       c.SwaggerDoc("v1", new OpenApiInfo { Title = "Portal API", Version = "v1" });

                       var securitySchema = new OpenApiSecurityScheme
                       {
                           Description = "JWT Auth Bearer Scheme",
                           Name = "Authorization",
                           In = ParameterLocation.Header,
                           Type = SecuritySchemeType.Http,
                           Scheme = "Bearer",
                           Reference = new OpenApiReference
                           {
                               Type = ReferenceType.SecurityScheme,
                               Id = "Bearer"
                           }
                       };

                       c.AddSecurityDefinition("Bearer", securitySchema);
                       var securityRequirement = new OpenApiSecurityRequirement { { securitySchema, value } };
                       c.AddSecurityRequirement(securityRequirement);
                   });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));

        return app;
    }
}