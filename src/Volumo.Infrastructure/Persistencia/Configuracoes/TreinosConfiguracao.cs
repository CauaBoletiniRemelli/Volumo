using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volumo.Domain.Exercicios;
using Volumo.Domain.Treinos;
using Volumo.Domain.Usuarios;
using Volumo.Infrastructure.Identity;

namespace Volumo.Infrastructure.Persistencia.Configuracoes;

internal sealed class TemplateTreinoConfiguracao : IEntityTypeConfiguration<TemplateTreino>
{
    public void Configure(EntityTypeBuilder<TemplateTreino> builder)
    {
        builder.ToTable("TemplatesTreino");
        builder.HasOne<PerfilPersonal>().WithMany().HasForeignKey(t => t.PersonalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => t.PersonalId);
        PlanoDeTreinoMapeamento.Configurar(builder, "ItensTemplateTreino", "SeriesTemplateTreino", "CardiosTemplateTreino");
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

        // Combinação de dias (flags): número, não texto como os outros enums ("Segunda, Quinta" não cabe na convenção).
        builder.Property(t => t.Dias).HasConversion<int>();
        PlanoDeTreinoMapeamento.Configurar(builder, "ItensTreino", "SeriesTreino", "CardiosTreino");
    }
}

/// <summary>Presets próprios (os prontos ficam no código). O dono é a conta, de personal ou de aluno.</summary>
internal sealed class PresetDeSeriesConfiguracao : IEntityTypeConfiguration<PresetDeSeries>
{
    public void Configure(EntityTypeBuilder<PresetDeSeries> builder)
    {
        builder.ToTable("PresetsDeSeries");
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nome).HasMaxLength(PresetDeSeries.NomeMaximo);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(p => p.DonoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.DonoId);
        builder.Ignore(p => p.Pronto);
        builder.HasQueryFilter(FiltrosDeConsulta.Ativos, p => p.InativadoEm == null);

        builder.OwnsMany(p => p.Series, serie =>
        {
            serie.ToTable("SeriesPresetDeSeries");
            serie.Property<Guid>("PresetId");
            serie.WithOwner().HasForeignKey("PresetId");
            serie.Property<int>("Id");
            serie.HasKey("Id");
        });
        builder.Navigation(p => p.Series).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Uma anotação por treino e exercício (o app grava por cima).</summary>
internal sealed class AnotacaoDoAlunoConfiguracao : IEntityTypeConfiguration<AnotacaoDoAluno>
{
    public void Configure(EntityTypeBuilder<AnotacaoDoAluno> builder)
    {
        builder.ToTable("AnotacoesDoAluno");
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Texto).HasMaxLength(AnotacaoDoAluno.TextoMaximo);
        builder.HasOne<PerfilAluno>().WithMany().HasForeignKey(a => a.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Treino>().WithMany().HasForeignKey(a => a.TreinoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercicio>().WithMany().HasForeignKey(a => a.ExercicioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.TreinoId, a.ExercicioId }).IsUnique();
        builder.HasIndex(a => a.AlunoId);
    }
}

/// <summary>
/// Itens e séries são tipos "owned": pertencem ao treino ou ao template e não existem sozinhos.
/// Cada dono tem as próprias tabelas, então template e treino não compartilham linhas.
/// </summary>
internal static class PlanoDeTreinoMapeamento
{
    public static void Configurar<TPlano>(EntityTypeBuilder<TPlano> builder, string tabelaItens, string tabelaSeries, string tabelaCardios)
        where TPlano : PlanoDeTreino
    {
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Nome).HasMaxLength(PlanoDeTreino.NomeMaximo);

        // Treinos e modelos que já existiam ficam com "depois" (o padrão do domínio).
        builder.Property(p => p.MomentoDoCardio).HasDefaultValue(MomentoDoCardio.Depois).HasSentinel((MomentoDoCardio)0);

        builder.OwnsMany(p => p.Cardios, cardio =>
        {
            cardio.ToTable(tabelaCardios);
            cardio.WithOwner().HasForeignKey("PlanoId");
            cardio.HasKey(c => c.Id);
            cardio.Property(c => c.Id).ValueGeneratedNever();
            cardio.Property(c => c.Observacao).HasMaxLength(ItemCardio.ObservacaoMaxima);
            cardio.HasOne<Exercicio>().WithMany().HasForeignKey(c => c.ExercicioId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(p => p.Cardios).UsePropertyAccessMode(PropertyAccessMode.Field);
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
                serie.Property<Guid>("ItemId");
                serie.WithOwner().HasForeignKey("ItemId");
                serie.Property<int>("Id");
                serie.HasKey("Id");
            });
            item.Navigation(i => i.Series).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        builder.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
