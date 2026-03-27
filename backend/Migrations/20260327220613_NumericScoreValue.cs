using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class NumericScoreValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScoreValue",
                table: "Submissions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId_CreatedAt",
                table: "Submissions",
                columns: new[] { "GameId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId_CreatedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ScoreValue",
                table: "Submissions");
        }
    }
}
