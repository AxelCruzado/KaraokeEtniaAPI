using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KaraokeEtniaApi.Infrastructure.Postgres.Migrations.Caja
{
    /// <inheritdoc />
    public partial class PostgresCajaInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Caja_TurnoCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Caja = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CajaActiva = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Fin = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AperturaRegistradaUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FondoInicial = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Responsable = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Estado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_TurnoCaja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Caja_VentaCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Referencia = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PedidoHistorialId = table.Column<int>(type: "integer", nullable: true),
                    Total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Estado = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caja_VentaCaja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Caja_AuditoriaCaja",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TurnoId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EntidadCodigo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Accion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Usuario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AnteriorJson = table.Column<string>(type: "text", maxLength: 16000, nullable: false),
                    PosteriorJson = table.Column<string>(type: "text", maxLength: 16000, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Caja_CierreCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TurnoId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Responsable = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Contado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Esperado = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", maxLength: 16000, nullable: false),
                    ClaveOperacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Caja_ReporteCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TurnoId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    GeneradoUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Usuario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", maxLength: 16000, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Caja_MovimientoCaja",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TurnoId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VentaId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Tipo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Naturaleza = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Concepto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Categoria = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Documento = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    FechaOperacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RegistradoUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Usuario = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClaveOperacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HuellaSolicitud = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Anulado = table.Column<bool>(type: "boolean", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Caja_PagoCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MovimientoCajaId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Medio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
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
                });

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
