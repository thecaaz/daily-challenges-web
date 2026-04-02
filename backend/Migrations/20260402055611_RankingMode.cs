using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class RankingMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RankingMode",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RankingMode",
                table: "Games");
        }
    }
}
