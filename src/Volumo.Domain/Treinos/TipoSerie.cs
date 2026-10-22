namespace Volumo.Domain.Treinos;

/// <summary>Gravado como texto no banco; não renomear membros existentes.</summary>
public enum TipoSerie
{
    Aquecimento = 1,
    Trabalho = 2,

    /// <summary>Série de aproximação: carga perto da de trabalho, poucas repetições, para preparar a primeira série de trabalho.</summary>
    Preparatoria = 3,
}
