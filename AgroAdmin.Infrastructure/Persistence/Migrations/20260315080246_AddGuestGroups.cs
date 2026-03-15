using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgroAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestGroupMembers",
                columns: table => new
                {
                    GuestId = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestGroupMembers", x => new { x.GuestId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_GuestGroupMembers_GuestGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "GuestGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestGroupMembers_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestGroupMembers_GroupId",
                table: "GuestGroupMembers",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestGroupMembers");

            migrationBuilder.DropTable(
                name: "GuestGroups");
        }
    }
}
