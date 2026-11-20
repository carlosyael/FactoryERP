using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRawMaterialBatchesAndTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawMaterialBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "int", nullable: true),
                    BatchCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitialQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMaterialBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMaterialBatches_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMaterialBatches_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionMaterialBatchAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionOrderId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialId = table.Column<int>(type: "int", nullable: false),
                    RawMaterialBatchId = table.Column<int>(type: "int", nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionMaterialBatchAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialBatchAllocations_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialBatchAllocations_RawMaterialBatches_RawMaterialBatchId",
                        column: x => x.RawMaterialBatchId,
                        principalTable: "RawMaterialBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionMaterialBatchAllocations_RawMaterials_RawMaterialId",
                        column: x => x.RawMaterialId,
                        principalTable: "RawMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialBatchAllocations_ProductionOrderId_RawMaterialBatchId",
                table: "ProductionMaterialBatchAllocations",
                columns: new[] { "ProductionOrderId", "RawMaterialBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialBatchAllocations_RawMaterialBatchId",
                table: "ProductionMaterialBatchAllocations",
                column: "RawMaterialBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionMaterialBatchAllocations_RawMaterialId",
                table: "ProductionMaterialBatchAllocations",
                column: "RawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialBatches_BatchCode",
                table: "RawMaterialBatches",
                column: "BatchCode");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialBatches_PurchaseOrderId",
                table: "RawMaterialBatches",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialBatches_RawMaterialId_ExpirationDate",
                table: "RawMaterialBatches",
                columns: new[] { "RawMaterialId", "ExpirationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionMaterialBatchAllocations");

            migrationBuilder.DropTable(
                name: "RawMaterialBatches");
        }
    }
}
