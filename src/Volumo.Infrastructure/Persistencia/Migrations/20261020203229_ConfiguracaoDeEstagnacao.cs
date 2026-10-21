using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoDeEstagnacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CriterioDeEstagnacao",
                table: "VinculosPersonalAluno",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Tempo");

            migrationBuilder.AddColumn<int>(
                name: "LimiteDeEstagnacao",
                table: "VinculosPersonalAluno",
                type: "int",
                nullable: false,
                defaultValue: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriterioDeEstagnacao",
                table: "VinculosPersonalAluno");

            migrationBuilder.DropColumn(
                name: "LimiteDeEstagnacao",
                table: "VinculosPersonalAluno");
        }
    }
}
