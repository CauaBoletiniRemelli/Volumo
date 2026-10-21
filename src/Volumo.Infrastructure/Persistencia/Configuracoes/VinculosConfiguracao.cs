using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Usuarios;
using Volumo.Domain.Vinculos;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class VinculoPersonalAlunoConfiguracao : IEntityTypeConfiguration<VinculoPersonalAluno>
{
    public const string IndiceAlunoComVinculoAtivo = "UX_VinculosPersonalAluno_AlunoAtivo";

    public void Configure(EntityTypeBuilder<VinculoPersonalAluno> builder)
    {
        builder.ToTable("VinculosPersonalAluno");
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(v => v.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PerfilAluno>().WithMany().HasForeignKey(v => v.AlunoId).OnDelete(DeleteBehavior.Restrict);

        // Regra "um aluno tem só um personal": no máximo um vínculo ativo por aluno.
        builder.HasIndex(v => v.AlunoId)
            .IsUnique()
            .HasFilter("[EncerradoEm] IS NULL")
            .HasDatabaseName(IndiceAlunoComVinculoAtivo);

        builder.HasIndex(v => new { v.PersonalId, v.EncerradoEm });

        // Vínculos que já existiam ganham o padrão: 15 dias sem subir peso.
        builder.Property(v => v.CriterioDeEstagnacao).HasDefaultValue(AnaliseDeEstagnacao.CriterioPadrao);
        builder.Property(v => v.LimiteDeEstagnacao).HasDefaultValue(AnaliseDeEstagnacao.LimitePadrao);
    }
}

internal sealed class ConviteAlunoConfiguracao : IEntityTypeConfiguration<ConviteAluno>
{
    public void Configure(EntityTypeBuilder<ConviteAluno> builder)
    {
        builder.ToTable("ConvitesAluno");
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(c => c.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PerfilAluno>().WithMany().HasForeignKey(c => c.AlunoId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Email).HasMaxLength(256);
        builder.Property(c => c.TokenHash).HasMaxLength(ConviteAluno.TamanhoHashBytes).IsFixedLength();
        builder.HasIndex(c => c.TokenHash).IsUnique();
        builder.HasIndex(c => new { c.PersonalId, c.Email });
    }
}
