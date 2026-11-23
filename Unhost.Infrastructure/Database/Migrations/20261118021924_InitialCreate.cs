using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unhost.Infrastructure.Database.Migrations;

/// <inheritdoc />
public partial class _20261118021924_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", nullable: false),
                UserName = table.Column<string>(type: "TEXT", nullable: true),
                NormalizedUserName = table.Column<string>(type: "TEXT", nullable: true),
                Email = table.Column<string>(type: "TEXT", nullable: true),
                NormalizedEmail = table.Column<string>(type: "TEXT", nullable: true),
                EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "InstallationSetup",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InstallationSetup", x => x.Id);
                table.CheckConstraint("CK_InstallationSetup_SingleRow", "Id = 1");
            });

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "InstallationSetup");
    }
}
