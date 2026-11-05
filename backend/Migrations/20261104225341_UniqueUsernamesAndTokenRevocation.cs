using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUsernamesAndTokenRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fix before the unique index: usernames that differ only by case (or surrounding
            // whitespace) collide under the new NOCASE unique index. Keep the lowest Id of each group and
            // rename the others to "<trimmed name>_<Id>". Then trim the remaining names, because login and
            // register now trim their input (a stored " bob" could never log in again).
            migrationBuilder.Sql(
                "UPDATE Users SET Username = trim(Username) || '_' || Id " +
                "WHERE EXISTS (SELECT 1 FROM Users k WHERE lower(trim(k.Username)) = lower(trim(Users.Username)) AND k.Id < Users.Id);");
            migrationBuilder.Sql(
                "UPDATE Users SET Username = trim(Username) WHERE Username <> trim(Username);");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<int>(
                name: "TokenVersion",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RevokedRefreshTokens",
                columns: table => new
                {
                    Jti = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevokedRefreshTokens", x => x.Jti);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevokedRefreshTokens_ExpiresAt",
                table: "RevokedRefreshTokens",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RevokedRefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenVersion",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");
        }
    }
}
