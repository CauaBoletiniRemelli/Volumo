using Volumo.Domain.Comum;
using Volumo.Domain.Usuarios;

namespace Volumo.Domain.Vinculos;

/// <summary>
/// Aceite de convite considerando o vínculo atual do aluno (cada aluno tem um personal só).
/// Se o personal atual estiver inativo, o vínculo antigo é encerrado automaticamente, para o
/// aluno não ficar preso a quem não atende mais. Com personal atual ativo, o aluno precisa
/// encerrar o vínculo antes.
/// </summary>
public static class AceiteDeConvite
{
    /// <param name="vinculoAtivo">Vínculo ativo do aluno, se houver.</param>
    /// <param name="personalAtual">Personal do <paramref name="vinculoAtivo"/>; obrigatório quando há vínculo ativo.</param>
    public static VinculoPersonalAluno Aceitar(
        ConviteAluno convite,
        string token,
        PerfilAluno aluno,
        VinculoPersonalAluno? vinculoAtivo,
        PerfilPersonal? personalAtual,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(convite);
        ArgumentNullException.ThrowIfNull(aluno);

        if (!aluno.Ativo)
        {
            throw new RegraDeNegocioException("Reative sua conta antes de aceitar um convite.");
        }

        var encerrarVinculoAtual = false;
        if (vinculoAtivo is { Ativo: true })
        {
            if (vinculoAtivo.AlunoId != aluno.Id)
            {
                throw new ArgumentException("O vínculo informado não é deste aluno.", nameof(vinculoAtivo));
            }

            if (personalAtual is null || personalAtual.Id != vinculoAtivo.PersonalId)
            {
                throw new ArgumentException("Informe o personal do vínculo atual.", nameof(personalAtual));
            }

            if (vinculoAtivo.PersonalId == convite.PersonalId)
            {
                throw new RegraDeNegocioException("Você já treina com este personal.");
            }

            if (personalAtual.Ativo)
            {
                throw new RegraDeNegocioException("Você já tem um personal. Encerre o vínculo atual antes de aceitar outro convite.");
            }

            encerrarVinculoAtual = true;
        }

        // O token é validado antes de mexer no vínculo atual: convite inválido não altera nada.
        var novoVinculo = convite.Aceitar(token, aluno.Id, agora);
        if (encerrarVinculoAtual)
        {
            vinculoAtivo!.Encerrar(agora);
        }

        return novoVinculo;
    }
}
