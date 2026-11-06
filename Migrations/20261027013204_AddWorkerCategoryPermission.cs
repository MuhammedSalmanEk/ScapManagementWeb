using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrapManagement.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerCategoryPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanEditProducts",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanManageCategories",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanEditProducts",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CanManageCategories",
                table: "AspNetUsers");
        }
    }
}
