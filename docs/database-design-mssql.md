# Thiết kế cơ sở dữ liệu MSSQL cho nền tảng review phim (TMDB)

Cập nhật: 2025-09-30

Tài liệu này tổng hợp từ các yêu cầu nghiệp vụ và gợi ý kiến trúc trong:
- `docs/nghiep-vu-review-phim.md`
- `docs/blazor-aspnetcore-implementation.md`

Kèm theo: script DDL đầy đủ tại `docs/sql/reviewmovies-mssql.ddl.sql`.

## Mục tiêu và phạm vi
- Lưu trữ review nội bộ, phi tập trung với TMDB (chỉ đọc từ TMDB).
- Áp dụng rule “gating review theo vé”: đang chiếu hoặc ngừng chiếu < 1 năm cần vé hợp lệ.
- Tích hợp ASP.NET Core Identity (bảng `AspNetUsers`) làm nguồn User.
- Tối ưu truy vấn chính: danh sách review theo phim, duy nhất một review chính (primary) cho mỗi cặp (user, movie), tra cứu vé của user.

## Lược đồ tổng quan (ERD dạng chữ)
- Users: dùng `AspNetUsers` (PK: `Id` nvarchar(450)).
- app.Movies (PK: TmdbMovieId int)
  - 1 — n với app.Reviews, app.Tickets
- app.Reviews (PK: ReviewId bigint)
  - FK (UserId -> AspNetUsers.Id), (TmdbMovieId -> app.Movies.TmdbMovieId)
  - Unique filtered index đảm bảo mỗi user có tối đa 1 “review chính” cho một phim (`IsPrimary=1`).
- app.Tickets (PK: TicketId bigint)
  - FK (UserId -> AspNetUsers.Id), (TmdbMovieId -> app.Movies.TmdbMovieId), (ProviderCode -> app.TicketProviders.Code)
  - Unique (UserId, ExternalTicketId)
  - Check ISJSON cho `VerificationPayload` (SQL Server 2016+; tham số kiểu nâng cao từ 2022)
- app.ReviewVotes (PK: VoteId bigint)
  - Unique (ReviewId, UserId), value ∈ {-1, 1}
- app.ReviewReports (PK: ReportId bigint)
  - Report/Moderation workflow
- app.TicketProviders (PK: Code nvarchar(50))

Gợi ý schema: dùng schema `app` để nhóm bảng domain.

## Chi tiết bảng và chỉ mục chính
1) app.Movies
- TmdbMovieId (int, PK)
- Title (nvarchar(200), not null), OriginalTitle (nvarchar(200), null)
- ReleaseDate (date, null), LastTheatricalDate (date, null)
- PosterPath, BackdropPath (nvarchar(300), null)
- Status (tinyint, default 0) — tuỳ app định nghĩa
- FetchedAt (datetime2(3) default SYSUTCDATETIME()), LastSyncedAt (datetime2(3), null)
- Chỉ mục: IX_Movies_ReleaseDate (ReleaseDate), tuỳ nhu cầu lọc

2) app.Reviews
- ReviewId (bigint IDENTITY, PK)
- UserId (nvarchar(450), FK -> AspNetUsers.Id)
- TmdbMovieId (int, FK -> app.Movies)
- Rating (tinyint 1..10), Title (nvarchar(200)), Content (nvarchar(max))
- SpoilerFlag (bit, default 0), VerifiedTicket (bit, default 0)
- IsPrimary (bit, default 1) — review chính của user cho phim
- Status (tinyint, default 1) — Published/Hidden/Deleted…
- AbuseScore (int, default 0)
- CreatedAt, UpdatedAt (datetime2(3), default SYSUTCDATETIME())
- RowVersion (rowversion) — concurrency token
- Chỉ mục:
  - Unique filtered: UQ_Reviews_User_Movie_Primary on (UserId, TmdbMovieId) WHERE IsPrimary=1
  - IX_Reviews_Movie_Created on (TmdbMovieId, CreatedAt DESC)
  - IX_Reviews_User_Movie on (UserId, TmdbMovieId)

3) app.Tickets
- TicketId (bigint IDENTITY, PK)
- UserId (nvarchar(450), FK), TmdbMovieId (int, FK)
- ProviderCode (nvarchar(50), FK -> app.TicketProviders)
- ExternalTicketId (nvarchar(100), not null)
- ShowTimeUtc (datetime2(0), not null)
- VerifiedAt (datetime2(3) default SYSUTCDATETIME())
- VerificationPayload (nvarchar(max), null, CHECK ISJSON)
- RowVersion (rowversion)
- Ràng buộc/chỉ mục:
  - Unique (UserId, ExternalTicketId)
  - IX_Tickets_User_Movie (UserId, TmdbMovieId)
  - IX_Tickets_Movie_ShowTime (TmdbMovieId, ShowTimeUtc)

4) app.ReviewVotes
- VoteId (bigint IDENTITY, PK)
- ReviewId (bigint, FK), UserId (nvarchar(450), FK)
- Value (smallint in {-1,1})
- CreatedAt (datetime2(3) default SYSUTCDATETIME())
- Unique (ReviewId, UserId)

