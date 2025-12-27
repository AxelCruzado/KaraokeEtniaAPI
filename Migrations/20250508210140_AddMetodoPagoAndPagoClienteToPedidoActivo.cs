using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeNoeliasApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMetodoPagoAndPagoClienteToPedidoActivo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetodoPago",
                table: "PedidosActivos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PagoCliente",
                table: "PedidosActivos",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetodoPago",
                table: "PedidosActivos");

            migrationBuilder.DropColumn(
                name: "PagoCliente",
                table: "PedidosActivos");
        }
    }
}
