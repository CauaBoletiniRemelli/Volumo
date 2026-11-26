namespace Volumo.Infrastructure.Armazenamento;

/// <summary>
/// Onde o arquivo fica. Cada área é um bucket separado no R2, com acesso separado: o catálogo é
/// conteúdo da plataforma; o privado guarda dados pessoais (fotos de check-in, vídeos de feedback).
/// </summary>
public enum AreaDeArquivos
{
    Catalogo = 1,
    Privado = 2,
}

/// <summary>
/// Armazenamento de fotos e vídeos fora do banco e fora da pasta pública da API. Os arquivos nunca
/// ficam públicos: a leitura é por URL com prazo, gerada na hora. Em produção é o Cloudflare R2;
/// em desenvolvimento e nos testes, uma pasta local.
/// </summary>
public interface IArmazenamentoDeArquivos
{
    /// <param name="chave">Nome gerado pela API (nunca o nome que veio do usuário).</param>
    /// <param name="tipoDeConteudo">Tipo confirmado pelo conteúdo do arquivo, não pelo que o navegador informou.</param>
    Task GravarAsync(AreaDeArquivos area, string chave, Stream conteudo, string tipoDeConteudo, CancellationToken ct);

    /// <summary>Remove o arquivo. Arquivo que não existe não é erro.</summary>
    Task ApagarAsync(AreaDeArquivos area, string chave, CancellationToken ct);

    /// <summary>URL de leitura que vence em <paramref name="validade"/>. Não acessa a rede.</summary>
    Uri UrlDeLeitura(AreaDeArquivos area, string chave, TimeSpan validade);
}
