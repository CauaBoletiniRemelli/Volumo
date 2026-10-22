using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Volumo.Shared.Autenticacao;

// Contratos da API de autenticação, compartilhados com o front (Blazor).
// O tamanho mínimo da senha é validado pelo Identity no servidor (10 caracteres).

/// <summary>Formulários protegidos por verificação anti-robô (Cloudflare Turnstile).</summary>
public interface IComVerificacaoHumana
{
    /// <summary>Token gerado pelo widget do Turnstile no navegador.</summary>
    string TokenTurnstile { get; }
}

public sealed record CadastroDePersonal(
    [Required, StringLength(120)] string Nome,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Senha,
    [StringLength(20)] string? Cref,
    [Required, StringLength(2048)] string TokenTurnstile) : IComVerificacaoHumana;

public sealed record ConfirmacaoDeEmail(
    [Required] Guid UsuarioId,
    [Required, StringLength(2048)] string Token);

public sealed record PedidoDeReenvioDeConfirmacao(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(2048)] string TokenTurnstile) : IComVerificacaoHumana;

public sealed record PedidoDeLogin(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Senha);

/// <summary>
/// Resposta do login. Com 2FA ativo, a primeira etapa devolve só o <see cref="Desafio2Fa"/>, que deve
/// ir para <c>/auth/login/2fa</c> com o código do aplicativo autenticador. O refresh token nunca vem
/// aqui: vai num cookie HttpOnly, inacessível ao JavaScript.
/// </summary>
public sealed record RespostaDeLogin(string? TokenDeAcesso, DateTimeOffset? ExpiraEm, string? Desafio2Fa = null)
{
    public bool Requer2Fa => Desafio2Fa is not null;
}

/// <param name="LembrarAparelho">Não pedir o código neste aparelho por 30 dias (a senha continua sendo pedida).</param>
public sealed record SegundaEtapaDoLogin(
    [Required, StringLength(2048)] string Desafio,
    [Required, StringLength(10)] string Codigo,
    bool LembrarAparelho = false);

public sealed record LoginComCodigoDeRecuperacao(
    [Required, StringLength(2048)] string Desafio,
    [Required, StringLength(20)] string CodigoDeRecuperacao);

/// <summary>Chave para cadastrar no aplicativo autenticador (digitada ou lida como QR code da URI).</summary>
public sealed record ConfiguracaoDeDoisFatores(string Chave, string UriOtpauth);

public sealed record AtivacaoDeDoisFatores([Required, StringLength(10)] string Codigo);

/// <summary>Os códigos de recuperação aparecem só esta vez; cada um serve para entrar uma vez sem o celular.</summary>
public sealed record RespostaDeAtivacaoDeDoisFatores(IReadOnlyList<string> CodigosDeRecuperacao, RespostaDeLogin Sessao);

public sealed record CodigosDeRecuperacao(IReadOnlyList<string> Codigos);

public sealed record PedidoDeRecuperacaoDeSenha(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(2048)] string TokenTurnstile) : IComVerificacaoHumana;

public sealed record RedefinicaoDeSenha(
    [Required] Guid UsuarioId,
    [Required, StringLength(2048)] string Token,
    [Required, StringLength(128)] string NovaSenha);

/// <summary>Cadastro de aluno pelo link do convite. O e-mail vem do convite, que já prova a posse dele.</summary>
public sealed record CadastroDeAluno(
    [Required, StringLength(2048)] string TokenConvite,
    [Required, StringLength(120)] string Nome,
    [Required, StringLength(128)] string Senha);

/// <summary>Confirmação de identidade para ações sensíveis da própria conta.</summary>
public sealed record ConfirmacaoDeSenha([Required, StringLength(128)] string Senha);

/// <param name="PrecisaConfigurarDoisFatores">Personal sem 2FA: o app deve levar à configuração antes de tudo.</param>
/// <param name="ContaInativa">Conta pausada pela própria pessoa: o app oferece "Reativar minha conta".</param>
/// <param name="CorpoDoDesenho">Desenho do corpo nos músculos dos exercícios; nulo em conta sem perfil (admin).</param>
public sealed record DadosDaConta(
    Guid Id, string Email, IReadOnlyList<string> Papeis, bool DoisFatoresAtivo, bool PrecisaConfigurarDoisFatores, bool ContaInativa,
    CorpoDoDesenho? CorpoDoDesenho = null);

/// <summary>Desenho do corpo que mostra os músculos de cada exercício (preferência de exibição, não é cadastro de sexo).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CorpoDoDesenho>))]
public enum CorpoDoDesenho
{
    Masculino = 1,
    Feminino = 2,
}

public sealed record PreferenciaDeDesenho([Required] CorpoDoDesenho CorpoDoDesenho);
