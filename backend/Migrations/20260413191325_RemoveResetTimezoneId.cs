using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class RemoveResetTimezoneId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetTimezoneId",
                table: "Games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResetTimezoneId",
                table: "Games",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
