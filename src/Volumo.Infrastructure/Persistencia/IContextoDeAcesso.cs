namespace Volumo.Infrastructure.Persistencia;

/// <summary>
/// Quem está fazendo a requisição, para os filtros de dono do EF. A API implementa a partir do
/// token; fora de uma requisição autenticada (rotas anônimas, tarefas do sistema, testes de
/// persistência) não há usuário e os filtros de dono não se aplicam.
/// </summary>
public interface IContextoDeAcesso
{
    Guid? UsuarioId { get; }
}
