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

        // Detalhes do catálogo (21/10/2026). Listas guardadas como texto nos campos privados.
        builder.Ignore(e => e.MusculosSecundarios);
        builder.Ignore(e => e.Instrucoes);
        builder.Property<string?>("_secundarios").HasColumnName("MusculosSecundarios").HasMaxLength(200);
        builder.Property<string?>("_instrucoes").HasColumnName("Instrucoes")
            .HasMaxLength((Exercicio.MaximoPassos * (Exercicio.PassoMaximo + 1)) - 1);
        builder.Property(e => e.FotoChave).HasMaxLength(Exercicio.ChaveDeArquivoMaxima);
        builder.Property(e => e.VideoChave).HasMaxLength(Exercicio.ChaveDeArquivoMaxima);
        builder.HasData(CatalogoInicial.Exercicios.Select((e, i) => new
        {
            Id = CatalogoInicial.Id(i + 1),
            e.Nome,
            e.GrupoMuscular,
            PersonalId = (Guid?)null,
            InativadoEm = (DateTimeOffset?)null,
        }));
    }
}

/// <summary>
/// Exercícios básicos do catálogo da plataforma, criados pela migration. Ids fixos: não reordenar
/// nem remover itens (só acrescentar no fim); para tirar um exercício, inativar por migration.
/// </summary>
internal static class CatalogoInicial
{
    public static readonly (string Nome, GrupoMuscular GrupoMuscular)[] Exercicios =
    [
        ("Supino reto com barra", GrupoMuscular.Peitoral),
        ("Supino inclinado com halteres", GrupoMuscular.Peitoral),
        ("Crucifixo com halteres", GrupoMuscular.Peitoral),
        ("Crossover na polia", GrupoMuscular.Peitoral),
        ("Flexão de braço", GrupoMuscular.Peitoral),
        ("Puxada frontal na polia", GrupoMuscular.Costas),
        ("Remada curvada com barra", GrupoMuscular.Costas),
        ("Remada baixa na polia", GrupoMuscular.Costas),
        ("Remada unilateral com halter", GrupoMuscular.Costas),
        ("Barra fixa", GrupoMuscular.Costas),
        ("Desenvolvimento com halteres", GrupoMuscular.Ombros),
        ("Elevação lateral com halteres", GrupoMuscular.Ombros),
        ("Elevação frontal com halteres", GrupoMuscular.Ombros),
        ("Crucifixo invertido", GrupoMuscular.Ombros),
        ("Encolhimento com halteres", GrupoMuscular.Trapezio),
        ("Rosca direta com barra", GrupoMuscular.Biceps),
        ("Rosca alternada com halteres", GrupoMuscular.Biceps),
        ("Rosca martelo", GrupoMuscular.Biceps),
        ("Tríceps na polia com corda", GrupoMuscular.Triceps),
        ("Tríceps testa", GrupoMuscular.Triceps),
        ("Mergulho no banco", GrupoMuscular.Triceps),
        ("Rosca de punho", GrupoMuscular.Antebraco),
        ("Agachamento livre", GrupoMuscular.Quadriceps),
        ("Leg press 45°", GrupoMuscular.Quadriceps),
        ("Cadeira extensora", GrupoMuscular.Quadriceps),
        ("Afundo com halteres", GrupoMuscular.Quadriceps),
        ("Stiff com barra", GrupoMuscular.Posteriores),
        ("Mesa flexora", GrupoMuscular.Posteriores),
        ("Cadeira flexora", GrupoMuscular.Posteriores),
        ("Elevação pélvica", GrupoMuscular.Gluteos),
        ("Glúteo na polia", GrupoMuscular.Gluteos),
        ("Cadeira adutora", GrupoMuscular.Adutores),
        ("Cadeira abdutora", GrupoMuscular.Abdutores),
        ("Panturrilha em pé", GrupoMuscular.Panturrilhas),
        ("Panturrilha sentado", GrupoMuscular.Panturrilhas),
        ("Prancha", GrupoMuscular.Abdomen),
        ("Abdominal supra", GrupoMuscular.Abdomen),
        ("Elevação de pernas", GrupoMuscular.Abdomen),
        ("Hiperextensão lombar", GrupoMuscular.Lombar),
        ("Levantamento terra", GrupoMuscular.CorpoInteiro),
        ("Esteira", GrupoMuscular.Cardio),
        ("Bicicleta ergométrica", GrupoMuscular.Cardio),

        // Cardio (21/10/2026): para a aba de cardio do construtor.
        ("Elíptico", GrupoMuscular.Cardio),
        ("Remo ergômetro", GrupoMuscular.Cardio),
        ("Escada ergométrica", GrupoMuscular.Cardio),
        ("Bicicleta de spinning", GrupoMuscular.Cardio),
        ("Pular corda", GrupoMuscular.Cardio),
        ("Caminhada ao ar livre", GrupoMuscular.Cardio),
        ("Corrida ao ar livre", GrupoMuscular.Cardio),
    ];

    /// <summary>Id fixo no formato GUID v7 para o exercício na posição <paramref name="numero"/> (a partir de 1).</summary>
    public static Guid Id(int numero) => Guid.Parse($"0199ecb1-0000-7000-8000-{numero:x12}");
}
