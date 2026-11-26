using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class InativacaoEAnonimizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerfisAluno_AspNetUsers_Id",
                table: "PerfisAluno");

            migrationBuilder.DropForeignKey(
                name: "FK_PerfisPersonal_AspNetUsers_Id",
                table: "PerfisPersonal");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosSerie_SessoesTreino_SessaoId",
                table: "RegistrosSerie");

            migrationBuilder.DropForeignKey(
                name: "FK_SessoesTreino_Treinos_TreinoId",
                table: "SessoesTreino");

            migrationBuilder.DropForeignKey(
                name: "FK_Treinos_TemplatesTreino_TemplateOrigemId",
                table: "Treinos");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InativadoEm",
                table: "Treinos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InativadoEm",
                table: "TemplatesTreino",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonimizadoEm",
                table: "PerfisPersonal",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InativadoEm",
                table: "PerfisPersonal",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonimizadoEm",
                table: "PerfisAluno",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InativadoEm",
                table: "PerfisAluno",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InativadoEm",
                table: "Exercicios",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnonimizadoEm",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PerfisAluno_AspNetUsers_Id",
                table: "PerfisAluno",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PerfisPersonal_AspNetUsers_Id",
                table: "PerfisPersonal",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosSerie_SessoesTreino_SessaoId",
                table: "RegistrosSerie",
                column: "SessaoId",
                principalTable: "SessoesTreino",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesTreino_Treinos_TreinoId",
                table: "SessoesTreino",
                column: "TreinoId",
                principalTable: "Treinos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Treinos_TemplatesTreino_TemplateOrigemId",
                table: "Treinos",
                column: "TemplateOrigemId",
                principalTable: "TemplatesTreino",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PerfisAluno_AspNetUsers_Id",
                table: "PerfisAluno");

            migrationBuilder.DropForeignKey(
                name: "FK_PerfisPersonal_AspNetUsers_Id",
                table: "PerfisPersonal");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosSerie_SessoesTreino_SessaoId",
                table: "RegistrosSerie");

            migrationBuilder.DropForeignKey(
                name: "FK_SessoesTreino_Treinos_TreinoId",
                table: "SessoesTreino");

            migrationBuilder.DropForeignKey(
                name: "FK_Treinos_TemplatesTreino_TemplateOrigemId",
                table: "Treinos");

            migrationBuilder.DropColumn(
                name: "InativadoEm",
                table: "Treinos");

            migrationBuilder.DropColumn(
                name: "InativadoEm",
                table: "TemplatesTreino");

            migrationBuilder.DropColumn(
                name: "AnonimizadoEm",
                table: "PerfisPersonal");

            migrationBuilder.DropColumn(
                name: "InativadoEm",
                table: "PerfisPersonal");

            migrationBuilder.DropColumn(
                name: "AnonimizadoEm",
                table: "PerfisAluno");

            migrationBuilder.DropColumn(
                name: "InativadoEm",
                table: "PerfisAluno");

            migrationBuilder.DropColumn(
                name: "InativadoEm",
                table: "Exercicios");

            migrationBuilder.DropColumn(
                name: "AnonimizadoEm",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_PerfisAluno_AspNetUsers_Id",
                table: "PerfisAluno",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PerfisPersonal_AspNetUsers_Id",
                table: "PerfisPersonal",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosSerie_SessoesTreino_SessaoId",
                table: "RegistrosSerie",
                column: "SessaoId",
                principalTable: "SessoesTreino",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SessoesTreino_Treinos_TreinoId",
                table: "SessoesTreino",
                column: "TreinoId",
                principalTable: "Treinos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Treinos_TemplatesTreino_TemplateOrigemId",
                table: "Treinos",
                column: "TemplateOrigemId",
                principalTable: "TemplatesTreino",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