5) app.ReviewReports
- ReportId (bigint IDENTITY, PK)
- ReviewId (bigint, FK), ReporterUserId (nvarchar(450), FK)
- ReasonCode (smallint, default 0), Details (nvarchar(1000))
- Status (tinyint, default 0), CreatedAt (datetime2(3) default SYSUTCDATETIME())
- IX_Reports_Review_Created (ReviewId, CreatedAt DESC)

6) app.TicketProviders
- Code (nvarchar(50), PK), Name (nvarchar(100) not null), IsActive (bit default 1)

Ghi chú FK tới Identity: tham chiếu `AspNetUsers(Id nvarchar(450))` là mặc định phổ biến của ASP.NET Core Identity trên SQL Server, giảm rủi ro đụng trần index key length khi join với các khoá khác.

## DDL T-SQL tham khảo
Script đầy đủ: `docs/sql/reviewmovies-mssql.ddl.sql`

Nổi bật:
- Dùng `SYSUTCDATETIME()` cho timestamp mặc định (độ chính xác cao hơn GETUTCDATE()).
- Dùng `rowversion` cho optimistic concurrency (EF Core: byte[] + `[Timestamp]`/`IsRowVersion()`).
- Dùng Unique Filtered Index để enforce “một review chính” mỗi (User, Movie).
- Dùng `ISJSON(...)>0` làm CHECK constraint (SQL Server 2016+; một số tuỳ chọn mở rộng từ 2022).

## Mapping EF Core (gợi ý)
- RowVersion:
  - Property byte[] RowVersion; builder.Property(x => x.RowVersion).IsRowVersion();
- Filtered unique index (IsPrimary=1):
  - builder.Entity<Review>()
    .HasIndex(x => new { x.UserId, x.TmdbMovieId })
    .IsUnique()
    .HasFilter("[IsPrimary] = 1");
- Check constraints:
  - builder.Entity<Review>().ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 10"));
  - builder.Entity<Ticket>().ToTable(t => t.HasCheckConstraint("CK_Tickets_VerificationPayload_IsJson", "ISJSON([VerificationPayload]) > 0 OR [VerificationPayload] IS NULL"));
- Khóa ngoại tới Identity: cấu hình `UserId` là string (nvarchar(450)).

## Mẹo vận hành
- Không xoá cứng review: dùng Status=Deleted để “soft-delete”; filtered unique index sẽ chỉ áp trên IsPrimary=1, cho phép giữ lịch sử nếu cần nhiều bản ghi.
- UpdatedAt nên cập nhật ở application/service layer; `rowversion` dùng để phát hiện conflict.
- Index theo truy vấn thật: nếu trang danh sách review sort theo “hữu ích”, cân nhắc thêm chỉ mục phủ (INCLUDE) trên cột aggregate/phụ trợ.

## Kịch bản truy vấn phổ biến và chỉ mục liên quan
- Danh sách review theo phim (paging mới nhất):
  - WHERE TmdbMovieId=@id ORDER BY CreatedAt DESC — dùng IX_Reviews_Movie_Created
- Kiểm tra user đã có review chính chưa:
  - WHERE UserId=@u AND TmdbMovieId=@m AND IsPrimary=1 — dùng UQ_Reviews_User_Movie_Primary
- Xác minh vé theo user và mã vé:
  - WHERE UserId=@u AND ExternalTicketId=@x — dùng Unique(UserId, ExternalTicketId)
- Lấy tất cả vé của user cho một phim:
  - WHERE UserId=@u AND TmdbMovieId=@m — dùng IX_Tickets_User_Movie

## Phần mở rộng (tuỳ chọn)
- Lịch phát hành theo vùng/app logic: thêm bảng `app.MovieReleases` nếu cần chi tiết theo region/language.
- Audit trail: thêm bảng lịch sử hoặc bật system-versioned temporal tables cho một số bảng (chi phí lưu trữ cao hơn).
- Phân hoạch (partitioning) nếu dữ liệu rất lớn trong tương lai.

## Tham chiếu xác thực kỹ thuật (Microsoft Learn)
- Filtered Index & Unique Index:
  - https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes?view=sql-server-ver17
  - https://learn.microsoft.com/en-us/sql/t-sql/statements/create-index-transact-sql?view=sql-server-ver17
- datetime2 và SYSUTCDATETIME():
  - https://learn.microsoft.com/en-us/sql/t-sql/data-types/datetime2-transact-sql?view=sql-server-ver17
  - https://learn.microsoft.com/en-us/sql/t-sql/functions/sysutcdatetime-transact-sql?view=sql-server-ver17
- rowversion:
  - https://learn.microsoft.com/en-us/sql/t-sql/data-types/rowversion-transact-sql?view=sql-server-ver17
  - EF Core concurrency: https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- ISJSON và JSON trong SQL Server:
  - https://learn.microsoft.com/en-us/sql/t-sql/functions/isjson-transact-sql?view=sql-server-ver17
  - https://learn.microsoft.com/en-us/sql/relational-databases/json/json-data-sql-server?view=sql-server-ver17
- ASP.NET Core Identity (mặc định Id nvarchar(450)):
  - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-9.0
  - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-9.0

---
Nếu bạn muốn, mình có thể tạo migration EF Core tương ứng với lược đồ này hoặc chuyển đổi DDL cho PostgreSQL/MySQL.
