using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class DetalhesDoExercicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Equipamento",
                table: "Exercicios",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoChave",
                table: "Exercicios",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instrucoes",
                table: "Exercicios",
                type: "nvarchar(3009)",
                maxLength: 3009,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusculosSecundarios",
                table: "Exercicios",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoChave",
                table: "Exercicios",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000001"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000002"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000003"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000004"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000005"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000006"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000007"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000008"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000009"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000a"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000b"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000c"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000d"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000e"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000000f"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000010"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000011"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000012"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000013"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000014"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000015"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000016"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000017"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000018"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000019"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001a"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001b"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001c"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001d"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001e"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000001f"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000020"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000021"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000022"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000023"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000024"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000025"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000026"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000027"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000028"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000029"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002a"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002b"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002c"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002d"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002e"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-00000000002f"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000030"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "Exercicios",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb1-0000-7000-8000-000000000031"),
                columns: new[] { "Equipamento", "FotoChave", "VideoChave", "Instrucoes", "MusculosSecundarios" },
                values: new object[] { null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Equipamento",
                table: "Exercicios");

            migrationBuilder.DropColumn(
                name: "FotoChave",
                table: "Exercicios");

            migrationBuilder.DropColumn(
                name: "Instrucoes",
                table: "Exercicios");

            migrationBuilder.DropColumn(
                name: "MusculosSecundarios",
                table: "Exercicios");

            migrationBuilder.DropColumn(
                name: "VideoChave",
                table: "Exercicios");
        }
    }
}
