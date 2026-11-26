using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Autenticacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TokensDeAtualizacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevogadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensDeAtualizacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensDeAtualizacao_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { new Guid("0199ecb0-0000-7000-8000-000000000001"), "0199ecb0-0000-7000-8000-000000000001", "Aluno", "ALUNO" },
                    { new Guid("0199ecb0-0000-7000-8000-000000000002"), "0199ecb0-0000-7000-8000-000000000002", "Personal", "PERSONAL" },
                    { new Guid("0199ecb0-0000-7000-8000-000000000003"), "0199ecb0-0000-7000-8000-000000000003", "Admin", "ADMIN" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensDeAtualizacao_FamiliaId",
                table: "TokensDeAtualizacao",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_TokensDeAtualizacao_TokenHash",
                table: "TokensDeAtualizacao",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensDeAtualizacao_UsuarioId",
                table: "TokensDeAtualizacao",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "TokensDeAtualizacao");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb0-0000-7000-8000-000000000001"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb0-0000-7000-8000-000000000002"));

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("0199ecb0-0000-7000-8000-000000000003"));
        }
    }
}
