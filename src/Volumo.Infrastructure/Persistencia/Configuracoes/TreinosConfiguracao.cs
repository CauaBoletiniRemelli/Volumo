using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Treinos;
using Volumo.Domain.Usuarios;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class TemplateTreinoConfiguracao : IEntityTypeConfiguration<TemplateTreino>
{
    public void Configure(EntityTypeBuilder<TemplateTreino> builder)
    {
        builder.ToTable("TemplatesTreino");
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(t => t.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.PersonalId);
        PlanoDeTreinoMapeamento.Configurar(builder, "ItensTemplateTreino", "SeriesTemplateTreino");
    }
}

internal sealed class TreinoConfiguracao : IEntityTypeConfiguration<Treino>
{
    public void Configure(EntityTypeBuilder<Treino> builder)
    {
        builder.ToTable("Treinos");
        builder.HasOne<PerfilAluno>().WithMany().HasForeignKey(t => t.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(t => t.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TemplateTreino>().WithMany().HasForeignKey(t => t.TemplateOrigemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.AlunoId);
        PlanoDeTreinoMapeamento.Configurar(builder, "ItensTreino", "SeriesTreino");
    }
}

/// <summary>
/// Itens e séries são tipos "owned": pertencem ao treino ou ao template e não existem sozinhos.
/// Cada dono tem as próprias tabelas, então template e treino não compartilham linhas.
/// </summary>
internal static class PlanoDeTreinoMapeamento
{
    public static void Configurar<TPlano>(EntityTypeBuilder<TPlano> builder, string tabelaItens, string tabelaSeries)
        where TPlano : PlanoDeTreino
    {
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nome).HasMaxLength(PlanoDeTreino.NomeMaximo);
        builder.HasQueryFilter(FiltrosDeConsulta.Ativos, p => p.InativadoEm == null);

        builder.OwnsMany(p => p.Itens, item =>
        {
            item.ToTable(tabelaItens);
            item.WithOwner().HasForeignKey("PlanoId");
            item.HasKey(i => i.Id);
            item.Property(i => i.Id).ValueGeneratedNever();
            item.Property(i => i.Observacao).HasMaxLength(ItemTreino.ObservacaoMaxima);
            item.HasOne<Exercicio>().WithMany().HasForeignKey(i => i.ExercicioId).OnDelete(DeleteBehavior.Restrict);

            item.OwnsMany(i => i.Series, serie =>
            {
                serie.ToTable(tabelaSeries);
                serie.WithOwner().HasForeignKey("ItemId");
                serie.Property<int>("Id");
                serie.HasKey("Id");
            });
            item.Navigation(i => i.Series).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
