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
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "Games",
                newName: "ScreenshotContentType");

            migrationBuilder.AddColumn<byte[]>(
                name: "ScreenshotData",
                table: "Games",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScreenshotData",
                table: "Games");

            migrationBuilder.RenameColumn(
                name: "ScreenshotContentType",
                table: "Games",
                newName: "ImageUrl");
        }
    }
}
