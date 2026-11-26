using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Cardio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MomentoDoCardio",
                table: "Treinos",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Depois");

            migrationBuilder.AddColumn<string>(
                name: "MomentoDoCardio",
                table: "TemplatesTreino",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Depois");

            migrationBuilder.CreateTable(
                name: "CardiosTemplateTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardiosTemplateTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardiosTemplateTreino_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardiosTemplateTreino_TemplatesTreino_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "TemplatesTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CardiosTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardiosTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CardiosTreino_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CardiosTreino_Treinos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Treinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosCardio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DuracaoMinutos = table.Column<int>(type: "int", nullable: false),
                    RealizadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosCardio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosCardio_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosCardio_SessoesTreino_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessoesTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Exercicios",
                columns: new[] { "Id", "GrupoMuscular", "InativadoEm", "Nome", "PersonalId" },
                values: new object[,]
                {
                    { new Guid("0199ecb1-0000-7000-8000-00000000002b"), "Cardio", null, "Elíptico", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000002c"), "Cardio", null, "Remo ergômetro", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000002d"), "Cardio", null, "Escada ergométrica", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000002e"), "Cardio", null, "Bicicleta de spinning", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000002f"), "Cardio", null, "Pular corda", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000030"), "Cardio", null, "Caminhada ao ar livre", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000031"), "Cardio", null, "Corrida ao ar livre", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardiosTemplateTreino_ExercicioId",
                table: "CardiosTemplateTreino",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_CardiosTemplateTreino_PlanoId",
                table: "CardiosTemplateTreino",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_CardiosTreino_ExercicioId",
                table: "CardiosTreino",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_CardiosTreino_PlanoId",
                table: "CardiosTreino",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCardio_ExercicioId",
                table: "RegistrosCardio",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosCardio_SessaoId",
                table: "RegistrosCardio",
                column: "SessaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardiosTemplateTreino");

            migrationBuilder.DropTable(
                name: "CardiosTreino");

            migrationBuilder.DropTable(
                name: "RegistrosCardio");

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002b"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002c"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002d"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002e"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002f"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000030"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000031"));

            migrationBuilder.DropColumn(
                name: "MomentoDoCardio",
                table: "Treinos");

            migrationBuilder.DropColumn(
                name: "MomentoDoCardio",
                table: "TemplatesTreino");
        }
    }
}
