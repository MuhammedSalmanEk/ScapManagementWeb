using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerStockPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanViewStock",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanViewStock",
                table: "AspNetUsers");
        }
    }
}
