using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EsportTeamManager.Infrastructure.PostgreSql.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmailReservationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedPendingEmail",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedPendingEmail",
                table: "AspNetUsers",
                column: "NormalizedPendingEmail",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_PendingEmailConsistency",
                table: "AspNetUsers",
                sql: "(\"PendingEmail\" IS NULL AND \"NormalizedPendingEmail\" IS NULL AND \"PendingEmailExpiresAtUtc\" IS NULL) OR (\"PendingEmail\" IS NOT NULL AND \"NormalizedPendingEmail\" IS NOT NULL AND \"PendingEmailExpiresAtUtc\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedPendingEmail",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_PendingEmailConsistency",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedPendingEmail",
                table: "AspNetUsers",
                column: "NormalizedPendingEmail");
        }
    }
}
