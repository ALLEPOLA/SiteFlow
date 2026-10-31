using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderIssuing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAtUtc",
                table: "PurchaseOrders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuedByUserId",
                table: "PurchaseOrders",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_IssuedByUserId",
                table: "PurchaseOrders",
                column: "IssuedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_IssuedByUserId",
                table: "PurchaseOrders",
                column: "IssuedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_AspNetUsers_IssuedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_IssuedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IssuedAtUtc",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IssuedByUserId",
                table: "PurchaseOrders");
        }
    }
}
