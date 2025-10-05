using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewAndUserRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TmdbMovieId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CommunicationScore = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    DescriptionTag = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Review", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRating",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewId = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRating_Review_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Review",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Review_Status",
                table: "Review",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Review_TmdbMovieId",
                table: "Review",
                column: "TmdbMovieId");

            migrationBuilder.CreateIndex(
                name: "IX_Review_UserId",
                table: "Review",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Review_UserId_TmdbMovieId",
                table: "Review",
                columns: new[] { "UserId", "TmdbMovieId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRating_ReviewId",
                table: "UserRating",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRating_UserId",
                table: "UserRating",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRating_UserId_ReviewId",
                table: "UserRating",
                columns: new[] { "UserId", "ReviewId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRating");

            migrationBuilder.DropTable(
                name: "Review");
        }
    }
}
