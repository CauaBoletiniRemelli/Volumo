namespace Volumo.Domain.Comum;

/// <summary>
/// Entidade que nunca é apagada do banco, só inativada, para preservar o histórico.
/// Registros inativos somem das consultas comuns (filtro global do EF) e não podem ser alterados.
/// </summary>
public abstract class EntidadeInativavel : Entidade
{
    protected EntidadeInativavel()
    {
    }

    protected EntidadeInativavel(Guid id)
        : base(id)
    {
    }

    public DateTimeOffset? InativadoEm { get; private set; }

    public bool Ativo => InativadoEm is null;

    /// <summary>Inativa o registro. Inativar de novo mantém a data original.</summary>
    public void Inativar(DateTimeOffset agora) => InativadoEm ??= agora;

    public virtual void Reativar() => InativadoEm = null;

    protected void GarantirAtivo()
    {
        if (!Ativo)
        {
            throw new RegraDeNegocioException("Este registro está inativo e não pode ser alterado. Reative-o primeiro.");
        }
    }
}
