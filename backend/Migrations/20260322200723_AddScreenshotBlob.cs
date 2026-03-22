using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenshotBlob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ScreenshotUrl",
                table: "Submissions",
                newName: "ScreenshotContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "ScreenshotData",
                table: "Submissions",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenshotData",
                table: "Submissions");

            migrationBuilder.RenameColumn(
                name: "ScreenshotContentType",
                table: "Submissions",
                newName: "ScreenshotUrl");
        }
    }
}
