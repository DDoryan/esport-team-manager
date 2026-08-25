using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EsportTeamManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetEmailRateLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PasswordResetEmailCount",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetEmailWindowStartedAtUtc",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_PasswordResetEmailCount",
                table: "AspNetUsers",
                sql: "\"PasswordResetEmailCount\" >= 0 AND \"PasswordResetEmailCount\" <= 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_PasswordResetEmailCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetEmailCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasswordResetEmailWindowStartedAtUtc",
                table: "AspNetUsers");
        }
    }
}
