using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReviewRatingToDoubleAndAddRejectReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "Rating",
                table: "Review",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "Review",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "Review");

            migrationBuilder.AlterColumn<int>(
                name: "Rating",
                table: "Review",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");
        }
    }
}
