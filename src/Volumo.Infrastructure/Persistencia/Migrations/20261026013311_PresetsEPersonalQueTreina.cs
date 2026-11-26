using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class PresetsEPersonalQueTreina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PresetsDeSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InativadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresetsDeSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresetsDeSeries_AspNetUsers_DonoId",
                        column: x => x.DonoId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeriesPresetDeSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RepeticoesMinimas = table.Column<int>(type: "int", nullable: false),
                    RepeticoesMaximas = table.Column<int>(type: "int", nullable: false),
                    DescansoSegundos = table.Column<int>(type: "int", nullable: false),
                    CargaKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    PresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesPresetDeSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesPresetDeSeries_PresetsDeSeries_PresetId",
                        column: x => x.PresetId,
                        principalTable: "PresetsDeSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresetsDeSeries_DonoId",
                table: "PresetsDeSeries",
                column: "DonoId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesPresetDeSeries_PresetId",
                table: "SeriesPresetDeSeries",
                column: "PresetId");

            // Personal também treina: os personais que já existiam ganham o perfil e o papel de aluno
            // (o cadastro novo já cria os dois). Mesmo nome, mesma situação (pausado continua pausado)
            // e mesmo desenho do corpo; contas anonimizadas ficam de fora.
            migrationBuilder.Sql("""
                INSERT INTO [PerfisAluno] ([Id], [Nome], [InativadoEm], [AnonimizadoEm], [CorpoDoDesenho])
                SELECT p.[Id], p.[Nome], p.[InativadoEm], NULL, p.[CorpoDoDesenho]
                FROM [PerfisPersonal] p
                WHERE p.[AnonimizadoEm] IS NULL
                  AND NOT EXISTS (SELECT 1 FROM [PerfisAluno] a WHERE a.[Id] = p.[Id]);
                """);

            // Id fixo do papel Aluno (IdentityConfiguracao.AlunoId).
            migrationBuilder.Sql("""
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT p.[Id], '0199ecb0-0000-7000-8000-000000000001'
                FROM [PerfisPersonal] p
                WHERE p.[AnonimizadoEm] IS NULL
                  AND NOT EXISTS (SELECT 1 FROM [AspNetUserRoles] r WHERE r.[UserId] = p.[Id] AND r.[RoleId] = '0199ecb0-0000-7000-8000-000000000001');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O perfil e o papel de aluno dados aos personais ficam: podem já ter treinos e sessões.

            migrationBuilder.DropTable(
                name: "SeriesPresetDeSeries");

            migrationBuilder.DropTable(
                name: "PresetsDeSeries");
        }
    }
}
