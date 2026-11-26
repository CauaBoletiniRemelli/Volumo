namespace Volumo.Domain.Usuarios;

/// <summary>Dados de quem treina.</summary>
public sealed class PerfilAluno : Perfil
{
    private PerfilAluno()
    {
    }

    private PerfilAluno(Guid usuarioId, string nome)
        : base(usuarioId, nome)
    {
    }

    public static PerfilAluno Criar(Guid usuarioId, string nome) => new(usuarioId, nome);
}
