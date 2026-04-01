using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class AddXpAndLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSubmissionAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Ensure all existing users start at level 1 (the column default covers new rows,
            // but existing rows already inserted before this migration get the column default too
            // in SQLite; this explicit UPDATE makes the intent clear and safe across engines).
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Level\" = 1 WHERE \"Level\" = 0");

            migrationBuilder.AddColumn<int>(
                name: "Streak",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalXp",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScoringDay",
                table: "Submissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "XpAwarded",
                table: "Submissions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "XpEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: true),
                    ScoringDay = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpEvents_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_XpEvents_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_XpEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_GameId",
                table: "XpEvents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_SubmissionId",
                table: "XpEvents",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_UserId_CreatedAt",
                table: "XpEvents",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XpEvents");

            migrationBuilder.DropColumn(
                name: "LastSubmissionAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Streak",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotalXp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "XpAwarded",
                table: "Submissions");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ScoringDay",
                table: "Submissions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");
        }
    }
}
