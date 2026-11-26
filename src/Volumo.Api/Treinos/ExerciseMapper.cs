using Volumo.Api.Arquivos;
using Volumo.Domain.Exercicios;
using Volumo.Shared.Treinos;
using DomainEquipment = Volumo.Domain.Exercicios.Equipamento;
using Equipamento = Volumo.Shared.Treinos.Equipamento;

namespace Volumo.Api.Treinos;

/// <summary>Converte exercícios para os contratos, com as URLs de foto e vídeo geradas na hora.</summary>
public sealed class ExerciseMapper(MidiaDoExercicio midia)
{
    public ExercicioResumo ToSummary(Exercicio e) =>
        new(e.Id, e.Nome, PlanMapper.ToContract(e.GrupoMuscular), e.DoCatalogo, midia.Url(e.FotoChave));

    public ExercicioDetalhe ToDetail(Exercicio e) => new(
        e.Id,
        e.Nome,
        PlanMapper.ToContract(e.GrupoMuscular),
        e.DoCatalogo,
        [.. e.MusculosSecundarios.Select(PlanMapper.ToContract)],
        e.Equipamento is { } equipamento ? ToContract(equipamento) : null,
        e.Instrucoes,
        midia.Url(e.FotoChave),
        midia.Url(e.VideoChave),
        e.Ativo);

    public static DomainEquipment ToDomain(Equipamento equipamento) =>
        Enum.IsDefined(equipamento) ? Enum.Parse<DomainEquipment>(equipamento.ToString()) : (DomainEquipment)0;

    public static Equipamento ToContract(DomainEquipment equipamento) => Enum.Parse<Equipamento>(equipamento.ToString());
}
