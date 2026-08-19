using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EsportTeamManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedLegalDocumentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "LegalDocumentVersions",
                columns: new[] { "LegalDocumentVersionId", "DocumentType", "PublishedAtUtc", "RequiresAcceptance", "VersionNumber" },
                values: new object[,]
                {
                    { 1, "TermsOfService", new DateTimeOffset(new DateTime(2026, 8, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "1.0" },
                    { 2, "PrivacyPolicy", new DateTimeOffset(new DateTime(2026, 8, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, "1.0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LegalDocumentVersions",
                keyColumn: "LegalDocumentVersionId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "LegalDocumentVersions",
                keyColumn: "LegalDocumentVersionId",
                keyValue: 2);
        }
    }
}
