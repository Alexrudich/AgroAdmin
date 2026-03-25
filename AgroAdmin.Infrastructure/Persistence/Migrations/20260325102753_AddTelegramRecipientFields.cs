using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgroAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramRecipientFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommandCountToday",
                table: "TelegramRecipients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "TelegramRecipients",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TelegramRecipients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActiveAt",
                table: "TelegramRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCommandAt",
                table: "TelegramRecipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "TelegramRecipients",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandCountToday",
                table: "TelegramRecipients");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TelegramRecipients");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TelegramRecipients");

            migrationBuilder.DropColumn(
                name: "LastActiveAt",
                table: "TelegramRecipients");

            migrationBuilder.DropColumn(
                name: "LastCommandAt",
                table: "TelegramRecipients");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "TelegramRecipients");
        }
    }
}
