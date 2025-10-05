using CineReview.Client.Features.Movies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddMemoryCache();
builder.Services.Configure<TmdbOptions>(builder.Configuration.GetSection(TmdbOptions.SectionName));

var tmdbOptions = builder.Configuration.GetSection(TmdbOptions.SectionName).Get<TmdbOptions>() ?? new TmdbOptions();
if (!string.IsNullOrWhiteSpace(tmdbOptions.ApiKey) || !string.IsNullOrWhiteSpace(tmdbOptions.AccessToken))
{
    builder.Services.AddHttpClient<IMovieDataProvider, TmdbMovieDataProvider>();
}
else
{
    builder.Services.AddSingleton<IMovieDataProvider, SampleMovieDataProvider>();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
