using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class ScoringDayOnSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScoringDay",
                table: "Submissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId_ScoringDay",
                table: "Submissions",
                columns: new[] { "GameId", "ScoringDay" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId_ScoringDay",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ScoringDay",
                table: "Submissions");
        }
    }
}
