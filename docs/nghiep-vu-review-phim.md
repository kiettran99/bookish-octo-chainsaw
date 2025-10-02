# Tài liệu nghiệp vụ: Nền tảng review phim tích hợp TMDB

Ngày cập nhật: 2025-09-30

Nguồn tham chiếu chính:
- TMDB API: Getting Started, Authentication (Application/User), Rate Limiting, Movies/Discover/Now Playing/Details/Credits/Reviews/Configuration. 
  - https://developer.themoviedb.org/docs/getting-started
  - https://developer.themoviedb.org/docs/authentication-application
  - https://developer.themoviedb.org/reference/intro/authentication
  - https://developer.themoviedb.org/reference/movie-details
  - https://developer.themoviedb.org/reference/movie-now-playing-list
  - https://developer.themoviedb.org/reference/movie-credits
  - https://developer.themoviedb.org/reference/movie-reviews
  - https://developer.themoviedb.org/reference/discover-movie
  - https://developer.themoviedb.org/reference/configuration-details
- Điều khoản TMDB API & Attribution: https://www.themoviedb.org/api-terms-of-use

## 1) Bối cảnh và mục tiêu
Nền tảng cho phép người dùng xem thông tin phim, xếp hạng, để lại review/bình luận. Dữ liệu phim (title, poster, metadata, credits, trailers, lịch chiếu…) được lấy từ TMDB. 

Các ràng buộc nghiệp vụ bổ sung (theo trao đổi):
- Chỉ bật quyền comment review cho một phim nếu người dùng thực sự đã đi xem phim đó (phải có mã vé hợp lệ). Mã vé xuất hiện trên vé, nền tảng gọi API đối tác rạp (ví dụ CGV) để xác minh. Nếu true, bật cờ cho phép comment phim đó.
- Ngoại lệ: Cho phép bình luận tự do đối với các phim mà rạp từng chiếu cách thời điểm hiện tại >= 1 năm.
- Đối với phim đang chiếu (Now Playing): vẫn yêu cầu đã có vé hợp lệ để comment.

Mục tiêu: 
- Đồng bộ và hiển thị catalog phim từ TMDB.
- Quản lý người dùng, quyền review theo rule trên.
- Bảo đảm tuân thủ điều khoản sử dụng, attribution TMDB.

## 2) Phạm vi (Scope)
- Đọc dữ liệu công khai từ TMDB (v3 API) bằng Bearer token/hoặc api_key.
- Không ghi dữ liệu lên TMDB (rating của user TMDB là tùy chọn, thường không cần).
- Lưu trữ review nội bộ (DB riêng), không đồng bộ review lên TMDB.
- Xác thực vé qua API nhà rạp (CGV/v.v.), tích hợp dạng webhook/REST (mô phỏng).

## 3) Vai trò người dùng
- Khách: xem trang phim, thông tin, trailer, danh sách đang chiếu, phổ biến, trending, tìm kiếm.
- Người dùng đã đăng nhập: có thể review nếu:
  - Có vé hợp lệ với phim đó; hoặc
  - Phim đã ngừng chiếu ≥ 1 năm (miễn cần vé).
- Quản trị: quản lý nội dung vi phạm, gắn nhãn, ẩn/bỏ ẩn review, xử lý report.

## 4) Thuật ngữ & dữ liệu chính
- Movie (TMDB): id, title, overview, poster_path, backdrop_path, release_date, genres, runtime, original_language, vote_average, etc.
- Credits: cast, crew. (GET /movie/{id}/credits)
- Videos: trailers, teasers. (GET /movie/{id}/videos)
- Configuration: base_url, image sizes. (GET /configuration)
- Now Playing: danh sách phim đang chiếu. (GET /movie/now_playing)
- Discover/Search: khám phá và tìm kiếm.
- Review nội bộ: {id, userId, movieId, rating (1..10), title, content, spoilerFlag, createdAt, updatedAt, status}
- Vé: bản ghi xác thực lưu tối thiểu {userId, theater, externalTicketId, movieId, showtime, verifiedAt, provider}.

## 5) Luồng chính
1) Duyệt khám phá phim
   - Người dùng vào trang chủ: hiển thị Trending/Popular/Now Playing (TMDB endpoints).
   - Khi xem chi tiết phim: gọi TMDB movie details + append (credits, videos) nếu cần.

2) Xác thực vé trước khi comment (gating)
   - User nhấn "Viết review" trên trang phim đang chiếu hoặc phim < 1 năm kể từ lần chiếu ở rạp.
   - Hệ thống kiểm tra rule:
     - Nếu phim đang chiếu (Now Playing) => bắt buộc đã có vé.
     - Nếu phim không còn chiếu: nếu ngày hiện tại - ngày chiếu rạp gần nhất ≥ 365 ngày => được bình luận luôn; ngược lại cần vé.
   - Nếu cần vé: user nhập mã vé/ảnh vé; hệ thống gọi API đối tác rạp (ví dụ POST /tickets/verify) kèm mã vé, movieId, showtime. Nếu trả về valid=true, lưu bản ghi vé, bật quyền comment phim đó cho user. Nếu invalid => thông báo từ chối.

3) Tạo review
   - Với user đủ điều kiện, cho phép gửi review (rating, tiêu đề, nội dung). Lưu vào DB nội bộ. Áp dụng rate limit chống spam. Quy trình kiểm duyệt tự động/người.

4) Hiển thị review
   - Liệt kê review theo phim, sắp xếp mới nhất/hữu ích (có thể có upvote/downvote sau).
   - Ẩn review vi phạm; hiển thị nhãn "Đã xác thực vé" nếu review đến từ người đã verify vé.

