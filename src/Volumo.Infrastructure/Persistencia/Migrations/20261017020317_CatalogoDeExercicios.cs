using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoDeExercicios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Exercicios",
                columns: new[] { "Id", "GrupoMuscular", "InativadoEm", "Nome", "PersonalId" },
                values: new object[,]
                {
                    { new Guid("0199ecb1-0000-7000-8000-000000000001"), "Peitoral", null, "Supino reto com barra", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000002"), "Peitoral", null, "Supino inclinado com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000003"), "Peitoral", null, "Crucifixo com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000004"), "Peitoral", null, "Crossover na polia", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000005"), "Peitoral", null, "Flexão de braço", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000006"), "Costas", null, "Puxada frontal na polia", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000007"), "Costas", null, "Remada curvada com barra", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000008"), "Costas", null, "Remada baixa na polia", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000009"), "Costas", null, "Remada unilateral com halter", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000a"), "Costas", null, "Barra fixa", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000b"), "Ombros", null, "Desenvolvimento com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000c"), "Ombros", null, "Elevação lateral com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000d"), "Ombros", null, "Elevação frontal com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000e"), "Ombros", null, "Crucifixo invertido", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000000f"), "Trapezio", null, "Encolhimento com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000010"), "Biceps", null, "Rosca direta com barra", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000011"), "Biceps", null, "Rosca alternada com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000012"), "Biceps", null, "Rosca martelo", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000013"), "Triceps", null, "Tríceps na polia com corda", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000014"), "Triceps", null, "Tríceps testa", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000015"), "Triceps", null, "Mergulho no banco", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000016"), "Antebraco", null, "Rosca de punho", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000017"), "Quadriceps", null, "Agachamento livre", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000018"), "Quadriceps", null, "Leg press 45°", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000019"), "Quadriceps", null, "Cadeira extensora", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001a"), "Quadriceps", null, "Afundo com halteres", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001b"), "Posteriores", null, "Stiff com barra", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001c"), "Posteriores", null, "Mesa flexora", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001d"), "Posteriores", null, "Cadeira flexora", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001e"), "Gluteos", null, "Elevação pélvica", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000001f"), "Gluteos", null, "Glúteo na polia", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000020"), "Adutores", null, "Cadeira adutora", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000021"), "Abdutores", null, "Cadeira abdutora", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000022"), "Panturrilhas", null, "Panturrilha em pé", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000023"), "Panturrilhas", null, "Panturrilha sentado", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000024"), "Abdomen", null, "Prancha", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000025"), "Abdomen", null, "Abdominal supra", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000026"), "Abdomen", null, "Elevação de pernas", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000027"), "Lombar", null, "Hiperextensão lombar", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000028"), "CorpoInteiro", null, "Levantamento terra", null },
                    { new Guid("0199ecb1-0000-7000-8000-000000000029"), "Cardio", null, "Esteira", null },
                    { new Guid("0199ecb1-0000-7000-8000-00000000002a"), "Cardio", null, "Bicicleta ergométrica", null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000a"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000b"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000c"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000d"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000e"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000f"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000013"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000015"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000016"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000017"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000018"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000019"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001a"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001b"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001c"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001d"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001e"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001f"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000020"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000021"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000022"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000023"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000024"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000025"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000026"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000027"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000028"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000029"));

            migrationBuilder.DeleteData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002a"));
        }
    }
}
