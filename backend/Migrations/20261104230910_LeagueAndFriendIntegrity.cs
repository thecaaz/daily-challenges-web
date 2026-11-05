using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DailyChallenges.Migrations
{
    /// <inheritdoc />
    public partial class LeagueAndFriendIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data fixes before the new unique indexes (LeagueInvitationStatus: 0 Pending, 3 Cancelled;
            // FriendRequestStatus: 0 Pending, 1 Accepted).

            // 1. Duplicate pending direct invites: keep the oldest (lowest Id) per (league, invitee), cancel the rest.
            migrationBuilder.Sql(
                "UPDATE LeagueInvitations SET Status = 3, UpdatedAt = datetime('now') " +
                "WHERE Status = 0 AND InviteeId IS NOT NULL AND EXISTS (SELECT 1 FROM LeagueInvitations k " +
                "WHERE k.LeagueId = LeagueInvitations.LeagueId AND k.InviteeId = LeagueInvitations.InviteeId " +
                "AND k.Status = 0 AND k.Id < LeagueInvitations.Id);");

            // 2. Duplicate invite-link tokens: the lowest Id keeps the token; the others lose it, and are
            //    cancelled if still pending (a pending link without a token can never be used).
            migrationBuilder.Sql(
                "UPDATE LeagueInvitations SET " +
                "UpdatedAt = CASE WHEN Status = 0 THEN datetime('now') ELSE UpdatedAt END, " +
                "Status = CASE WHEN Status = 0 THEN 3 ELSE Status END, " +
                "Token = NULL " +
                "WHERE Token IS NOT NULL AND EXISTS (SELECT 1 FROM LeagueInvitations k " +
                "WHERE k.Token = LeagueInvitations.Token AND k.Id < LeagueInvitations.Id);");

            // 3. More than one friend request row for a pair of users (A->B and B->A): keep the accepted one,
            //    else the oldest; delete the others.
            migrationBuilder.Sql(
                "DELETE FROM FriendRequests WHERE Id <> (SELECT k.Id FROM FriendRequests k " +
                "WHERE min(k.SenderId, k.ReceiverId) = min(FriendRequests.SenderId, FriendRequests.ReceiverId) " +
                "AND max(k.SenderId, k.ReceiverId) = max(FriendRequests.SenderId, FriendRequests.ReceiverId) " +
                "ORDER BY (k.Status = 1) DESC, k.CreatedAt, k.Id LIMIT 1);");

            migrationBuilder.DropIndex(
                name: "IX_LeagueInvitations_LeagueId_InviteeId",
                table: "LeagueInvitations");

            migrationBuilder.DropIndex(
                name: "IX_LeagueInvitations_Token",
                table: "LeagueInvitations");

            migrationBuilder.DropIndex(
                name: "IX_Favorites_UserId",
                table: "Favorites");

            migrationBuilder.AddColumn<int>(
                name: "ActorUserId",
                table: "Notifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserHighId",
                table: "FriendRequests",
                type: "INTEGER",
                nullable: false,
                computedColumnSql: "max([SenderId], [ReceiverId])",
                stored: false);

            migrationBuilder.AddColumn<int>(
                name: "UserLowId",
                table: "FriendRequests",
                type: "INTEGER",
                nullable: false,
                computedColumnSql: "min([SenderId], [ReceiverId])",
                stored: false);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActorUserId",
                table: "Notifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvitations_LeagueId",
                table: "LeagueInvitations",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvitations_LeagueId_InviteeId",
                table: "LeagueInvitations",
                columns: new[] { "LeagueId", "InviteeId" },
                unique: true,
                filter: "[Status] = 0 AND [InviteeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvitations_Token",
                table: "LeagueInvitations",
                column: "Token",
                unique: true,
                filter: "[Token] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_UserLowId_UserHighId",
                table: "FriendRequests",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_ActorUserId",
                table: "Notifications",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Backfill ActorUserId of existing friend/league notifications from their message text
            // ("<username> sent you a friend request.", ...). Usernames are unique (NOCASE); no match leaves null.
            foreach (var (type, marker) in new[]
            {
                ("friend_request", " sent you a friend request."),
                ("friend_request_accepted", " accepted your friend request."),
                ("league_invite", " invited you to join the league \""),
                ("league_joined", " joined your league \""),
            })
            {
                migrationBuilder.Sql(
                    "UPDATE Notifications SET ActorUserId = (SELECT u.Id FROM Users u " +
                    $"WHERE u.Username = substr(Notifications.Message, 1, instr(Notifications.Message, '{marker}') - 1)) " +
                    $"WHERE Type = '{type}' AND ActorUserId IS NULL AND instr(Message, '{marker}') > 1;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_ActorUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_ActorUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_LeagueInvitations_LeagueId",
                table: "LeagueInvitations");

            migrationBuilder.DropIndex(
                name: "IX_LeagueInvitations_LeagueId_InviteeId",
                table: "LeagueInvitations");

            migrationBuilder.DropIndex(
                name: "IX_LeagueInvitations_Token",
                table: "LeagueInvitations");

            migrationBuilder.DropIndex(
                name: "IX_FriendRequests_UserLowId_UserHighId",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "UserHighId",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "UserLowId",
                table: "FriendRequests");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvitations_LeagueId_InviteeId",
                table: "LeagueInvitations",
                columns: new[] { "LeagueId", "InviteeId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInvitations_Token",
                table: "LeagueInvitations",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_Favorites_UserId",
                table: "Favorites",
                column: "UserId");
        }
    }
}
