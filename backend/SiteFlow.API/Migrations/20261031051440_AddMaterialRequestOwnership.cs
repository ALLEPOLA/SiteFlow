using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiteFlow.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialRequestOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "MaterialRequests",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "MaterialRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_CreatedByUserId",
                table: "MaterialRequests",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_AspNetUsers_CreatedByUserId",
                table: "MaterialRequests",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_AspNetUsers_CreatedByUserId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_CreatedByUserId",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "MaterialRequests");
        }
    }
}
