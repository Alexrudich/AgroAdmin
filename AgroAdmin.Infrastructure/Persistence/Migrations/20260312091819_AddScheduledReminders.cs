using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgroAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    TargetChatId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReminders_ScheduledFor_IsSent",
                table: "ScheduledReminders",
                columns: new[] { "ScheduledFor", "IsSent" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledReminders");
        }
    }
}
