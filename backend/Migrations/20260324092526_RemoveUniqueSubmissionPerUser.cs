using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueSubmissionPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions",
                columns: new[] { "GameId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions",
                columns: new[] { "GameId", "UserId" },
                unique: true);
        }
    }
}
