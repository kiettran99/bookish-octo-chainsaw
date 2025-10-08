using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineReview.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCommunicationScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CommunicationScore",
                table: "User",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommunicationScore",
                table: "User");
        }
    }
}
