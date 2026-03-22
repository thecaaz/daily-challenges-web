using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class AddGameReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "ResetTime",
                table: "Games",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "ResetTimezoneId",
                table: "Games",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetTime",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "ResetTimezoneId",
                table: "Games");
        }
    }
}
