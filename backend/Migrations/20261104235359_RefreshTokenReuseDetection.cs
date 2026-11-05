using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <summary>
    /// Records when and why a refresh token was revoked, so a rotated token replayed long after its rotation
    /// can be detected as theft. Existing rows get RevokedAt = NULL (replays of them are only rejected).
    /// </summary>
    public partial class RefreshTokenReuseDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "RevokedRefreshTokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "RevokedRefreshTokens",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "RevokedRefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "RevokedRefreshTokens");
        }
    }
}
