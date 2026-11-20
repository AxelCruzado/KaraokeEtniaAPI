using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeEtniaApi.Administracion.Migrations
{
    /// <inheritdoc />
    public partial class TurnosAutomaticosYCobros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PedidoOrigenId",
                table: "Caja_VentaCaja",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AtencionExtraAutorizadaUtc",
                table: "Caja_TurnoCaja",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AtencionExtraHasta",
                table: "Caja_TurnoCaja",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtencionExtraPor",
                table: "Caja_TurnoCaja",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Automatico",
                table: "Caja_TurnoCaja",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizadoAutomaticamenteUtc",
                table: "Caja_TurnoCaja",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FondoDeclaradoPor",
                table: "Caja_TurnoCaja",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "FondoDeclaradoUtc",
                table: "Caja_TurnoCaja",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAtencionExtra",
                table: "Caja_TurnoCaja",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "VersionExcepcion",
                table: "Caja_TurnoCaja",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VersionFondo",
                table: "Caja_TurnoCaja",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EstadoConformidad",
                table: "Caja_CierreCaja",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Observaciones",
                table: "Caja_CierreCaja",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PlinVerificado",
                table: "Caja_CierreCaja",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TarjetaVerificado",
                table: "Caja_CierreCaja",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YapeVerificado",
                table: "Caja_CierreCaja",
                type: "decimal(14,2)",
                precision: 14,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql("UPDATE `Caja_TurnoCaja` SET `FondoDeclaradoUtc`=`AperturaRegistradaUtc`, `FondoDeclaradoPor`=`Responsable`, `VersionFondo`=1;");

            migrationBuilder.CreateTable(
                name: "Caja_CalendarioCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProximaFecha = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_CalendarioCaja", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Caja_CalendarioCaja",
                columns: new[] { "Id", "ProximaFecha" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_Caja_VentaCaja_PedidoOrigenId",
                table: "Caja_VentaCaja",
                column: "PedidoOrigenId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Caja_CalendarioCaja");

            migrationBuilder.DropIndex(
                name: "IX_Caja_VentaCaja_PedidoOrigenId",
                table: "Caja_VentaCaja");

            migrationBuilder.DropColumn(
                name: "PedidoOrigenId",
                table: "Caja_VentaCaja");

            migrationBuilder.DropColumn(
                name: "AtencionExtraAutorizadaUtc",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "AtencionExtraHasta",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "AtencionExtraPor",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "Automatico",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "FinalizadoAutomaticamenteUtc",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "FondoDeclaradoPor",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "FondoDeclaradoUtc",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "MotivoAtencionExtra",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "VersionExcepcion",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "VersionFondo",
                table: "Caja_TurnoCaja");

            migrationBuilder.DropColumn(
                name: "EstadoConformidad",
                table: "Caja_CierreCaja");

            migrationBuilder.DropColumn(
                name: "Observaciones",
                table: "Caja_CierreCaja");

            migrationBuilder.DropColumn(
                name: "PlinVerificado",
                table: "Caja_CierreCaja");

            migrationBuilder.DropColumn(
                name: "TarjetaVerificado",
                table: "Caja_CierreCaja");

            migrationBuilder.DropColumn(
                name: "YapeVerificado",
                table: "Caja_CierreCaja");
        }
    }
}
