using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgroAdmin.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MinPricePerNightFullHouse = table.Column<int>(type: "int", nullable: false),
                    PricePerAdultFullHouse = table.Column<int>(type: "int", nullable: false),
                    IncludedAdultsFullHouse = table.Column<int>(type: "int", nullable: false),
                    MinPricePerNightHalf = table.Column<int>(type: "int", nullable: false),
                    PricePerAdultHalf = table.Column<int>(type: "int", nullable: false),
                    IncludedAdultsHalf = table.Column<int>(type: "int", nullable: false),
                    SaunaPrice = table.Column<int>(type: "int", nullable: false),
                    BanquetHallPrice = table.Column<int>(type: "int", nullable: false),
                    DogFee = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingConfigurations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingConfigurations");
        }
    }
}
