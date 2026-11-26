using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AnotacoesDoAluno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnotacoesDoAluno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TreinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AtualizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnotacoesDoAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnotacoesDoAluno_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnotacoesDoAluno_PerfisAluno_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "PerfisAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnotacoesDoAluno_Treinos_TreinoId",
                        column: x => x.TreinoId,
                        principalTable: "Treinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnotacoesDoAluno_AlunoId",
                table: "AnotacoesDoAluno",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_AnotacoesDoAluno_ExercicioId",
                table: "AnotacoesDoAluno",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_AnotacoesDoAluno_TreinoId_ExercicioId",
                table: "AnotacoesDoAluno",
                columns: new[] { "TreinoId", "ExercicioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnotacoesDoAluno");
        }
    }
}