5) Báo cáo vi phạm
   - Người dùng có thể report review; admin xử lý.

## 6) Quy tắc nghiệp vụ chi tiết
- Gating comment theo vé:
  - Trạng thái phim:
    - Đang chiếu: RequireVerifiedTicket = true.
    - Đã ngừng chiếu < 1 năm: RequireVerifiedTicket = true.
    - Đã ngừng chiếu ≥ 1 năm: RequireVerifiedTicket = false.
- Xác định "đã ngừng chiếu" và mốc 1 năm:
  - Dựa vào dữ liệu lịch phát hành/chiếu theo region (TMDB Release Dates + logic nội bộ +/hoặc lịch chiếu rạp). Nếu không có dữ liệu lịch chiếu chi tiết, dùng release_date + khoảng đệm cấu hình (ví dụ 90 ngày là cửa sổ chiếu) để suy ra.
- Một vé chỉ gắn được với một user.
- Vé hợp lệ phải khớp: provider, movieId (mapping qua external ids hoặc title+ngày), ngày giờ suất chiếu (cho phép lệch ±X phút), chưa bị hủy/refund.
- Review của user cho cùng một phim: tối đa 1 review chính; cho phép chỉnh sửa trong 24–48h, sau đó chỉ cho phép update nhỏ (ví dụ grammar) hoặc tạo review bổ sung có nhãn "Cập nhật" theo chính sách.
- Nội dung review: cấm spoil không gắn spoilerFlag; cấm nội dung vi phạm (NSFW, hate, spam...). Có bộ lọc tự động + moderation.

## 7) TMDB tích hợp
- Authentication (Application level): sử dụng Bearer API Read Access Token hoặc api_key (khuyến nghị Bearer). Tham khảo: https://developer.themoviedb.org/docs/authentication-application
- Rate limit: TMDB không công bố cứng; tài liệu nhắc ~50 req/giây và có thể thay đổi, phải tôn trọng 429. Tham khảo: https://developer.themoviedb.org/docs/rate-limiting
- Endpoints sử dụng chính:
  - Configuration: GET /configuration (lấy base_url, image sizes)
  - Discover/Search: GET /discover/movie, /search/movie
  - Now Playing: GET /movie/now_playing
  - Details: GET /movie/{id} (có append_to_response=credits,videos,releases...) khi cần
  - Credits: GET /movie/{id}/credits
  - Reviews (TMDB): GET /movie/{id}/reviews (chỉ tham khảo; review nội bộ do ta quản lý)
- Quy định sử dụng & Attribution:
  - Phải ghi nguồn: "This product uses the TMDB API but is not endorsed or certified by TMDB" và hiển thị logo TMDB theo yêu cầu. Tham khảo điều khoản: https://www.themoviedb.org/api-terms-of-use
  - Không cache dữ liệu quá 6 tháng; không dùng TMDB như image hosting cho banner quảng cáo; không dùng vào ML/AI training; tuân thủ cấm commercial use nếu chưa có thỏa thuận.

## 8) Kiến trúc dữ liệu nội bộ (đề xuất)
- Bảng Movies (local cache mỏng): tmdbId (PK), title, posterPath, backdropPath, releaseDate, fetchedAt, lastSyncedAt, status.
- Bảng Users.
- Bảng Tickets: id (PK), userId (FK), tmdbMovieId, provider, externalTicketId, showtimeUtc, verifiedAt, verificationPayload (JSON), unique(userId, externalTicketId).
- Bảng Reviews: id (PK), userId (FK), tmdbMovieId, rating, title, content, spoilerFlag, verifiedTicket (bool), createdAt, updatedAt, status, abuseScore.
- Bảng ReviewVotes (nếu cần), ReviewReports.

## 9) Các rủi ro & kiểm soát
- Rate limiting TMDB: Sử dụng HttpClientFactory + Polly retry/backoff; cache cấu hình/images; output cache kết quả public.
- Đồng bộ vùng/region: Khi gọi discover/now_playing dùng tham số region/language phù hợp.
- Mapping vé -> phim TMDB: cần bảng map title/ids giữa rạp và TMDB (dựa trên external ids, release date, runtime) để tránh nhầm.
- Pháp lý: Hiển thị attribution bắt buộc; tôn trọng giới hạn cache ≤ 6 tháng; không thương mại hóa nếu chưa có thỏa thuận TMDB.
- Bảo mật: Không lưu token TMDB trong client; dùng Secret Manager/Azure Key Vault cho môi trường prod.

## 10) KPI & báo cáo
- Tỷ lệ review có vé xác thực vs tổng review.
- Thời gian xác thực vé trung bình.
- Tỷ lệ từ chối do vé không hợp lệ.
- Lượt xem trang phim, CTR vào nút viết review.
- Tỷ lệ report/vi phạm.

## 11) Non-functional
- Hiệu năng: cache output cho danh mục phổ biến/đang chiếu; cache in-memory/Redis.
- Khả dụng: xử lý gracefully khi TMDB lỗi (fallback dữ liệu đã cache trong TTL ngắn).
- Bảo mật: XSS, CSRF, rate limit chống spam review.
- Khả năng mở rộng: thiết kế endpoint nội bộ stateless; cân nhắc CDN cho ảnh (dùng domain TMDB theo cấu hình image).

---
Tài liệu này là cơ sở để đội dev triển khai; các endpoint cụ thể của rạp (CGV) và lược đồ xác thực vé sẽ được mô tả ở tài liệu tích hợp đối tác khi có đặc tả chính thức.
