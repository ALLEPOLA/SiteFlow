using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStoresAndLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StorageLocationId",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StorageLocationId",
                table: "MaterialReservations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StorageLocationId",
                table: "MaterialIssues",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DestinationStorageLocationId",
                table: "InventoryOperations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageLocationId",
                table: "InventoryOperations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StorageLocationId",
                table: "GoodsReceipts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stores_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StorageLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StoreId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rack = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bin = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageLocations_Stores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Preserve legacy quantities and values; assign their system default location.
            migrationBuilder.Sql("""
                INSERT INTO `Stores` (`ProjectId`, `Code`, `Name`, `IsActive`) SELECT `Id`, 'MAIN', 'Main Store', 1 FROM `Projects`;
                INSERT INTO `StorageLocations` (`StoreId`, `Code`, `Name`, `Rack`, `Bin`, `IsActive`) SELECT `Id`, 'DEFAULT', 'Default Location', '', '', 1 FROM `Stores` WHERE `Code` = 'MAIN';
                UPDATE `StockMovements` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
                UPDATE `MaterialReservations` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
                UPDATE `MaterialIssues` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
                UPDATE `InventoryOperations` t JOIN `Stores` s ON s.`ProjectId` = t.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET t.`StorageLocationId` = l.`Id`;
                UPDATE `GoodsReceipts` g JOIN `PurchaseOrders` o ON o.`Id` = g.`PurchaseOrderId` JOIN `MaterialRequests` r ON r.`Id` = o.`MaterialRequestId` JOIN `Stores` s ON s.`ProjectId` = r.`ProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET g.`StorageLocationId` = l.`Id`;
                UPDATE `InventoryOperations` o JOIN `Stores` s ON s.`ProjectId` = o.`DestinationProjectId` AND s.`Code` = 'MAIN' JOIN `StorageLocations` l ON l.`StoreId` = s.`Id` AND l.`Code` = 'DEFAULT' SET o.`DestinationStorageLocationId` = l.`Id` WHERE o.`DestinationProjectId` IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StorageLocationId_MaterialId",
                table: "StockMovements",
                columns: new[] { "StorageLocationId", "MaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_StorageLocationId_MaterialId",
                table: "MaterialReservations",
                columns: new[] { "StorageLocationId", "MaterialId" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_StorageLocationId",
                table: "MaterialIssues",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOperations_DestinationStorageLocationId",
                table: "InventoryOperations",
                column: "DestinationStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryOperations_StorageLocationId",
                table: "InventoryOperations",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_StorageLocationId",
                table: "GoodsReceipts",
                column: "StorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageLocations_StoreId_Code",
                table: "StorageLocations",
                columns: new[] { "StoreId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_ProjectId_Code",
                table: "Stores",
                columns: new[] { "ProjectId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_StorageLocations_StorageLocationId",
                table: "GoodsReceipts",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryOperations_StorageLocations_DestinationStorageLocat~",
                table: "InventoryOperations",
                column: "DestinationStorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryOperations_StorageLocations_StorageLocationId",
                table: "InventoryOperations",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialIssues_StorageLocations_StorageLocationId",
                table: "MaterialIssues",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialReservations_StorageLocations_StorageLocationId",
                table: "MaterialReservations",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_StorageLocations_StorageLocationId",
                table: "StockMovements",
                column: "StorageLocationId",
                principalTable: "StorageLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_StorageLocations_StorageLocationId",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryOperations_StorageLocations_DestinationStorageLocat~",
                table: "InventoryOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryOperations_StorageLocations_StorageLocationId",
                table: "InventoryOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialIssues_StorageLocations_StorageLocationId",
                table: "MaterialIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialReservations_StorageLocations_StorageLocationId",
                table: "MaterialReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_StorageLocations_StorageLocationId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "StorageLocations");

            migrationBuilder.DropTable(
                name: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_StorageLocationId_MaterialId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_MaterialReservations_StorageLocationId_MaterialId",
                table: "MaterialReservations");

            migrationBuilder.DropIndex(
                name: "IX_MaterialIssues_StorageLocationId",
                table: "MaterialIssues");

            migrationBuilder.DropIndex(
                name: "IX_InventoryOperations_DestinationStorageLocationId",
                table: "InventoryOperations");

            migrationBuilder.DropIndex(
                name: "IX_InventoryOperations_StorageLocationId",
                table: "InventoryOperations");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_StorageLocationId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "MaterialReservations");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "MaterialIssues");

            migrationBuilder.DropColumn(
                name: "DestinationStorageLocationId",
                table: "InventoryOperations");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "InventoryOperations");

            migrationBuilder.DropColumn(
                name: "StorageLocationId",
                table: "GoodsReceipts");
        }
    }
}
