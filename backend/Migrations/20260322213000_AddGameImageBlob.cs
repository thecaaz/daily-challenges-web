using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class AddGameImageBlob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ScreenshotData",
                table: "Games",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreenshotContentType",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ScreenshotData",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ScreenshotContentType",
                table: "Games");
        }
    }
}
