using System.Text.Json;
using CineReview.API.Extensions;
using CineReview.API.Middlewares;
using Common.Models;
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

app.UseCors(x => x
    .SetIsOriginAllowed(origin => origin.Contains("localhost") || origin.Contains("127.0.0.1") || origin.EndsWith(".github.io") || origin.EndsWith(".codegota.me"))
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials());

app.MapControllers();

app.Run();