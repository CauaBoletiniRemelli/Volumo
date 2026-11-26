using System.ComponentModel.DataAnnotations;

namespace Volumo.Shared.Convites;

public sealed record NovoConvite([Required, EmailAddress, StringLength(256)] string Email);

/// <param name="Situacao">Pendente, Aceito, Revogado ou Expirado.</param>
public sealed record ConviteEnviado(Guid Id, string Email, string Situacao, DateTimeOffset CriadoEm, DateTimeOffset ExpiraEm);

/// <summary>Token do link do convite. Vai no corpo (não na URL da API) para não parar em logs.</summary>
public sealed record TokenDeConvite([Required, StringLength(2048)] string Token);

/// <param name="ContaExistente">O e-mail convidado já tem conta: o app pede para entrar em vez de cadastrar.</param>
public sealed record ResumoDoConvite(string NomeDoPersonal, string Email, DateTimeOffset ExpiraEm, bool ContaExistente);
