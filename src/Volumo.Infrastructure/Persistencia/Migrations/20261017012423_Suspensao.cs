using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Suspensao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoDaSuspensao",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SuspensoEm",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoDaSuspensao",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SuspensoEm",
                table: "AspNetUsers");
        }
    }
}
