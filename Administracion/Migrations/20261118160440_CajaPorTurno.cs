using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeEtniaApi.Administracion.Migrations
{
    /// <inheritdoc />
    public partial class CajaPorTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_TurnoCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Codigo = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Caja = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CajaActiva = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Inicio = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Fin = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AperturaRegistradaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Responsable = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Estado = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_TurnoCaja", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_VentaCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Referencia = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PedidoHistorialId = table.Column<int>(type: "int", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_VentaCaja", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_AuditoriaCaja",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TurnoId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntidadCodigo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Accion = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Usuario = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegistradoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Motivo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AnteriorJson = table.Column<string>(type: "longtext", maxLength: 16000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PosteriorJson = table.Column<string>(type: "longtext", maxLength: 16000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_AuditoriaCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caja_AuditoriaCaja_Caja_TurnoCaja_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Caja_TurnoCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_CierreCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Codigo = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TurnoId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Responsable = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Contado = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Esperado = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    SnapshotJson = table.Column<string>(type: "longtext", maxLength: 16000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaveOperacion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_CierreCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caja_CierreCaja_Caja_TurnoCaja_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Caja_TurnoCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_ReporteCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Codigo = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TurnoId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    GeneradoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Usuario = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SnapshotJson = table.Column<string>(type: "longtext", maxLength: 16000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_ReporteCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caja_ReporteCaja_Caja_TurnoCaja_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Caja_TurnoCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_MovimientoCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Codigo = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TurnoId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VentaId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Naturaleza = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Concepto = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Categoria = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Documento = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Importe = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    FechaOperacion = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Usuario = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClaveOperacion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HuellaSolicitud = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Anulado = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_MovimientoCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caja_MovimientoCaja_Caja_TurnoCaja_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Caja_TurnoCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Caja_MovimientoCaja_Caja_VentaCaja_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Caja_VentaCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Caja_PagoCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MovimientoCajaId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Medio = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Importe = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_PagoCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caja_PagoCaja_Caja_MovimientoCaja_MovimientoCajaId",
                        column: x => x.MovimientoCajaId,
                        principalTable: "Caja_MovimientoCaja",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Caja_AuditoriaCaja_TurnoId",
                table: "Caja_AuditoriaCaja",
                column: "TurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_Caja_CierreCaja_TurnoId_ClaveOperacion",
                table: "Caja_CierreCaja",
                columns: new[] { "TurnoId", "ClaveOperacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_CierreCaja_TurnoId_Version",
                table: "Caja_CierreCaja",
                columns: new[] { "TurnoId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_MovimientoCaja_Codigo",
                table: "Caja_MovimientoCaja",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_MovimientoCaja_TurnoId_ClaveOperacion",
                table: "Caja_MovimientoCaja",
                columns: new[] { "TurnoId", "ClaveOperacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_MovimientoCaja_VentaId",
                table: "Caja_MovimientoCaja",
                column: "VentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_PagoCaja_MovimientoCajaId",
                table: "Caja_PagoCaja",
                column: "MovimientoCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Caja_ReporteCaja_TurnoId_Version",
                table: "Caja_ReporteCaja",
                columns: new[] { "TurnoId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_TurnoCaja_Caja_Inicio",
                table: "Caja_TurnoCaja",
                columns: new[] { "Caja", "Inicio" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_TurnoCaja_CajaActiva",
                table: "Caja_TurnoCaja",
                column: "CajaActiva",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_TurnoCaja_Codigo",
                table: "Caja_TurnoCaja",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_VentaCaja_PedidoHistorialId",
                table: "Caja_VentaCaja",
                column: "PedidoHistorialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caja_VentaCaja_Referencia",
                table: "Caja_VentaCaja",
                column: "Referencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Caja_AuditoriaCaja");

            migrationBuilder.DropTable(
                name: "Caja_CierreCaja");

            migrationBuilder.DropTable(
                name: "Caja_PagoCaja");

            migrationBuilder.DropTable(
                name: "Caja_ReporteCaja");

            migrationBuilder.DropTable(
                name: "Caja_MovimientoCaja");

            migrationBuilder.DropTable(
                name: "Caja_TurnoCaja");

            migrationBuilder.DropTable(
                name: "Caja_VentaCaja");
        }
    }
}
