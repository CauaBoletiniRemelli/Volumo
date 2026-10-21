using System.ComponentModel.DataAnnotations;
using Volumo.Shared.Treinos;

namespace Volumo.Shared.Admin;

public sealed record Suspensao([Required, StringLength(500)] string Motivo);

/// <summary>Exercício do catálogo como a TI cria e edita. Foto e vídeo são enviados à parte.</summary>
public sealed record DadosDoExercicioDoCatalogo(
    [Required, StringLength(100)] string Nome,
    [Required] GrupoMuscular GrupoMuscular,
    [Required, MaxLength(5)] IReadOnlyList<GrupoMuscular> MusculosSecundarios,
    Equipamento? Equipamento,
    [Required, MaxLength(10)] IReadOnlyList<string> Instrucoes);

/// <summary>Linha da lista do catálogo no painel da TI, inclusive exercícios inativos.</summary>
public sealed record ExercicioDoCatalogoResumo(
    Guid Id, string Nome, GrupoMuscular GrupoMuscular, Equipamento? Equipamento, bool TemFoto, bool TemVideo, bool Ativo);
