# Hướng dẫn chuẩn bị implement: Blazor ASP.NET Core (.NET 9/10) cho nền tảng review phim (TMDB)

Ngày cập nhật: 2025-09-30

Tài liệu tham chiếu:
- Blazor tổng quan & bảo mật: https://learn.microsoft.com/aspnet/core/blazor/?view=aspnetcore-9.0, https://learn.microsoft.com/aspnet/core/blazor/security/?view=aspnetcore-9.0
- IHttpClientFactory & Polly: https://learn.microsoft.com/aspnet/core/fundamentals/http-requests?view=aspnetcore-9.0
- Rate Limiting middleware: https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-9.0
- Output Caching: https://learn.microsoft.com/aspnet/core/performance/caching/output?view=aspnetcore-9.0
- In-memory cache: https://learn.microsoft.com/aspnet/core/performance/caching/memory?view=aspnetcore-9.0
- App Secrets: https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-9.0
- Minimal APIs: https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/overview?view=aspnetcore-9.0
- EF Core: https://learn.microsoft.com/ef/core/
- TMDB Auth & endpoints: https://developer.themoviedb.org/docs/authentication-application, https://developer.themoviedb.org/reference/movie-details, /discover/movie, /movie/now_playing, /configuration, /movie-credits

## 1) Kiến trúc tổng quan
- Chọn Blazor Web App (SSR + optional interactivity), hoặc Blazor Server thuần cho giai đoạn đầu để kiểm soát bảo mật dễ hơn.
- Tách lớp gọi TMDB qua HttpClientFactory (typed client `TmdbClient`).
- Backend nội bộ cung cấp Minimal APIs cho resource riêng (review, ticket-verify, users). Client Blazor gọi các API nội bộ, không gọi TMDB trực tiếp từ browser ở chế độ SSR để bảo vệ token.
- Dùng Output Caching cho endpoint public (trending, now_playing) và IMemoryCache/Redis cho layer dịch vụ.
- Rate limiting áp dụng cho API public (comment, search) để chống lạm dụng.
- Secrets (TMDB token, DB connection) bằng User Secrets trong dev và Key Vault/managed identity cho prod.

Sơ đồ khối rút gọn:
Blazor (UI) -> Services (TmdbService, ReviewService) -> TMDB API (read-only)
                                   -> DB (EF Core: Reviews, Tickets)
                                   -> Partner API (CGV verify)

## 2) Cấu trúc solution đề xuất
- src/
  - ReviewMovies.Web (Blazor Web App)
    - Components/Pages: Home (NowPlaying, Popular), MovieDetail, WriteReview
    - Services: ITmdbClient, IReviewApi, ITicketApi
    - HttpClients config, Polly policies, OutputCache, RateLimit
  - ReviewMovies.Api (Minimal APIs cho review, ticket)
  - ReviewMovies.Domain (entities: Review, Ticket; rules)
  - ReviewMovies.Infrastructure (EF Core DbContext, Repositories, Migrations)
- tests/
  - UnitTests (rules gating review), IntegrationTests (API)

Có thể bắt đầu all-in-one project rồi tách dần.

## 3) Packages/NuGet
- Microsoft.AspNetCore.Components.Authorization (Blazor auth)
- Microsoft.Extensions.Http.Polly (Polly cho HttpClient)
- Microsoft.AspNetCore.OutputCaching
- Microsoft.AspNetCore.RateLimiting
- Microsoft.EntityFrameworkCore + Provider (SqlServer/Sqlite) + Tools
- Microsoft.AspNetCore.HeaderPropagation (nếu cần)
- Swashbuckle.AspNetCore (tùy chọn cho API nội bộ)

## 4) Cấu hình secrets & appsettings
- Dev (User Secrets):
  - Movies:ServiceApiKey (nếu có), TMDB:BearerToken, ConnectionStrings:Default
- Prod: dùng Key Vault hoặc biến môi trường bảo mật. Không nhúng token vào client.

