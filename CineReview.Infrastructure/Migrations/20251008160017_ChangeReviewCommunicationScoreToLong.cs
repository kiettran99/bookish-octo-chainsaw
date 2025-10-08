using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeReviewCommunicationScoreToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "CommunicationScore",
                table: "Review",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(double),
                oldType: "REAL",
                oldDefaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "CommunicationScore",
                table: "Review",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldDefaultValue: 0L);
        }
    }
}
