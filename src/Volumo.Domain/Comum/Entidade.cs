namespace Volumo.Domain.Comum;

/// <summary>
/// Base das entidades. O Id é um GUID v7 gerado no cliente, para o app registrar
/// treinos offline e sincronizar depois sem conflito de chave.
/// </summary>
public abstract class Entidade
{
    /// <summary>Usado pelo EF Core ao materializar do banco.</summary>
    protected Entidade()
    {
    }

    protected Entidade(Guid id) => Id = Guarda.IdNovo(id, nameof(Id));

    public Guid Id { get; private init; }
}
