using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class UniqueDayWinXpEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fix first: the old finalizer could award the same day win twice (no idempotency check,
            // result row written last). Keep the lowest-Id "day_win" event per (UserId, GameId, ScoringDay),
            // subtract the removed duplicates' XP from Users.TotalXp (clamped at 0), then delete them.
            // Users.Level is re-derived from TotalXp at startup (UserLevelSync, after Migrate()).
            // Events whose game was deleted (GameId NULL) are left alone: the unique index treats NULLs as distinct.
            const string duplicate =
                "x.EventType = 'day_win' AND x.GameId IS NOT NULL AND x.ScoringDay IS NOT NULL " +
                "AND EXISTS (SELECT 1 FROM XpEvents k WHERE k.EventType = 'day_win' AND k.UserId = x.UserId " +
                "AND k.GameId = x.GameId AND k.ScoringDay = x.ScoringDay AND k.Id < x.Id)";

            migrationBuilder.Sql(
                "UPDATE Users SET TotalXp = max(0, TotalXp - " +
                $"(SELECT COALESCE(SUM(x.Amount), 0) FROM XpEvents x WHERE x.UserId = Users.Id AND {duplicate})) " +
                $"WHERE Id IN (SELECT x.UserId FROM XpEvents x WHERE {duplicate});");
            migrationBuilder.Sql(
                $"DELETE FROM XpEvents WHERE Id IN (SELECT x.Id FROM XpEvents x WHERE {duplicate});");

            migrationBuilder.CreateIndex(
                name: "IX_XpEvents_UserId_GameId_ScoringDay_DayWin",
                table: "XpEvents",
                columns: new[] { "UserId", "GameId", "ScoringDay" },
                unique: true,
                filter: "[EventType] = 'day_win'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed duplicate day-win events (and the XP they added) are not restored.
            migrationBuilder.DropIndex(
                name: "IX_XpEvents_UserId_GameId_ScoringDay_DayWin",
                table: "XpEvents");
        }
    }
}
