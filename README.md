# CineReview

## Yêu cầu môi trường
- .NET SDK 9.0
- Tài khoản [TheMovieDB](https://developer.themoviedb.org/docs) để lấy API key hoặc Read Access Token.

## Cấu hình TMDB API
1. Lấy "API Read Access Token (v4 auth)" hoặc "API Key (v3 auth)" từ trang tài khoản TMDB.
2. Trong thư mục `CineReview`, thiết lập secret cho môi trường dev:
	```bash
	cd CineReview
	dotnet user-secrets init
	dotnet user-secrets set "Tmdb:AccessToken" "<your_v4_token>"
	# hoặc nếu dùng API key v3:
	dotnet user-secrets set "Tmdb:ApiKey" "<your_v3_key>"
	```
3. Có thể tuỳ chỉnh ngôn ngữ/vùng trong `appsettings.Development.json` (`Tmdb:DefaultLanguage`, `Tmdb:DefaultRegion`).

## Chạy ứng dụng
```bash
cd CineReview
dotnet restore
dotnet run
```

Sau khi chạy, truy cập `https://localhost:5001` (hoặc cổng được hiển thị) để xem giao diện.