using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Usuarios;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class ExercicioConfiguracao : IEntityTypeConfiguration<Exercicio>
{
    public void Configure(EntityTypeBuilder<Exercicio> builder)
    {
        builder.ToTable("Exercicios");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Nome).HasMaxLength(Exercicio.NomeMaximo);
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(e => e.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.PersonalId);
        builder.HasQueryFilter(FiltrosDeConsulta.Ativos, e => e.InativadoEm == null);
    }
}
