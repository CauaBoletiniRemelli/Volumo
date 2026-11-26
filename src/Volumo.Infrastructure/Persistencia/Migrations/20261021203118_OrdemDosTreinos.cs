using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class OrdemDosTreinos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Ordem",
                table: "Treinos",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ordem",
                table: "Treinos");
        }
    }
}
