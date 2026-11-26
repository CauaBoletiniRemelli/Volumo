using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

/// <summary>Papéis fixos, criados pela migration (Ids e carimbos constantes para a migration não mudar).</summary>
internal sealed class PapeisConfiguracao : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public static readonly Guid AlunoId = Guid.Parse("0199ecb0-0000-7000-8000-000000000001");
    public static readonly Guid PersonalId = Guid.Parse("0199ecb0-0000-7000-8000-000000000002");
    public static readonly Guid AdminId = Guid.Parse("0199ecb0-0000-7000-8000-000000000003");

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) =>
        builder.HasData(
            Papel(AlunoId, Papeis.Aluno),
            Papel(PersonalId, Papeis.Personal),
            Papel(AdminId, Papeis.Admin));

    private static IdentityRole<Guid> Papel(Guid id, string nome) => new()
    {
        Id = id,
        Name = nome,
        NormalizedName = nome.ToUpperInvariant(),
        ConcurrencyStamp = id.ToString(),
    };
}

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder) =>
        builder.Property(u => u.MotivoDaSuspensao).HasMaxLength(Usuario.MotivoDaSuspensaoMaximo);
}

internal sealed class TokenDeAtualizacaoConfiguracao : IEntityTypeConfiguration<TokenDeAtualizacao>
{
    public void Configure(EntityTypeBuilder<TokenDeAtualizacao> builder)
    {
        builder.ToTable("TokensDeAtualizacao");
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(TokenDeAtualizacao.TamanhoHashBytes).IsFixedLength();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamiliaId);
        builder.HasIndex(t => t.UsuarioId);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
