using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorpoDoDesenho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorpoDoDesenho",
                table: "PerfisPersonal",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Masculino");

            migrationBuilder.AddColumn<string>(
                name: "CorpoDoDesenho",
                table: "PerfisAluno",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Masculino");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorpoDoDesenho",
                table: "PerfisPersonal");

            migrationBuilder.DropColumn(
                name: "CorpoDoDesenho",
                table: "PerfisAluno");
        }
    }
}
