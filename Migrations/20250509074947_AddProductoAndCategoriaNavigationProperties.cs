using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KaraokeNoeliasApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoAndCategoriaNavigationProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Imagen",
                table: "Productos",
                newName: "ImagenUrl");

            migrationBuilder.RenameColumn(
                name: "Imagen",
                table: "Categorias",
                newName: "ImagenUrl");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImagenUrl",
                table: "Productos",
                newName: "Imagen");

            migrationBuilder.RenameColumn(
                name: "ImagenUrl",
                table: "Categorias",
                newName: "Imagen");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Categorias",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
