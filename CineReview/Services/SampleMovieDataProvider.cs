using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CineReview.Models;

namespace CineReview.Services;

public sealed class SampleMovieDataProvider : IMovieDataProvider
{
    private readonly HomePageData _homeSnapshot;
    private readonly IReadOnlyDictionary<int, MovieProfile> _movieDetails;
    private readonly IReadOnlyList<MovieSummary> _allSummaries;
    private readonly IReadOnlyList<MovieFilterOption> _genreOptions;
    private readonly IReadOnlyList<MovieFilterOption> _regionOptions;
    private readonly IReadOnlyDictionary<int, string> _sampleRegions;

    public SampleMovieDataProvider()
    {
        var details = BuildMovieProfiles();
        _movieDetails = details.ToDictionary(detail => detail.Summary.Id);
        _homeSnapshot = BuildHomeData(details);
        _allSummaries = details
            .Select(detail => detail.Summary)
            .DistinctBy(summary => summary.Id)
            .ToArray();

        _sampleRegions = new Dictionary<int, string>
        {
            [693134] = "US",
            [1029575] = "US",
            [872585] = "US",
            [558449] = "GB",
            [823464] = "US"
        };

        _genreOptions = _allSummaries
            .SelectMany(summary => summary.Genres)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .Select(name => new MovieFilterOption(name, name))
            .ToArray();

        var regionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VN"] = "Việt Nam",
            ["US"] = "Hoa Kỳ",
            ["GB"] = "Vương quốc Anh",
            ["JP"] = "Nhật Bản",
            ["KR"] = "Hàn Quốc"
        };

