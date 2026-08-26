using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EsportTeamManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationSenderRateLimitIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_SenderUserId",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SenderUserId_CreatedAtUtc",
                table: "Invitations",
                columns: new[] { "SenderUserId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_SenderUserId_CreatedAtUtc",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SenderUserId",
                table: "Invitations",
                column: "SenderUserId");
        }
    }
}
