using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWholesaleRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RateType",
                table: "PurchaseBills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WholesaleRatePerKg",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RateType",
                table: "PurchaseBills");

            migrationBuilder.DropColumn(
                name: "WholesaleRatePerKg",
                table: "Products");
        }
    }
}
