using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeNoeliasApi.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePedidosHistorialAndDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "PagoCliente",
                table: "PedidosActivos",
                type: "DECIMAL(10,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "MetodoPago",
                table: "PedidosActivos",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "PedidosHistorial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MesaId = table.Column<int>(type: "INT", nullable: false),
                    Total = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    CerradoEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetodoPago = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PagoCliente = table.Column<decimal>(type: "DECIMAL(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidosHistorial_Mesas_MesaId",
                        column: x => x.MesaId,
                        principalTable: "Mesas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PedidoDetallesHistorial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoHistorialId = table.Column<int>(type: "INT", nullable: false),
                    ProductoId = table.Column<int>(type: "INT", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<decimal>(type: "DECIMAL(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidoDetallesHistorial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PedidoDetallesHistorial_PedidosHistorial_PedidoHistorialId",
                        column: x => x.PedidoHistorialId,
                        principalTable: "PedidosHistorial",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidoDetallesHistorial_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetallesHistorial_PedidoHistorialId",
                table: "PedidoDetallesHistorial",
                column: "PedidoHistorialId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidoDetallesHistorial_ProductoId",
                table: "PedidoDetallesHistorial",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosHistorial_MesaId",
                table: "PedidosHistorial",
                column: "MesaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidoDetallesHistorial");

            migrationBuilder.DropTable(
                name: "PedidosHistorial");

            migrationBuilder.AlterColumn<decimal>(
                name: "PagoCliente",
                table: "PedidosActivos",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(10,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "MetodoPago",
                table: "PedidosActivos",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
