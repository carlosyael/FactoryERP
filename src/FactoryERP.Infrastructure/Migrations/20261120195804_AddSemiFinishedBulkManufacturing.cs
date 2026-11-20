using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSemiFinishedBulkManufacturing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductFormulas_Products_ProductId",
                table: "ProductFormulas");

            migrationBuilder.AddColumn<int>(
                name: "SourceProductionOrderId",
                table: "RawMaterialBatches",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductionOrders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "OutputRawMaterialId",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductFormulas",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "OutputRawMaterialId",
                table: "ProductFormulas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RawMaterialBatches_SourceProductionOrderId",
                table: "RawMaterialBatches",
                column: "SourceProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_OutputRawMaterialId",
                table: "ProductionOrders",
                column: "OutputRawMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductFormulas_OutputRawMaterialId",
                table: "ProductFormulas",
                column: "OutputRawMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductFormulas_Products_ProductId",
                table: "ProductFormulas",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductFormulas_RawMaterials_OutputRawMaterialId",
                table: "ProductFormulas",
                column: "OutputRawMaterialId",
                principalTable: "RawMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_RawMaterials_OutputRawMaterialId",
                table: "ProductionOrders",
                column: "OutputRawMaterialId",
                principalTable: "RawMaterials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RawMaterialBatches_ProductionOrders_SourceProductionOrderId",
                table: "RawMaterialBatches",
                column: "SourceProductionOrderId",
                principalTable: "ProductionOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductFormulas_Products_ProductId",
                table: "ProductFormulas");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductFormulas_RawMaterials_OutputRawMaterialId",
                table: "ProductFormulas");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_RawMaterials_OutputRawMaterialId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_RawMaterialBatches_ProductionOrders_SourceProductionOrderId",
                table: "RawMaterialBatches");

            migrationBuilder.DropIndex(
                name: "IX_RawMaterialBatches_SourceProductionOrderId",
                table: "RawMaterialBatches");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_OutputRawMaterialId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductFormulas_OutputRawMaterialId",
                table: "ProductFormulas");

            migrationBuilder.DropColumn(
                name: "SourceProductionOrderId",
                table: "RawMaterialBatches");

            migrationBuilder.DropColumn(
                name: "OutputRawMaterialId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "OutputRawMaterialId",
                table: "ProductFormulas");

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductionOrders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "ProductFormulas",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductFormulas_Products_ProductId",
                table: "ProductFormulas",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
