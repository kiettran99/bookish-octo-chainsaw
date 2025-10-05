/*
DDL tạo lược đồ MSSQL cho nền tảng review phim
Phiên bản: 2025-09-30
Yêu cầu: SQL Server 2016+ (ISJSON khả dụng từ 2016; một số tham số nâng cao từ 2022)
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Schema
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'app')
BEGIN
    EXEC('CREATE SCHEMA app');
END
GO

-- TicketProviders
IF OBJECT_ID('app.TicketProviders','U') IS NULL
BEGIN
    CREATE TABLE app.TicketProviders (
        Code            nvarchar(50)  NOT NULL,
        Name            nvarchar(100) NOT NULL,
        IsActive        bit           NOT NULL CONSTRAINT DF_TicketProviders_IsActive DEFAULT(1),
        CONSTRAINT PK_TicketProviders PRIMARY KEY (Code)
    );
END
GO

-- Movies
IF OBJECT_ID('app.Movies','U') IS NULL
BEGIN
    CREATE TABLE app.Movies (
        TmdbMovieId         int             NOT NULL,
        Title               nvarchar(200)   NOT NULL,
        OriginalTitle       nvarchar(200)   NULL,
        ReleaseDate         date            NULL,
        LastTheatricalDate  date            NULL,
        PosterPath          nvarchar(300)   NULL,
        BackdropPath        nvarchar(300)   NULL,
        Status              tinyint         NOT NULL CONSTRAINT DF_Movies_Status DEFAULT(0),
        FetchedAt           datetime2(3)    NOT NULL CONSTRAINT DF_Movies_FetchedAt DEFAULT (SYSUTCDATETIME()),
        LastSyncedAt        datetime2(3)    NULL,
        CONSTRAINT PK_Movies PRIMARY KEY (TmdbMovieId)
    );

    CREATE INDEX IX_Movies_ReleaseDate ON app.Movies(ReleaseDate);
END
GO

-- Reviews
IF OBJECT_ID('app.Reviews','U') IS NULL
BEGIN
    CREATE TABLE app.Reviews (
        ReviewId        bigint          NOT NULL IDENTITY(1,1),
        UserId          nvarchar(450)   NOT NULL, -- FK -> AspNetUsers(Id)
        TmdbMovieId     int             NOT NULL, -- FK -> app.Movies
        Rating          tinyint         NOT NULL,
        Title           nvarchar(200)   NULL,
        Content         nvarchar(max)   NULL,
        SpoilerFlag     bit             NOT NULL CONSTRAINT DF_Reviews_SpoilerFlag DEFAULT(0),
        VerifiedTicket  bit             NOT NULL CONSTRAINT DF_Reviews_Verified DEFAULT(0),
        IsPrimary       bit             NOT NULL CONSTRAINT DF_Reviews_IsPrimary DEFAULT(1),
        Status          tinyint         NOT NULL CONSTRAINT DF_Reviews_Status DEFAULT(1),
        AbuseScore      int             NOT NULL CONSTRAINT DF_Reviews_AbuseScore DEFAULT(0),
        CreatedAt       datetime2(3)    NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       datetime2(3)    NOT NULL CONSTRAINT DF_Reviews_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        RowVersion      rowversion      NOT NULL,
        CONSTRAINT PK_Reviews PRIMARY KEY (ReviewId),
        CONSTRAINT FK_Reviews_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_Reviews_Movie FOREIGN KEY (TmdbMovieId) REFERENCES app.Movies(TmdbMovieId),
        CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 10)
    );

    -- Unique filtered index: một review chính duy nhất cho (User, Movie)
    CREATE UNIQUE INDEX UQ_Reviews_User_Movie_Primary
        ON app.Reviews(UserId, TmdbMovieId)
        WHERE IsPrimary = 1;

    CREATE INDEX IX_Reviews_Movie_Created ON app.Reviews(TmdbMovieId, CreatedAt DESC);
    CREATE INDEX IX_Reviews_User_Movie ON app.Reviews(UserId, TmdbMovieId);
END
GO

-- Tickets
IF OBJECT_ID('app.Tickets','U') IS NULL
BEGIN
    CREATE TABLE app.Tickets (
        TicketId            bigint          NOT NULL IDENTITY(1,1),
        UserId              nvarchar(450)   NOT NULL, -- FK -> AspNetUsers(Id)
        TmdbMovieId         int             NOT NULL, -- FK -> app.Movies
        ProviderCode        nvarchar(50)    NOT NULL, -- FK -> app.TicketProviders
        ExternalTicketId    nvarchar(100)   NOT NULL,
        ShowTimeUtc         datetime2(0)    NOT NULL,
        VerifiedAt          datetime2(3)    NOT NULL CONSTRAINT DF_Tickets_VerifiedAt DEFAULT (SYSUTCDATETIME()),
        VerificationPayload nvarchar(max)   NULL,
        RowVersion          rowversion      NOT NULL,
        CONSTRAINT PK_Tickets PRIMARY KEY (TicketId),
        CONSTRAINT FK_Tickets_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT FK_Tickets_Movie FOREIGN KEY (TmdbMovieId) REFERENCES app.Movies(TmdbMovieId),
        CONSTRAINT FK_Tickets_Provider FOREIGN KEY (ProviderCode) REFERENCES app.TicketProviders(Code)
    );

    -- ISJSON khả dụng từ SQL Server 2016; khối TRY/CATCH để an toàn trên môi trường không hỗ trợ
    BEGIN TRY
        ALTER TABLE app.Tickets WITH NOCHECK ADD CONSTRAINT CK_Tickets_VerificationPayload_IsJson
            CHECK (VerificationPayload IS NULL OR ISJSON(VerificationPayload) > 0);
    END TRY
    BEGIN CATCH
        -- Bỏ qua nếu phiên bản không hỗ trợ ISJSON trong CHECK; có thể thực thi ở layer ứng dụng
        PRINT 'ISJSON check not added (possibly unsupported version).';
    END CATCH;

    -- Unique: một mã vé chỉ gắn được cho một user
    CREATE UNIQUE INDEX UX_Tickets_User_ExternalId ON app.Tickets(UserId, ExternalTicketId);

    CREATE INDEX IX_Tickets_User_Movie ON app.Tickets(UserId, TmdbMovieId);
    CREATE INDEX IX_Tickets_Movie_ShowTime ON app.Tickets(TmdbMovieId, ShowTimeUtc);
END
GO

-- ReviewVotes
IF OBJECT_ID('app.ReviewVotes','U') IS NULL
BEGIN
    CREATE TABLE app.ReviewVotes (
        VoteId      bigint          NOT NULL IDENTITY(1,1),
        ReviewId    bigint          NOT NULL, -- FK -> app.Reviews
        UserId      nvarchar(450)   NOT NULL, -- FK -> AspNetUsers(Id)
        Value       smallint        NOT NULL,
        CreatedAt   datetime2(3)    NOT NULL CONSTRAINT DF_ReviewVotes_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ReviewVotes PRIMARY KEY (VoteId),
        CONSTRAINT FK_ReviewVotes_Review FOREIGN KEY (ReviewId) REFERENCES app.Reviews(ReviewId),
        CONSTRAINT FK_ReviewVotes_User FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id),
        CONSTRAINT CK_ReviewVotes_Value CHECK (Value IN (-1, 1))
    );

    CREATE UNIQUE INDEX UX_ReviewVotes_Review_User ON app.ReviewVotes(ReviewId, UserId);
END
GO

-- ReviewReports
IF OBJECT_ID('app.ReviewReports','U') IS NULL
BEGIN
    CREATE TABLE app.ReviewReports (
        ReportId        bigint          NOT NULL IDENTITY(1,1),
        ReviewId        bigint          NOT NULL, -- FK -> app.Reviews
        ReporterUserId  nvarchar(450)   NOT NULL, -- FK -> AspNetUsers(Id)
        ReasonCode      smallint        NOT NULL CONSTRAINT DF_ReviewReports_Reason DEFAULT(0),
        Details         nvarchar(1000)  NULL,
        Status          tinyint         NOT NULL CONSTRAINT DF_ReviewReports_Status DEFAULT(0),
        CreatedAt       datetime2(3)    NOT NULL CONSTRAINT DF_ReviewReports_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ReviewReports PRIMARY KEY (ReportId),
        CONSTRAINT FK_ReviewReports_Review FOREIGN KEY (ReviewId) REFERENCES app.Reviews(ReviewId),
        CONSTRAINT FK_ReviewReports_User FOREIGN KEY (ReporterUserId) REFERENCES dbo.AspNetUsers(Id)
    );

    CREATE INDEX IX_Reports_Review_Created ON app.ReviewReports(ReviewId, CreatedAt DESC);
END
GO

PRINT 'DDL setup completed.';
