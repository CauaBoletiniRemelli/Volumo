using System.ComponentModel.DataAnnotations;

namespace Volumo.Shared.Autenticacao;

// Contratos da API de autenticação, compartilhados com o front (Blazor).
// O tamanho mínimo da senha é validado pelo Identity no servidor (10 caracteres).

public sealed record CadastroDePersonal(
    [Required, StringLength(120)] string Nome,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Senha,
    [StringLength(20)] string? Cref);

public sealed record ConfirmacaoDeEmail(
    [Required] Guid UsuarioId,
    [Required, StringLength(2048)] string Token);

public sealed record PedidoDeReenvioDeConfirmacao(
    [Required, EmailAddress, StringLength(256)] string Email);

public sealed record PedidoDeLogin(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128)] string Senha);

/// <summary>O refresh token não vem aqui: vai num cookie HttpOnly, inacessível ao JavaScript.</summary>
public sealed record RespostaDeLogin(string TokenDeAcesso, DateTimeOffset ExpiraEm);

public sealed record DadosDaConta(Guid Id, string Email, IReadOnlyList<string> Papeis);
