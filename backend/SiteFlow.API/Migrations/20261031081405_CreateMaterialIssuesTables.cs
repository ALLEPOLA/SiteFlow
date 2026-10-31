using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class CreateMaterialIssuesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_TypeQuantity",
                table: "StockMovements");

            migrationBuilder.AddColumn<int>(
                name: "MaterialIssueLineId",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientRequestId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedBy = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialIssues_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialIssues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MaterialIssueLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MaterialIssueId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialIssueLines", x => x.Id);
                    table.CheckConstraint("CK_MaterialIssueLines_Quantity", "`Quantity` > 0");
                    table.CheckConstraint("CK_MaterialIssueLines_UnitCost", "`UnitCost` >= 0");
                    table.ForeignKey(
                        name: "FK_MaterialIssueLines_MaterialIssues_MaterialIssueId",
                        column: x => x.MaterialIssueId,
                        principalTable: "MaterialIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaterialIssueLines_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MaterialIssueLineId",
                table: "StockMovements",
                column: "MaterialIssueLineId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_TypeQuantity",
                table: "StockMovements",
                sql: "(\r\n    `MovementType` = 'Receipt'\r\n    AND `Quantity` > 0\r\n    AND `GoodsReceiptLineId` IS NOT NULL\r\n    AND `MaterialIssueLineId` IS NULL\r\n)\r\nOR\r\n(\r\n    `MovementType` = 'Issue'\r\n    AND `Quantity` < 0\r\n    AND `GoodsReceiptLineId` IS NULL\r\n    AND `MaterialIssueLineId` IS NOT NULL\r\n)");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueLines_MaterialId",
                table: "MaterialIssueLines",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueLines_MaterialIssueId_MaterialId",
                table: "MaterialIssueLines",
                columns: new[] { "MaterialIssueId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_ClientRequestId",
                table: "MaterialIssues",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_CreatedByUserId",
                table: "MaterialIssues",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssues_ProjectId",
                table: "MaterialIssues",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_MaterialIssueLines_MaterialIssueLineId",
                table: "StockMovements",
                column: "MaterialIssueLineId",
                principalTable: "MaterialIssueLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_MaterialIssueLines_MaterialIssueLineId",
                table: "StockMovements");

            migrationBuilder.DropTable(
                name: "MaterialIssueLines");

            migrationBuilder.DropTable(
                name: "MaterialIssues");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_MaterialIssueLineId",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_TypeQuantity",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "MaterialIssueLineId",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_TypeQuantity",
                table: "StockMovements",
                sql: "(\r\n    `MovementType` = 'Receipt'\r\n    AND `Quantity` > 0\r\n    AND `GoodsReceiptLineId` IS NOT NULL\r\n)\r\nOR\r\n(\r\n    `MovementType` = 'Issue'\r\n    AND `Quantity` < 0\r\n    AND `GoodsReceiptLineId` IS NULL\r\n)");
        }
    }
}
