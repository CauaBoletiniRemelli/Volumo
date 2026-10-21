using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;
using Volumo.Domain.Usuarios;
using Volumo.Domain.Vinculos;
using Volumo.Infrastructure.Identity;

namespace Volumo.Infrastructure.Persistencia;

/// <param name="acesso">Usuário da requisição para os filtros de dono; nulo = sem filtro de dono.</param>
public sealed class VolumoDbContext(DbContextOptions<VolumoDbContext> options, IContextoDeAcesso? acesso = null)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// Únicos tipos que podem ser apagados de verdade: credenciais auxiliares do Identity
    /// (removidas na anonimização da conta), refresh tokens e chaves do Data Protection (limpeza
    /// de registros vencidos). Tipos owned (itens e séries de treino) também podem, porque são
    /// partes de um treino sendo editado.
    /// </summary>
    private static readonly HashSet<Type> TiposQuePodemSerApagados =
    [
        typeof(IdentityUserLogin<Guid>),
        typeof(IdentityUserToken<Guid>),
        typeof(IdentityUserClaim<Guid>),
        typeof(IdentityUserRole<Guid>),
        typeof(IdentityRoleClaim<Guid>),
        typeof(TokenDeAtualizacao),
        typeof(DataProtectionKey),
    ];

    /// <summary>Chaves do ASP.NET Core Data Protection (tokens de confirmação de e-mail etc.).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<TokenDeAtualizacao> TokensDeAtualizacao => Set<TokenDeAtualizacao>();

    public DbSet<PerfilPersonal> PerfisPersonal => Set<PerfilPersonal>();

    public DbSet<PerfilAluno> PerfisAluno => Set<PerfilAluno>();

    public DbSet<VinculoPersonalAluno> VinculosPersonalAluno => Set<VinculoPersonalAluno>();

    public DbSet<ConviteAluno> ConvitesAluno => Set<ConviteAluno>();

    public DbSet<Exercicio> Exercicios => Set<Exercicio>();

    public DbSet<TemplateTreino> TemplatesTreino => Set<TemplateTreino>();

    public DbSet<Treino> Treinos => Set<Treino>();

    public DbSet<SessaoTreino> SessoesTreino => Set<SessaoTreino>();

    public DbSet<RegistroSerie> RegistrosSerie => Set<RegistroSerie>();

    public DbSet<RegistroCardio> RegistrosCardio => Set<RegistroCardio>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BloquearExclusaoFisica();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BloquearExclusaoFisica();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Lido a cada consulta pelos filtros de dono (o EF trata como parâmetro da instância).</summary>
    private Guid? UsuarioDoAcesso => acesso?.UsuarioId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(VolumoDbContext).Assembly);
        ConfigurarFiltrosDeDono(builder);
    }

    /// <summary>
    /// Proteção contra IDOR no banco: mesmo que uma consulta esqueça de filtrar pelo dono, os dados
    /// de outra conta não aparecem. Regras:
    /// personal vê os próprios templates, exercícios e convites e os dados dos alunos com vínculo ativo;
    /// aluno vê os próprios treinos e sessões, o perfil e os exercícios dos personais com quem teve vínculo.
    /// </summary>
    private void ConfigurarFiltrosDeDono(ModelBuilder builder)
    {
        builder.Entity<PerfilPersonal>().HasQueryFilter(FiltrosDeConsulta.Dono, p =>
            UsuarioDoAcesso == null
            || p.Id == UsuarioDoAcesso
            || VinculosPersonalAluno.Any(v => v.PersonalId == p.Id && v.AlunoId == UsuarioDoAcesso));

        builder.Entity<PerfilAluno>().HasQueryFilter(FiltrosDeConsulta.Dono, a =>
            UsuarioDoAcesso == null
            || a.Id == UsuarioDoAcesso
            || VinculosPersonalAluno.Any(v => v.AlunoId == a.Id && v.PersonalId == UsuarioDoAcesso && v.EncerradoEm == null));

        builder.Entity<VinculoPersonalAluno>().HasQueryFilter(FiltrosDeConsulta.Dono, v =>
            UsuarioDoAcesso == null || v.PersonalId == UsuarioDoAcesso || v.AlunoId == UsuarioDoAcesso);

        builder.Entity<ConviteAluno>().HasQueryFilter(FiltrosDeConsulta.Dono, c =>
            UsuarioDoAcesso == null || c.PersonalId == UsuarioDoAcesso || c.AlunoId == UsuarioDoAcesso);

        builder.Entity<Exercicio>().HasQueryFilter(FiltrosDeConsulta.Dono, e =>
            UsuarioDoAcesso == null
            || e.PersonalId == null
            || e.PersonalId == UsuarioDoAcesso
            || VinculosPersonalAluno.Any(v => v.AlunoId == UsuarioDoAcesso && v.PersonalId == e.PersonalId));

        builder.Entity<TemplateTreino>().HasQueryFilter(FiltrosDeConsulta.Dono, t =>
            UsuarioDoAcesso == null || t.PersonalId == UsuarioDoAcesso);

        builder.Entity<Treino>().HasQueryFilter(FiltrosDeConsulta.Dono, t =>
            UsuarioDoAcesso == null
            || t.AlunoId == UsuarioDoAcesso
            || VinculosPersonalAluno.Any(v => v.PersonalId == UsuarioDoAcesso && v.AlunoId == t.AlunoId && v.EncerradoEm == null));

        builder.Entity<SessaoTreino>().HasQueryFilter(FiltrosDeConsulta.Dono, s =>
            UsuarioDoAcesso == null
            || s.AlunoId == UsuarioDoAcesso
            || VinculosPersonalAluno.Any(v => v.PersonalId == UsuarioDoAcesso && v.AlunoId == s.AlunoId && v.EncerradoEm == null));

        builder.Entity<RegistroSerie>().HasQueryFilter(FiltrosDeConsulta.Dono, r =>
            UsuarioDoAcesso == null
            || SessoesTreino.Any(s => s.Id == r.SessaoId
                && (s.AlunoId == UsuarioDoAcesso
                    || VinculosPersonalAluno.Any(v => v.PersonalId == UsuarioDoAcesso && v.AlunoId == s.AlunoId && v.EncerradoEm == null))));

        builder.Entity<RegistroCardio>().HasQueryFilter(FiltrosDeConsulta.Dono, c =>
            UsuarioDoAcesso == null
            || SessoesTreino.Any(s => s.Id == c.SessaoId
                && (s.AlunoId == UsuarioDoAcesso
                    || VinculosPersonalAluno.Any(v => v.PersonalId == UsuarioDoAcesso && v.AlunoId == s.AlunoId && v.EncerradoEm == null))));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums como texto: legível no banco e não quebra se a ordem dos membros mudar.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<decimal>().HavePrecision(6, 2);
    }

    private static bool PodeSerApagado(IEntityType tipo) => tipo.IsOwned() || TiposQuePodemSerApagados.Contains(tipo.ClrType);

    /// <summary>
    /// Regra do projeto: nada importante é apagado, só inativado (ou anonimizado, na exclusão de conta).
    /// Isto pega um <c>Remove()</c> esquecido antes de chegar ao banco. Não cobre <c>ExecuteDelete</c>,
    /// que não deve ser usado nessas tabelas.
    /// </summary>
    private void BloquearExclusaoFisica()
    {
        var proibidos = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && !PodeSerApagado(e.Metadata))
            .Select(e => e.Metadata.ClrType.Name)
            .Distinct()
            .ToList();

        if (proibidos.Count > 0)
        {
            throw new InvalidOperationException(
                $"Exclusão física não é permitida ({string.Join(", ", proibidos)}). Use Inativar() ou a anonimização da conta.");
        }
    }
}
