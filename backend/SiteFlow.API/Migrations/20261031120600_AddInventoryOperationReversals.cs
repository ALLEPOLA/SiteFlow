using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryOperationReversals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "InventoryOperations",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Posted")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "InventoryOperations");
        }
    }
}
