using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class MigrateValueToScoreValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Populate ScoreValue from Score when Score is a plain integer (optional leading '-').
            // Only update rows where ScoreValue is currently NULL to avoid overwriting existing data.
            migrationBuilder.Sql(
                "UPDATE Submissions SET ScoreValue = CAST(TRIM(Score) AS INTEGER) " +
                "WHERE ScoreValue IS NULL AND (TRIM(Score) GLOB '[0-9][0-9]*' OR TRIM(Score) GLOB '-[0-9][0-9]*');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert only those rows where the Score looks like an integer and matches the stored ScoreValue.
            migrationBuilder.Sql(
                "UPDATE Submissions SET ScoreValue = NULL " +
                "WHERE ScoreValue IS NOT NULL AND (TRIM(Score) GLOB '[0-9][0-9]*' OR TRIM(Score) GLOB '-[0-9][0-9]*') " +
                "AND ScoreValue = CAST(TRIM(Score) AS INTEGER);");
        }
    }
}
