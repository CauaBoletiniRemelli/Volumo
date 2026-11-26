namespace Volumo.Api.Auth;

/// <summary>Políticas para usar com [Authorize(Policy = ...)].</summary>
public static class Policies
{
    /// <summary>Personal que entrou com 2FA. O 2FA é obrigatório para personal (dados de alunos e cobrança).</summary>
    public const string Personal = "Personal";

    public const string Aluno = "Aluno";

    /// <summary>
    /// Quem monta treinos (personal ou aluno), para recursos da própria conta, como os presets de séries.
    /// Não exige 2FA: não dá acesso a dados de alunos (esses continuam atrás de <see cref="Personal"/>).
    /// </summary>
    public const string PersonalOuAluno = "PersonalOuAluno";

    /// <summary>Qualquer conta logada e ativa (aluno ou personal), para recursos dos dois lados, como ver um treino.</summary>
    public const string ContaAtiva = "ContaAtiva";

    /// <summary>Administrador da plataforma, sempre com 2FA.</summary>
    public const string Admin = "Admin";

    /// <summary>Qualquer conta que entrou com 2FA (ex.: gerar novos códigos de recuperação).</summary>
    public const string MultiFactor = "MultiFactor";
}
