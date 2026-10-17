using Volumo.Domain.Comum;

namespace Volumo.Domain.Usuarios;

/// <summary>
/// Base dos perfis de personal e aluno. O Id é o mesmo do usuário (relação 1:1 com a conta).
/// Quando a pessoa pede a exclusão da conta (LGPD), o perfil é anonimizado: os dados pessoais
/// somem, mas o registro fica para o histórico de treinos continuar consistente.
/// </summary>
public abstract class Perfil : EntidadeInativavel
{
    public const int NomeMaximo = 120;
    public const string NomeAnonimizado = "Usuário removido";

    protected Perfil()
    {
    }

    protected Perfil(Guid usuarioId, string nome)
        : base(usuarioId) => Renomear(nome);

    public string Nome { get; private set; } = null!;

    public DateTimeOffset? AnonimizadoEm { get; private set; }

    public bool Anonimizado => AnonimizadoEm is not null;

    public void Renomear(string nome)
    {
        GarantirAtivo();
        Nome = Guarda.Texto(nome, "Nome", NomeMaximo);
    }

    /// <summary>Apaga os dados pessoais e inativa o perfil. Não tem volta. Repetir não muda nada.</summary>
    public void Anonimizar(DateTimeOffset agora)
    {
        if (Anonimizado)
        {
            return;
        }

        Nome = NomeAnonimizado;
        ApagarDadosPessoais();
        AnonimizadoEm = agora;
        Inativar(agora);
    }

    public override void Reativar()
    {
        if (Anonimizado)
        {
            throw new RegraDeNegocioException("Uma conta excluída não pode ser reativada.");
        }

        base.Reativar();
    }

    /// <summary>Dados pessoais próprios de cada tipo de perfil.</summary>
    protected virtual void ApagarDadosPessoais()
    {
    }
}
