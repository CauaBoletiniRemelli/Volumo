namespace Volumo.Domain.Comum;

/// <summary>
/// Violação de uma regra do domínio. A mensagem é escrita para o usuário final e
/// não traz detalhes internos, então a API pode devolvê-la como está.
/// </summary>
public sealed class RegraDeNegocioException : Exception
{
    public RegraDeNegocioException()
    {
    }

    public RegraDeNegocioException(string message)
        : base(message)
    {
    }

    public RegraDeNegocioException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
