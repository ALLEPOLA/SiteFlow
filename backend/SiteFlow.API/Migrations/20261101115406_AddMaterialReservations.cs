using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservationId",
                table: "MaterialIssueLines",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MaterialReservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClientRequestId = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    MaterialRequestId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ReleasedQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReleaseReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialReservations", x => x.Id);
                    table.CheckConstraint("CK_MaterialReservations_Quantities", "`Quantity` > 0 AND `ConsumedQuantity` >= 0 AND `ReleasedQuantity` >= 0 AND `ConsumedQuantity` + `ReleasedQuantity` <= `Quantity`");
                    table.ForeignKey(
                        name: "FK_MaterialReservations_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReservations_MaterialRequests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReservations_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReservations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialIssueLines_ReservationId",
                table: "MaterialIssueLines",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_ClientRequestId",
                table: "MaterialReservations",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_CreatedByUserId",
                table: "MaterialReservations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_MaterialId",
                table: "MaterialReservations",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_MaterialRequestId",
                table: "MaterialReservations",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReservations_ProjectId_MaterialId",
                table: "MaterialReservations",
                columns: new[] { "ProjectId", "MaterialId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialIssueLines_MaterialReservations_ReservationId",
                table: "MaterialIssueLines",
                column: "ReservationId",
                principalTable: "MaterialReservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialIssueLines_MaterialReservations_ReservationId",
                table: "MaterialIssueLines");

            migrationBuilder.DropTable(
                name: "MaterialReservations");

            migrationBuilder.DropIndex(
                name: "IX_MaterialIssueLines_ReservationId",
                table: "MaterialIssueLines");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "MaterialIssueLines");
        }
    }
}