        _regionOptions = regionLabels
            .Select(kv => new MovieFilterOption(kv.Key, kv.Value))
            .OrderBy(option => option.Label)
            .ToArray();
    }

    public ValueTask<HomePageData> GetHomeAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_homeSnapshot);

    public ValueTask<MovieProfile?> GetMovieDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        _movieDetails.TryGetValue(id, out var detail);
        return ValueTask.FromResult(detail);
    }

    public ValueTask<PaginatedMovies> GetNowPlayingAsync(int page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildPagedResult(
            _homeSnapshot.NowPlaying,
            page,
            "Đang chiếu nổi bật",
            "Các suất chiếu đang mở bán, hãy xác thực vé để bật review."));

    public ValueTask<PaginatedMovies> GetComingSoonAsync(int page, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(BuildPagedResult(
            _homeSnapshot.ComingSoon,
            page,
            "Sắp công chiếu",
            "Đặt lịch phát sóng để nhận nhắc và vé sớm từ CineReview."));

    public ValueTask<MovieSearchResult> SearchMoviesAsync(MovieSearchRequest request, CancellationToken cancellationToken = default)
    {
        const int pageSize = 8;
        var safePage = request.Page < 1 ? 1 : request.Page;

        IEnumerable<MovieSummary> filtered = _allSummaries;

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            filtered = filtered.Where(summary =>
                summary.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(summary.Tagline) && summary.Tagline.Contains(request.Query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(summary.Overview) && summary.Overview.Contains(request.Query, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(request.Genre))
        {
            filtered = filtered.Where(summary => summary.Genres.Any(genre => string.Equals(genre, request.Genre, StringComparison.OrdinalIgnoreCase)));
        }

        if (request.ReleaseFrom.HasValue)
        {
            filtered = filtered.Where(summary => summary.ReleaseDate.Date >= request.ReleaseFrom.Value.Date);
        }

        if (request.ReleaseTo.HasValue)
        {
            filtered = filtered.Where(summary => summary.ReleaseDate.Date <= request.ReleaseTo.Value.Date);
        }

        if (request.MinScore.HasValue)
        {
            filtered = filtered.Where(summary => summary.CommunityScore >= request.MinScore.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            filtered = filtered.Where(summary =>
                _sampleRegions.TryGetValue(summary.Id, out var regionCode) &&
                string.Equals(regionCode, request.Region, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        var totalResults = filteredList.Count;
        var totalPages = totalResults == 0 ? 1 : (int)Math.Ceiling(totalResults / (double)pageSize);
        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        var items = filteredList
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var title = string.IsNullOrWhiteSpace(request.Query)
            ? "Tìm kiếm phim"
            : $"Kết quả cho \"{request.Query}\"";

        var description = totalResults switch
        {
            0 => "Không tìm thấy phim khớp với bộ lọc hiện tại. Thử điều chỉnh tiêu chí và tìm lại.",
            1 => "Đã tìm thấy 1 phim khớp với bộ lọc của bạn.",
            _ => $"Đã tìm thấy {totalResults} phim phù hợp với bộ lọc."
        };

        var page = new PaginatedMovies(items, totalResults == 0 ? 1 : safePage, totalPages, totalResults, title, description);
        return ValueTask.FromResult(new MovieSearchResult(page, _genreOptions, _regionOptions));
    }

    private static IReadOnlyList<MovieProfile> BuildMovieProfiles()
    {
        var dune = new MovieSummary(
            Id: 693134,
            Title: "Dune: Part Two",
            Tagline: "Long live the fighters.",
            PosterUrl: "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=500&q=80",
            BackdropUrl: "https://images.unsplash.com/photo-1478720568477-152d9b164e26?auto=format&fit=crop&w=1600&q=80",
            ReleaseDate: new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CommunityScore: 8.7,
            Genres: new[] { "Science Fiction", "Adventure", "Drama" },
            Overview: "Paul Atreides hợp lực cùng người Fremen để khai phá sức mạnh tương lai và trả thù những kẻ đã phá hủy gia tộc Atreides.",
            IsNowPlaying: true,
            RequiresTicketVerification: true
        );

        var insideOut = new MovieSummary(
            Id: 1029575,
            Title: "Inside Out 2",
            Tagline: "Feel all the feels.",
            PosterUrl: "https://images.unsplash.com/photo-1523731407965-2430cd12f5e4?auto=format&fit=crop&w=500&q=80",
            BackdropUrl: "https://images.unsplash.com/photo-1521737604893-d14cc237f11d?auto=format&fit=crop&w=1600&q=80",
            ReleaseDate: new DateTime(2024, 6, 11, 0, 0, 0, DateTimeKind.Utc),
            CommunityScore: 8.3,
            Genres: new[] { "Animation", "Family", "Comedy" },
            Overview: "Riley bước vào tuổi teen với những cảm xúc mới mẻ xuất hiện, khiến trụ sở cảm xúc hỗn loạn hơn bao giờ hết.",
            IsNowPlaying: true,
            RequiresTicketVerification: true
        );

        var oppenheimer = new MovieSummary(
            Id: 872585,
            Title: "Oppenheimer",
            Tagline: "The world forever changes.",
            PosterUrl: "https://images.unsplash.com/photo-1440404653325-ab127d49abc1?auto=format&fit=crop&w=500&q=80",
            BackdropUrl: "https://images.unsplash.com/photo-1500530855697-b586d89ba3ee?auto=format&fit=crop&w=1600&q=80",
            ReleaseDate: new DateTime(2023, 7, 19, 0, 0, 0, DateTimeKind.Utc),
            CommunityScore: 8.4,
            Genres: new[] { "Drama", "History" },
            Overview: "Christopher Nolan kể lại câu chuyện nhà khoa học J. Robert Oppenheimer và cuộc chạy đua vũ khí hạt nhân thời Thế chiến II.",
            IsNowPlaying: false,
            RequiresTicketVerification: false
        );

        var gladiator = new MovieSummary(
            Id: 558449,
            Title: "Gladiator II",
            Tagline: "A new champion rises.",
            PosterUrl: "https://images.unsplash.com/photo-1541899481282-d53bffe3c35d?auto=format&fit=crop&w=500&q=80",
            BackdropUrl: "https://images.unsplash.com/photo-1521737604893-d14cc237f11d?auto=format&fit=crop&w=1600&q=80",
            ReleaseDate: new DateTime(2024, 11, 14, 0, 0, 0, DateTimeKind.Utc),
            CommunityScore: 0,
            Genres: new[] { "Action", "Drama" },
            Overview: "Lucius trở lại đấu trường La Mã, đối mặt những âm mưu chính trị và kẻ thù mới để tìm lại danh dự.",
            IsNowPlaying: false,
            RequiresTicketVerification: true
        );

        var wicked = new MovieSummary(
            Id: 823464,
            Title: "Wicked",
            Tagline: "Before Dorothy, before the Ruby Slippers.",
            PosterUrl: "https://images.unsplash.com/photo-1526243741027-444d633d7365?auto=format&fit=crop&w=500&q=80",
            BackdropUrl: "https://images.unsplash.com/photo-1463107971871-fbac9ddb920f?auto=format&fit=crop&w=1600&q=80",
            ReleaseDate: new DateTime(2024, 11, 27, 0, 0, 0, DateTimeKind.Utc),
            CommunityScore: 0,
            Genres: new[] { "Fantasy", "Romance", "Musical" },
            Overview: "Tiền truyện của Wizard of Oz, khám phá tình bạn giữa Elphaba và Glinda trước khi định mệnh chia cách.",
            IsNowPlaying: false,
            RequiresTicketVerification: true
        );

        return new MovieProfile[]
        {
            new MovieProfile(
                Summary: dune,
                RuntimeMinutes: 167,
                Status: "Released",
                Certification: "PG-13",
                TicketPolicyNote: "Phim đang chiếu - yêu cầu xác thực vé hợp lệ trước khi đăng review.",
                Highlights: new[]
                {
                    "Đại cảnh sa mạc Arrakis được quay với định dạng IMAX 65mm.",
                    "Zendaya và Timothée Chalamet dẫn dắt câu chuyện bằng chemistry bùng nổ.",
                    "Âm nhạc Hans Zimmer nâng tầm mọi phân đoạn chiến đấu."
                },
                WatchOptions: new[]
                {
                    "Rạp CGV, BHD, Lotte toàn quốc",
                    "IMAX 12K tại các rạp trọng điểm",
                    "Suất chiếu Dolby Atmos"
                },
                TopCast: new[]
                {
                    new CastMember("Timothée Chalamet", "Paul Atreides", "https://i.pravatar.cc/160?img=12"),
                    new CastMember("Zendaya", "Chani", "https://i.pravatar.cc/160?img=32"),
                    new CastMember("Rebecca Ferguson", "Lady Jessica", "https://i.pravatar.cc/160?img=45"),
                    new CastMember("Austin Butler", "Feyd-Rautha", "https://i.pravatar.cc/160?img=17")
                },
                Videos: new[]
                {
                    new TrailerVideo("dune2-main", "Trailer chính thức", "https://img.youtube.com/vi/_19pRsZRiz4/hqdefault.jpg", "https://www.youtube.com/watch?v=_19pRsZRiz4", "YouTube"),
                    new TrailerVideo("dune2-featurette", "Featurette: The Fremen", "https://img.youtube.com/vi/c0C6M0a8nRc/hqdefault.jpg", "https://www.youtube.com/watch?v=c0C6M0a8nRc", "YouTube")
                },
                Reviews: new[]
                {
                    new ReviewSnapshot(
                        Id: "rv-dune-1",
                        Author: "Mai Anh",
                        Username: "@maianh.cine",
                        AvatarUrl: "https://i.pravatar.cc/96?img=47",
                        BadgeLabel: "Reviewer tích cực",
                        Excerpt: "Một tác phẩm hoành tráng khiến mình nghẹt thở từ đầu tới cuối.",
                        Rating: 9,
                        SupportScore: 132,
                        FairVotes: 248,
                        UnfairVotes: 6,
                        CreatedAt: new DateTime(2024, 3, 2, 12, 0, 0, DateTimeKind.Utc),
                        IsTicketVerified: true,
                        ContextLabel: "Dune: Part Two",
                        Location: "Ho Chi Minh City, VN"),
                    new ReviewSnapshot(
                        Id: "rv-dune-2",
                        Author: "Huy Tran",
                        Username: "@huy.arrakis",
                        AvatarUrl: "https://i.pravatar.cc/96?img=65",
                        BadgeLabel: "Người review thẳng thắn",
                        Excerpt: "Villeneuve đưa khán giả trở lại Arrakis với quy mô lớn hơn hẳn phần đầu.",
                        Rating: 8,
                        SupportScore: -14,
                        FairVotes: 61,
                        UnfairVotes: 22,
                        CreatedAt: new DateTime(2024, 3, 5, 8, 30, 0, DateTimeKind.Utc),
                        IsTicketVerified: true,
                        ContextLabel: "Dune: Part Two",
                        Location: "Da Nang, VN")
                },
                Recommended: new[] { insideOut, oppenheimer, gladiator }
            ),
            new MovieProfile(
                Summary: insideOut,
                RuntimeMinutes: 100,
                Status: "Released",
                Certification: "PG",
                TicketPolicyNote: "Phim đang chiếu - yêu cầu vé điện tử hoặc vé giấy còn hiệu lực.",
                Highlights: new[]
                {
                    "Những cảm xúc mới như Anxiety, Ennui mang lại góc nhìn hài hước.",
                    "Pixar tái hiện tuổi teen đầy tinh tế và đồng cảm.",
                    "Phần nhạc nền của Andrea Datzman nhẹ nhàng nhưng giàu cảm xúc."
                },
                WatchOptions: new[]
                {
                    "Rạp CGV, Galaxy, BHD",
                    "Xuất chiếu lồng tiếng & phụ đề",
                    "Suất gia đình cuối tuần"
                },
                TopCast: new[]
                {
                    new CastMember("Amy Poehler", "Joy", "https://i.pravatar.cc/160?img=10"),
                    new CastMember("Maya Hawke", "Anxiety", "https://i.pravatar.cc/160?img=30"),
                    new CastMember("Kaitlyn Dias", "Riley", "https://i.pravatar.cc/160?img=54"),
                    new CastMember("Phyllis Smith", "Sadness", "https://i.pravatar.cc/160?img=23")
                },
                Videos: new[]
                {
                    new TrailerVideo("insideout2-trailer", "Trailer chính thức", "https://img.youtube.com/vi/LEjhY15eCx0/hqdefault.jpg", "https://www.youtube.com/watch?v=LEjhY15eCx0", "YouTube")
                },
                Reviews: new[]
                {
                    new ReviewSnapshot(
                        Id: "rv-insideout2-1",
                        Author: "Lan Chi",
                        Username: "@lan.chill",
                        AvatarUrl: "https://i.pravatar.cc/96?img=11",
                        BadgeLabel: "Gia đình CineReview",
                        Excerpt: "Cả gia đình mình đã cười và khóc cùng Riley. Một phần tiếp theo rất đáng yêu.",
                        Rating: 9,
                        SupportScore: 98,
                        FairVotes: 184,
                        UnfairVotes: 9,
                        CreatedAt: new DateTime(2024, 6, 12, 14, 0, 0, DateTimeKind.Utc),
                        IsTicketVerified: true,
                        ContextLabel: "Inside Out 2",
                        Location: "Hanoi, VN"),
                    new ReviewSnapshot(
                        Id: "rv-insideout2-2",
                        Author: "Thanh Do",
                        Username: "@thanhdocmt",
                        AvatarUrl: "https://i.pravatar.cc/96?img=58",
                        BadgeLabel: "Người review cân bằng",
                        Excerpt: "Các cảm xúc mới hơi vội vàng nhưng thông điệp vẫn rất chạm.",
                        Rating: 7,
                        SupportScore: 24,
                        FairVotes: 102,
                        UnfairVotes: 11,
                        CreatedAt: new DateTime(2024, 6, 13, 19, 15, 0, DateTimeKind.Utc),
                        IsTicketVerified: true,
                        ContextLabel: "Inside Out 2",
                        Location: "Hue, VN")
                },
                Recommended: new[] { dune, oppenheimer, wicked }
            ),
            new MovieProfile(
                Summary: oppenheimer,
                RuntimeMinutes: 180,
                Status: "Released",
                Certification: "R",
                TicketPolicyNote: "Phim phát hành 2023 - không yêu cầu xác thực vé để review.",
                Highlights: new[]
                {
                    "Phim quay bằng IMAX 65mm, kết hợp thước phim đen trắng hiếm hoi.",
                    "Cillian Murphy thể hiện nội tâm Oppenheimer đầy ám ảnh.",
                    "Âm thanh bùng nổ mô phỏng vụ thử Trinity khiến khán giả nghẹt thở."
                },
                WatchOptions: new[]
                {
                    "Phát hành digital trên Universal PVOD",
                    "Blu-ray 4K UHD",
                    "Streaming trên Peacock (US)"
                },
                TopCast: new[]
                {
                    new CastMember("Cillian Murphy", "J. Robert Oppenheimer", "https://i.pravatar.cc/160?img=6"),
                    new CastMember("Emily Blunt", "Kitty Oppenheimer", "https://i.pravatar.cc/160?img=28"),
                    new CastMember("Robert Downey Jr.", "Lewis Strauss", "https://i.pravatar.cc/160?img=40"),
                    new CastMember("Florence Pugh", "Jean Tatlock", "https://i.pravatar.cc/160?img=35")
                },
                Videos: new[]
                {
                    new TrailerVideo("opp-trailer", "Trailer chính thức", "https://img.youtube.com/vi/uYPbbksJxIg/hqdefault.jpg", "https://www.youtube.com/watch?v=uYPbbksJxIg", "YouTube")
                },
                Reviews: new[]
                {
                    new ReviewSnapshot(
                        Id: "rv-opp-1",
                        Author: "Hoang Nguyen",
                        Username: "@hoangwrites",
                        AvatarUrl: "https://i.pravatar.cc/96?img=78",
                        BadgeLabel: "Hội trường bom tấn",
                        Excerpt: "Ba tiếng đồng hồ nhưng không phút nào mình cảm thấy thừa.",
                        Rating: 10,
                        SupportScore: 156,
                        FairVotes: 304,
                        UnfairVotes: 8,
                        CreatedAt: new DateTime(2023, 7, 22, 18, 45, 0, DateTimeKind.Utc),
                        IsTicketVerified: false,
                        ContextLabel: "Oppenheimer",
                        Location: "Da Nang, VN"),
                    new ReviewSnapshot(
                        Id: "rv-opp-2",
                        Author: "Thu Minh",
                        Username: "@minhthu.review",
                        AvatarUrl: "https://i.pravatar.cc/96?img=38",
                        BadgeLabel: "Phân tích chính trị",
                        Excerpt: "Câu chuyện chính trị phức tạp nhưng được kể rất hấp dẫn.",
                        Rating: 8,
                        SupportScore: 41,
                        FairVotes: 119,
                        UnfairVotes: 13,
                        CreatedAt: new DateTime(2023, 7, 24, 9, 0, 0, DateTimeKind.Utc),
                        IsTicketVerified: false,
                        ContextLabel: "Oppenheimer",
                        Location: "Can Tho, VN")
                },
                Recommended: new[] { dune, gladiator, wicked }
            ),
            new MovieProfile(
                Summary: gladiator,
                RuntimeMinutes: 158,
                Status: "Post Production",
                Certification: "Pending",
                TicketPolicyNote: "Phim sắp chiếu - mở đăng ký nhắc lịch, review sẽ yêu cầu vé sau khi công chiếu.",
                Highlights: new[]
                {
                    "Ridley Scott trở lại đấu trường Colosseum sau hơn 20 năm.",
                    "Paul Mescal đảm nhận vai chính Lucius với màn trình diễn hứa hẹn.",
                    "Nhạc nền Hans Zimmer tái hợp cùng những âm thanh trống chiến cổ điển."
                },
                WatchOptions: new[]
                {
                    "Khởi chiếu dự kiến 14.11.2024",
                    "Suất chiếu IMAX được xác nhận",
                    "Đăng ký nhận thông báo vé sớm"
                },
                TopCast: new[]
                {
                    new CastMember("Paul Mescal", "Lucius", "https://i.pravatar.cc/160?img=16"),
                    new CastMember("Denzel Washington", "Macrinus", "https://i.pravatar.cc/160?img=60"),
                    new CastMember("Pedro Pascal", "Marcus Acacius", "https://i.pravatar.cc/160?img=70"),
                    new CastMember("Connie Nielsen", "Lucilla", "https://i.pravatar.cc/160?img=14")
                },
                Videos: Array.Empty<TrailerVideo>(),
                Reviews: Array.Empty<ReviewSnapshot>(),
                Recommended: new[] { dune, oppenheimer, wicked }
            ),
            new MovieProfile(
                Summary: wicked,
                RuntimeMinutes: 165,
                Status: "Post Production",
                Certification: "Pending",
                TicketPolicyNote: "Phim đang mở đặt vé trước, yêu cầu xác thực sau khi xem để đăng review.",
                Highlights: new[]
                {
                    "Jon M. Chu chuyển thể musical kinh điển với hai phần phim.",
                    "Ariana Grande và Cynthia Erivo dẫn dắt bằng giọng hát nội lực.",
                    "Sắc xanh đặc trưng của Elphaba được tái hiện với hiệu ứng mới."
                },
                WatchOptions: new[]
                {
                    "Khởi chiếu dự kiến 27.11.2024",
                    "Định dạng IMAX và ScreenX",
                    "Suất early access cho thành viên CineReview"
                },
                TopCast: new[]
                {
                    new CastMember("Cynthia Erivo", "Elphaba", "https://i.pravatar.cc/160?img=41"),
                    new CastMember("Ariana Grande", "Glinda", "https://i.pravatar.cc/160?img=31"),
                    new CastMember("Jonathan Bailey", "Fiyero", "https://i.pravatar.cc/160?img=24"),
                    new CastMember("Michelle Yeoh", "Madame Morrible", "https://i.pravatar.cc/160?img=18")
                },
                Videos: Array.Empty<TrailerVideo>(),
                Reviews: Array.Empty<ReviewSnapshot>(),
                Recommended: new[] { insideOut, gladiator, dune }
            )
        };
    }

    private static HomePageData BuildHomeData(IReadOnlyList<MovieProfile> details)
    {
        var featured = details.First();

        var nowPlaying = details
            .Where(detail => detail.Summary.IsNowPlaying)
            .Select(detail => detail.Summary)
            .Take(6)
            .ToArray();

        var comingSoon = details
            .Where(detail => !detail.Summary.IsNowPlaying)
            .Select(detail => detail.Summary)
            .OrderBy(summary => summary.ReleaseDate)
            .Take(6)
            .ToArray();

        var trending = details
            .OrderByDescending(detail => detail.Summary.CommunityScore)
            .Select(detail => detail.Summary)
            .Take(6)
            .ToArray();

        var latestReviews = details
            .SelectMany(detail => detail.Reviews.Select(review => review with { ContextLabel = detail.Summary.Title }))
            .OrderByDescending(review => review.CreatedAt)
            .Take(4)
            .ToArray();

        var editorial = new[]
        {
            new EditorialSpotlight(
                Title: "Tổng hợp suất IMAX tháng này",
                Description: "Danh sách chiếu IMAX đáng chú ý tại Việt Nam từ ngày 01-31/10, bao gồm Dune: Part Two và các bom tấn mới.",
                ImageUrl: "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=1200&q=80",
                ActionLabel: "Đọc bài viết",
                ActionUrl: "#"
            ),
            new EditorialSpotlight(
                Title: "Hướng dẫn xác thực vé CGV",
                Description: "Các bước nhập mã vé điện tử, xử lý lỗi thường gặp và mẹo giữ vé giấy để xác thực nhanh chóng.",
                ImageUrl: "https://images.unsplash.com/photo-1485846234645-a62644f84728?auto=format&fit=crop&w=1200&q=80",
                ActionLabel: "Xem hướng dẫn",
                ActionUrl: "#"
            )
        };

        return new HomePageData(
            Featured: featured.Summary,
            NowPlaying: nowPlaying,
            ComingSoon: comingSoon,
            TrendingThisWeek: trending,
            LatestReviews: latestReviews,
            EditorialSpots: editorial
        );
    }

    private static PaginatedMovies BuildPagedResult(
        IReadOnlyList<MovieSummary> source,
        int requestedPage,
        string title,
        string description)
    {
        const int pageSize = 8;
        if (source.Count == 0)
        {
            return new PaginatedMovies(Array.Empty<MovieSummary>(), 1, 1, 0, title, description);
        }

        var safePage = requestedPage < 1 ? 1 : requestedPage;
        var totalPages = (int)Math.Ceiling(source.Count / (double)pageSize);
        totalPages = totalPages < 1 ? 1 : totalPages;

        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        var pageItems = source
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new PaginatedMovies(pageItems, safePage, totalPages, source.Count, title, description);
    }
}
