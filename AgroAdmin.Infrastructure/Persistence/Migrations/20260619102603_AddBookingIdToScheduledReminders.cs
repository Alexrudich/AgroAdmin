using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgroAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingIdToScheduledReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "ScheduledReminders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReminders_BookingId",
                table: "ScheduledReminders",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledReminders_Bookings_BookingId",
                table: "ScheduledReminders");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledReminders_ScheduledReminders_BookingId",
                table: "ScheduledReminders",
                column: "BookingId",
                principalTable: "ScheduledReminders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
