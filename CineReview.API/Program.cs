using System.Text.Json;
using CineReview.API.Extensions;
using CineReview.API.Middlewares;
using Common.Models;
using Hangfire;
using Hangfire.Dashboard.BasicAuthorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Camel Case return JSON response
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerServices();
builder.Services.AddPortalServices(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddCors();
builder.Services.AddHangFireServices(builder.Configuration);

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ => new ValidateModelActionResult();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerDocumentation();
}

app.UseHttpsRedirection();

app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthorization();

app.UseHangfireDashboard(options: new DashboardOptions
{
    Authorization =
    [
        new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
        {
            RequireSsl = false,
            SslRedirect = false,
            LoginCaseSensitive = true,
            Users =
            [
                new BasicAuthAuthorizationUser
                {
                    Login = "hangfire",
                    Password = [0x2e,0x31,0x9a,0xee,0x2e,0xf7,0x63,0x67,0xf1,0x42,0x0b,0x75,0x1a,0xce,0x38,0x27,0x12,0x15,0x67,0x48]
                }
            ]
        })
    ]
});

app.UseCors(x => x
    .SetIsOriginAllowed(origin => origin.Contains("localhost") || origin.Contains("127.0.0.1") || origin.EndsWith(".github.io") || origin.EndsWith(".technewsz.com"))
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

app.MapControllers();

app.Run();