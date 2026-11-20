using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinishedBatchQualityControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "QualityCheckedAt",
                table: "FinishedBatches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualityCheckedBy",
                table: "FinishedBatches",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QualityNotes",
                table: "FinishedBatches",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "QualityStatus",
                table: "FinishedBatches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_FinishedBatches_QualityStatus",
                table: "FinishedBatches",
                column: "QualityStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinishedBatches_QualityStatus",
                table: "FinishedBatches");

            migrationBuilder.DropColumn(
                name: "QualityCheckedAt",
                table: "FinishedBatches");

            migrationBuilder.DropColumn(
                name: "QualityCheckedBy",
                table: "FinishedBatches");

            migrationBuilder.DropColumn(
                name: "QualityNotes",
                table: "FinishedBatches");

            migrationBuilder.DropColumn(
                name: "QualityStatus",
                table: "FinishedBatches");
        }
    }
}