CLI mẫu (Windows PowerShell):
```powershell
cd .\src\ReviewMovies.Web
dotnet user-secrets init
dotnet user-secrets set "TMDB:BearerToken" "<YOUR_TMDB_V4_READ_TOKEN>"
```

Trong code đọc: `builder.Configuration["TMDB:BearerToken"]`.

## 5) HttpClientFactory + Polly + TMDB
Đăng ký typed client:
```csharp
builder.Services.AddHttpClient<TmdbClient>(http =>
{
    http.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    var token = builder.Configuration["TMDB:BearerToken"];
    if (!string.IsNullOrWhiteSpace(token))
        http.DefaultRequestHeaders.Authorization = new("Bearer", token);
})
// Retry ngắn + CircuitBreaker
.AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3, n => TimeSpan.FromMilliseconds(200 * n)))
.AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

Interface/methods tối thiểu:
- GetConfiguration()
- GetNowPlaying(region, page)
- GetMovieDetails(id, appendToResponse)
- GetCredits(id)
- SearchMovies(query, page)

Lưu ý: tôn trọng 429 từ TMDB, backoff/retry hợp lý.

## 6) Output Cache & In-memory cache
- Bật middleware OutputCache và policy cho endpoint công khai (home, danh sách phim) với TTL 10–60s để giảm tải TMDB.
- Ở service, với dữ liệu tĩnh như configuration/images: cache IMemoryCache vài giờ, nhưng tuân thủ điều khoản TMDB (không cache > 6 tháng; config an toàn có thể cache lâu hơn nhưng nên refresh định kỳ).

Ví dụ bật OutputCache:
```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(p => p.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("NowPlaying10", p => p.Expire(TimeSpan.FromSeconds(10)));
});
app.UseOutputCache();

app.MapGet("/api/movies/now-playing", async (TmdbClient tmdb, string? region) =>
{
    return await tmdb.GetNowPlaying(region ?? "US", 1);
}).CacheOutput("NowPlaying10");
```

## 7) Rate limiting
Áp dụng limiter cố định cho API nhạy cảm (post review) và toàn site:
```csharp
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 50,
                Window = TimeSpan.FromSeconds(10),
                QueueLimit = 0
            }));
});
app.UseRateLimiter();
```
Có thể dùng [EnableRateLimiting] cho từng component/page hoặc endpoint.

## 8) Identity/Authorization
- Dùng Individual Accounts nếu cần user management đầy đủ.
- Trên component/page, dùng `[Authorize]` để chặn truy cập màn viết review, kết hợp rule nghiệp vụ ở server khi submit.
- Không tin client; tất cả rule gating phải kiểm tra lại ở API server.

## 9) Domain rules (gating review) – triển khai
Service `ReviewPolicy`:
- IsTicketRequired(tmdbMovieId, nowPlayingFlag, lastTheatricalDate)
- CanUserReview(userId, tmdbMovieId)
- MarkTicketVerified(userId, ticket)

API luồng:
- POST /api/tickets/verify { provider, externalTicketId, movieId, showtime }
  - Gọi provider adapter -> trả về valid. Nếu valid, lưu Tickets và trả về {verified:true}.
- POST /api/reviews { movieId, rating, title, content, spoiler }
  - Server kiểm tra rule: nếu cần vé mà user chưa verified -> 403.

## 10) EF Core
- DbContext: Reviews, Tickets, Users (nếu dùng Identity thì context riêng, có thể chia schema).
- Migration và Seed dữ liệu ban đầu (nếu cần).
- Lưu ý hiệu năng: index (userId,movieId), unique (userId,movieId) cho review chính; unique (userId, externalTicketId) cho vé.

## 11) UI/UX gợi ý
- Trang chủ: Now Playing, Popular, Trending.
- Trang phim: chi tiết, trailer, cast, nút Viết review (disabled nếu chưa đủ điều kiện, hover hiển thị lý do).
- Modal nhập mã vé (đang chiếu hoặc <1 năm): gọi verify API.
- Badge "Đã xác thực vé" cạnh tên người dùng ở review.
- Cảnh báo spoiler.

## 12) Tuân thủ TMDB
- Hiển thị attribution bắt buộc ở footer/trang about:
  - "This product uses the TMDB API but is not endorsed or certified by TMDB" + logo TMDB.
- Không dùng nội dung TMDB để huấn luyện AI/LLM; không cache > 6 tháng; không dùng như image hosting.

## 13) Observability
- Logging HttpClient (category System.Net.Http.HttpClient.TmdbClient.*) cấp Information+
- Metrics rate limiting (Microsoft.AspNetCore.RateLimiting)
- Structured logging cho hành vi verify ticket và post review.

## 14) Bảo mật
- Secrets qua User Secrets/Key Vault; tuyệt đối không đặt token trong client.
- Antiforgery đã có trong Blazor template; sử dụng khi form post.
- Input validation, size limit (ảnh vé nếu upload), chống XSS.

## 15) Bước khởi tạo nhanh (CLI)
```powershell
# Tạo solution mẫu (tuỳ chọn)
dotnet new sln -n ReviewMovies

