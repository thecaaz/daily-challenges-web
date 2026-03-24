using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    public partial class RemoveUniqueSubmissionPerUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique composite index on GameId+UserId created in AddSubmissionUser
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions");

            // Recreate non-unique indexes to keep lookup performance
            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId",
                table: "Submissions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_UserId",
                table: "Submissions",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Submissions_GameId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_UserId",
                table: "Submissions");

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_GameId_UserId",
                table: "Submissions",
                columns: new[] { "GameId", "UserId" },
                unique: true);
        }
    }
}
