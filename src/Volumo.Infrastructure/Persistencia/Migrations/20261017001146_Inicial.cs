using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volumo.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerfisAluno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfisAluno_AspNetUsers_Id",
                        column: x => x.Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerfisPersonal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Cref = table.Column<string>(type: "varchar(11)", unicode: false, maxLength: 11, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerfisPersonal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerfisPersonal_AspNetUsers_Id",
                        column: x => x.Id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConvitesAluno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiraEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AceitoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevogadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvitesAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvitesAluno_PerfisAluno_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "PerfisAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConvitesAluno_PerfisPersonal_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "PerfisPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Exercicios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GrupoMuscular = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PersonalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercicios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercicios_PerfisPersonal_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "PerfisPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplatesTreino_PerfisPersonal_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "PerfisPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VinculosPersonalAluno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IniciadoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EncerradoEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VinculosPersonalAluno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VinculosPersonalAluno_PerfisAluno_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "PerfisAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VinculosPersonalAluno_PerfisPersonal_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "PerfisPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensTemplateTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensTemplateTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensTemplateTreino_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensTemplateTreino_TemplatesTreino_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "TemplatesTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Treinos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TemplateOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Treinos_PerfisAluno_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "PerfisAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Treinos_PerfisPersonal_PersonalId",
                        column: x => x.PersonalId,
                        principalTable: "PerfisPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Treinos_TemplatesTreino_TemplateOrigemId",
                        column: x => x.TemplateOrigemId,
                        principalTable: "TemplatesTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SeriesTemplateTreino",
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
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesTemplateTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesTemplateTreino_ItensTemplateTreino_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ItensTemplateTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItensTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlanoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensTreino_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensTreino_Treinos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Treinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessoesTreino",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlunoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TreinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IniciadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinalizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessoesTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessoesTreino_PerfisAluno_AlunoId",
                        column: x => x.AlunoId,
                        principalTable: "PerfisAluno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessoesTreino_Treinos_TreinoId",
                        column: x => x.TreinoId,
                        principalTable: "Treinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SeriesTreino",
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
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesTreino", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeriesTreino_ItensTreino_ItemId",
                        column: x => x.ItemId,
                        principalTable: "ItensTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosSerie",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExercicioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Repeticoes = table.Column<int>(type: "int", nullable: false),
                    CargaKg = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    Rir = table.Column<int>(type: "int", nullable: true),
                    RealizadaEm = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosSerie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosSerie_Exercicios_ExercicioId",
                        column: x => x.ExercicioId,
                        principalTable: "Exercicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosSerie_SessoesTreino_SessaoId",
                        column: x => x.SessaoId,
                        principalTable: "SessoesTreino",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAluno_AlunoId",
                table: "ConvitesAluno",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAluno_PersonalId_Email",
                table: "ConvitesAluno",
                columns: new[] { "PersonalId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesAluno_TokenHash",
                table: "ConvitesAluno",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exercicios_PersonalId",
                table: "Exercicios",
                column: "PersonalId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensTemplateTreino_ExercicioId",
                table: "ItensTemplateTreino",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensTemplateTreino_PlanoId",
                table: "ItensTemplateTreino",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensTreino_ExercicioId",
                table: "ItensTreino",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensTreino_PlanoId",
                table: "ItensTreino",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_PerfisPersonal_Cref",
                table: "PerfisPersonal",
                column: "Cref",
                unique: true,
                filter: "[Cref] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosSerie_ExercicioId",
                table: "RegistrosSerie",
                column: "ExercicioId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosSerie_SessaoId",
                table: "RegistrosSerie",
                column: "SessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesTemplateTreino_ItemId",
                table: "SeriesTemplateTreino",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesTreino_ItemId",
                table: "SeriesTreino",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreino_AlunoId_IniciadaEm",
                table: "SessoesTreino",
                columns: new[] { "AlunoId", "IniciadaEm" });

            migrationBuilder.CreateIndex(
                name: "IX_SessoesTreino_TreinoId",
                table: "SessoesTreino",
                column: "TreinoId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplatesTreino_PersonalId",
                table: "TemplatesTreino",
                column: "PersonalId");

            migrationBuilder.CreateIndex(
                name: "IX_Treinos_AlunoId",
                table: "Treinos",
                column: "AlunoId");

            migrationBuilder.CreateIndex(
                name: "IX_Treinos_PersonalId",
                table: "Treinos",
                column: "PersonalId");

            migrationBuilder.CreateIndex(
                name: "IX_Treinos_TemplateOrigemId",
                table: "Treinos",
                column: "TemplateOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_VinculosPersonalAluno_PersonalId_EncerradoEm",
                table: "VinculosPersonalAluno",
                columns: new[] { "PersonalId", "EncerradoEm" });

            migrationBuilder.CreateIndex(
                name: "UX_VinculosPersonalAluno_AlunoAtivo",
                table: "VinculosPersonalAluno",
                column: "AlunoId",
                unique: true,
                filter: "[EncerradoEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ConvitesAluno");

            migrationBuilder.DropTable(
                name: "RegistrosSerie");

            migrationBuilder.DropTable(
                name: "SeriesTemplateTreino");

            migrationBuilder.DropTable(
                name: "SeriesTreino");

            migrationBuilder.DropTable(
                name: "VinculosPersonalAluno");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "SessoesTreino");

            migrationBuilder.DropTable(
                name: "ItensTemplateTreino");

            migrationBuilder.DropTable(
                name: "ItensTreino");

            migrationBuilder.DropTable(
                name: "Exercicios");

            migrationBuilder.DropTable(
                name: "Treinos");

            migrationBuilder.DropTable(
                name: "PerfisAluno");

            migrationBuilder.DropTable(
                name: "TemplatesTreino");

            migrationBuilder.DropTable(
                name: "PerfisPersonal");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