# Web (Blazor) và API (tuỳ chọn tách riêng)
dotnet new blazor -n ReviewMovies.Web -o .\src\ReviewMovies.Web
# hoặc Blazor Server: dotnet new blazorserver -n ReviewMovies.Web -o .\src\ReviewMovies.Web

# API tối giản
cd .\src
mkdir ReviewMovies.Api
cd ReviewMovies.Api
dotnet new web -n ReviewMovies.Api

# Thêm gói chính
cd ..\ReviewMovies.Web
dotnet add package Microsoft.Extensions.Http.Polly
dotnet add package Microsoft.AspNetCore.OutputCaching
dotnet add package Microsoft.AspNetCore.RateLimiting

# EF Core (ở Infrastructure nếu có)
# dotnet add package Microsoft.EntityFrameworkCore.SqlServer
# dotnet add package Microsoft.EntityFrameworkCore.Tools
```

## 16) Code mẫu gọi TMDB (typed client rút gọn)
```csharp
public sealed class TmdbClient
{
    private readonly HttpClient _http;
    public TmdbClient(HttpClient http) => _http = http;

    public async Task<JsonDocument> GetConfigurationAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsync<JsonDocument>("configuration", ct)
           ?? throw new IOException("No TMDB config");

    public async Task<JsonDocument> GetNowPlayingAsync(string region = "US", int page = 1, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<JsonDocument>($"movie/now_playing?region={Uri.EscapeDataString(region)}&page={page}", ct)
           ?? throw new IOException("No now playing");

    public async Task<JsonDocument> GetMovieDetailsAsync(int id, string? append = null, CancellationToken ct = default)
    {
        var url = append is null ? $"movie/{id}" : $"movie/{id}?append_to_response={Uri.EscapeDataString(append)}";
        return await _http.GetFromJsonAsync<JsonDocument>(url, ct)
           ?? throw new IOException("No movie details");
    }
}
```

## 17) Kiểm thử và chất lượng
- Unit test cho `ReviewPolicy` với 3 tình huống: đang chiếu (cần vé), ngừng chiếu <1 năm (cần vé), ngừng chiếu ≥1 năm (không cần vé).
- Integration test API: POST /tickets/verify, POST /reviews.
- Load test rate limiting cho endpoints công khai.

## 18) Triển khai
- Reverse proxy (Nginx/IIS/Apache) + HTTPS.
- Nếu scale-out: dùng Redis OutputCache provider để đồng bộ cache giữa instances.
- Giám sát 429/5xx, log quota TMDB.

---
Tài liệu này bám theo tài liệu chính thức Microsoft (.NET 9) và TMDB (v3) cập nhật tới 2025-09, sẵn sàng cho đội dev bắt tay vào hiện thực hóa. 
