using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EsportTeamManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ActivityTypes",
                columns: new[] { "ActivityTypeId", "Code", "IsSystem", "Label", "TeamId" },
                values: new object[,]
                {
                    { 1, "Pracc", true, "Pracc", null },
                    { 2, "OfficialMatch", true, "Match officiel", null },
                    { 3, "Meeting", true, "Réunion", null },
                    { 4, "VodReview", true, "Review VOD", null }
                });

            migrationBuilder.InsertData(
                table: "Maps",
                columns: new[] { "MapId", "Name" },
                values: new object[,]
                {
                    { 1, "Bind" },
                    { 2, "Haven" },
                    { 3, "Split" },
                    { 4, "Ascent" },
                    { 5, "Icebox" },
                    { 6, "Breeze" },
                    { 7, "Fracture" },
                    { 8, "Pearl" },
                    { 9, "Lotus" },
                    { 10, "Sunset" },
                    { 11, "Abyss" },
                    { 12, "Corrode" },
                    { 13, "Summit" }
                });

            migrationBuilder.InsertData(
                table: "TeamRoles",
                columns: new[] { "TeamRoleId", "Code", "IsSystem", "Label", "TeamId" },
                values: new object[,]
                {
                    { 1, "Manager", true, "Manager", null },
                    { 2, "Coach", true, "Coach", null },
                    { 3, "Player", true, "Joueur", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ActivityTypes",
                keyColumn: "ActivityTypeId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "ActivityTypes",
                keyColumn: "ActivityTypeId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "ActivityTypes",
                keyColumn: "ActivityTypeId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "ActivityTypes",
                keyColumn: "ActivityTypeId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Maps",
                keyColumn: "MapId",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "TeamRoles",
                keyColumn: "TeamRoleId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "TeamRoles",
                keyColumn: "TeamRoleId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "TeamRoles",
                keyColumn: "TeamRoleId",
                keyValue: 3);
        }
    }
}
