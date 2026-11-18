using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeEtniaApi.Migrations
{
    /// <inheritdoc />
    public partial class VersionCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Compras",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Compras");
        }
    }
}
