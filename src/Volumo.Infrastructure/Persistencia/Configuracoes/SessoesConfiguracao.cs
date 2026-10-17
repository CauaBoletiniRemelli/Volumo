using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Sessoes;
using Volumo.Domain.Treinos;
using Volumo.Domain.Usuarios;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class SessaoTreinoConfiguracao : IEntityTypeConfiguration<SessaoTreino>
{
    public void Configure(EntityTypeBuilder<SessaoTreino> builder)
    {
        builder.ToTable("SessoesTreino");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.HasOne<PerfilAluno>().WithMany().HasForeignKey(s => s.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Treino>().WithMany().HasForeignKey(s => s.TreinoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Registros).WithOne().HasForeignKey(r => r.SessaoId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(s => s.Registros).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Painel do personal: último treino de cada aluno.
        builder.HasIndex(s => new { s.AlunoId, s.IniciadaEm });
    }
}

internal sealed class RegistroSerieConfiguracao : IEntityTypeConfiguration<RegistroSerie>
{
    public void Configure(EntityTypeBuilder<RegistroSerie> builder)
    {
        builder.ToTable("RegistrosSerie");

        // Id vem do app (offline). Sem isto o EF acharia que um registro novo já existe no banco.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasOne<Exercicio>().WithMany().HasForeignKey(r => r.ExercicioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.ExercicioId);
    }
}
