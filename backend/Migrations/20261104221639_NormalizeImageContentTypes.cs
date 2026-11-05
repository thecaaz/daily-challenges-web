using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeImageContentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only fix. List queries no longer load image blobs, so "has an image" is now
            // decided by a non-empty ScreenshotContentType. Make the content type match the blob:
            // old Games rows (column renamed from ImageUrl) can have a content type but no blob.
            migrationBuilder.Sql(
                "UPDATE Games SET ScreenshotContentType = NULL " +
                "WHERE ScreenshotData IS NULL OR length(ScreenshotData) = 0;");
            migrationBuilder.Sql(
                "UPDATE Submissions SET ScreenshotContentType = '' " +
                "WHERE ScreenshotData IS NULL OR length(ScreenshotData) = 0;");

            // Rows with a blob but no content type: sniff the type from the magic bytes.
            const string sniff =
                "CASE " +
                "WHEN hex(substr(ScreenshotData, 1, 4)) = '89504E47' THEN 'image/png' " +
                "WHEN hex(substr(ScreenshotData, 1, 3)) = 'FFD8FF' THEN 'image/jpeg' " +
                "WHEN hex(substr(ScreenshotData, 1, 4)) = '52494646' THEN 'image/webp' " +
                "WHEN hex(substr(ScreenshotData, 1, 3)) = '474946' THEN 'image/gif' " +
                "ELSE 'application/octet-stream' END";
            migrationBuilder.Sql(
                $"UPDATE Games SET ScreenshotContentType = {sniff} " +
                "WHERE length(ScreenshotData) > 0 AND (ScreenshotContentType IS NULL OR ScreenshotContentType = '');");
            migrationBuilder.Sql(
                $"UPDATE Submissions SET ScreenshotContentType = {sniff} " +
                "WHERE length(ScreenshotData) > 0 AND (ScreenshotContentType IS NULL OR ScreenshotContentType = '');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data fix only; nothing to undo.
        }
    }
}
