using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeEtniaApi.Infrastructure.Postgres.Migrations.App
{
    /// <inheritdoc />
    public partial class CartaProductosYConsumos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "PedidoDetallesHistorial",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Descripcion",
                table: "PedidoDetalles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CartaProductos",
                columns: table => new
                {
                    ProductoId = table.Column<int>(type: "integer", nullable: false),
                    Codigo = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Unidad = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Vendible = table.Column<bool>(type: "boolean", nullable: false),
                    HieloInicial = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartaProductos", x => x.ProductoId);
                    table.ForeignKey(
                        name: "FK_CartaProductos_Productos_ProductoId",
                        column: x => x.ProductoId,
                        principalTable: "Productos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CartaRegistros",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Clave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MesaId = table.Column<int>(type: "integer", nullable: true),
                    PedidoId = table.Column<int>(type: "integer", nullable: true),
                    Usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartaRegistros", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartaProductos_Codigo",
                table: "CartaProductos",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartaRegistros_Clave",
                table: "CartaRegistros",
                column: "Clave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartaProductos");

            migrationBuilder.DropTable(
                name: "CartaRegistros");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "PedidoDetallesHistorial");

            migrationBuilder.DropColumn(
                name: "Descripcion",
                table: "PedidoDetalles");
        }
    }
}
