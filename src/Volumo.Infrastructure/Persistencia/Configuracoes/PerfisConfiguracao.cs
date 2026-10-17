using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class PerfilPersonalConfiguracao : IEntityTypeConfiguration<PerfilPersonal>
{
    public void Configure(EntityTypeBuilder<PerfilPersonal> builder)
    {
        builder.ToTable("PerfisPersonal");
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.HasOne<Usuario>().WithOne().HasForeignKey<PerfilPersonal>(p => p.Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(FiltrosDeConsulta.Ativos, p => p.InativadoEm == null);

        builder.Property(p => p.Nome).HasMaxLength(Perfil.NomeMaximo);
        builder.Property(p => p.Cref)
            .HasConversion(cref => cref!.Valor, valor => Cref.Criar(valor))
            .HasMaxLength(Cref.TamanhoMaximo)
            .IsUnicode(false);

        // Um CREF por conta, inclusive inativas (quem volta reativa a conta antiga): dificulta alguém
        // se passar por outro profissional. A anonimização apaga o CREF e o libera.
        builder.HasIndex(p => p.Cref).IsUnique().HasFilter("[Cref] IS NOT NULL");
    }
}

internal sealed class PerfilAlunoConfiguracao : IEntityTypeConfiguration<PerfilAluno>
{
    public void Configure(EntityTypeBuilder<PerfilAluno> builder)
    {
        builder.ToTable("PerfisAluno");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasOne<Usuario>().WithOne().HasForeignKey<PerfilAluno>(p => p.Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasQueryFilter(FiltrosDeConsulta.Ativos, p => p.InativadoEm == null);
        builder.Property(p => p.Nome).HasMaxLength(Perfil.NomeMaximo);
    }
}
