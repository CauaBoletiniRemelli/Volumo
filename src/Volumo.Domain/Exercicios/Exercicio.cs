using Volumo.Domain.Comum;

namespace Volumo.Domain.Exercicios;

/// <summary>
/// Exercício do catálogo da plataforma (sem personal) ou criado por um personal,
/// visível só para ele e os alunos dele.
/// </summary>
public sealed class Exercicio : EntidadeInativavel
{
    public const int NomeMaximo = 100;
    public const int MaximoSecundarios = 5;
    public const int MaximoPassos = 10;
    public const int PassoMaximo = 300;
    public const int ChaveDeArquivoMaxima = 300;

    // Guardados como texto (grupos separados por vírgula; passos separados por quebra de linha):
    // poucos itens, só lidos junto com o exercício, e assim o catálogo semeado não precisa de valor.
    private string? _secundarios;
    private string? _instrucoes;

    private Exercicio()
    {
    }

    private Exercicio(Guid id, Guid? personalId, string nome, GrupoMuscular grupoMuscular)
        : base(id)
    {
        PersonalId = personalId;
        DefinirDados(nome, grupoMuscular);
    }

    public string Nome { get; private set; } = null!;

    public GrupoMuscular GrupoMuscular { get; private set; }

    /// <summary>Nulo para exercícios do catálogo da plataforma.</summary>
    public Guid? PersonalId { get; private set; }

    public bool DoCatalogo => PersonalId is null;

    /// <summary>Outros grupos trabalhados, além do principal.</summary>
    public IReadOnlyList<GrupoMuscular> MusculosSecundarios =>
        string.IsNullOrEmpty(_secundarios) ? [] : [.. _secundarios.Split(',').Select(Enum.Parse<GrupoMuscular>)];

    public Equipamento? Equipamento { get; private set; }

    /// <summary>Instruções curtas de execução, em passos.</summary>
    public IReadOnlyList<string> Instrucoes =>
        string.IsNullOrEmpty(_instrucoes) ? [] : _instrucoes.Split('\n');

    /// <summary>Chave da foto no armazenamento de arquivos (não é URL: a URL é gerada na hora, com prazo).</summary>
    public string? FotoChave { get; private set; }

    /// <summary>Chave do vídeo curto de execução no armazenamento de arquivos.</summary>
    public string? VideoChave { get; private set; }

    public static Exercicio CriarDoCatalogo(Guid id, string nome, GrupoMuscular grupoMuscular) =>
        new(id, null, nome, grupoMuscular);

    public static Exercicio CriarPersonalizado(Guid id, Guid personalId, string nome, GrupoMuscular grupoMuscular) =>
        new(id, Guarda.Referencia(personalId, "Personal"), nome, grupoMuscular);

    public void Atualizar(string nome, GrupoMuscular grupoMuscular)
    {
        GarantirAtivo();
        DefinirDados(nome, grupoMuscular);
    }

    /// <summary>
    /// Pacote de informações do catálogo (CLAUDE.md, seção 1): músculos secundários, equipamento e
    /// instruções em passos. Tudo é validado antes: se algo for inválido, nada muda.
    /// </summary>
    public void DefinirDetalhes(IReadOnlyList<GrupoMuscular> secundarios, Equipamento? equipamento, IReadOnlyList<string> instrucoes)
    {
        ArgumentNullException.ThrowIfNull(secundarios);
        ArgumentNullException.ThrowIfNull(instrucoes);
        GarantirAtivo();

        var grupos = secundarios.Select(g => Guarda.EnumValido(g, "Músculo secundário")).Distinct().Where(g => g != GrupoMuscular).ToList();
        if (grupos.Count > MaximoSecundarios)
        {
            throw new RegraDeNegocioException($"Informe no máximo {MaximoSecundarios} músculos secundários.");
        }

        if (equipamento is { } valor)
        {
            Guarda.EnumValido(valor, "Equipamento");
        }

        // Quebras de linha dentro de um passo viram espaço: a quebra separa os passos.
        var passos = instrucoes
            .Select(p => p?.ReplaceLineEndings(" ").Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => Guarda.Texto(p, "Passo das instruções", PassoMaximo))
            .ToList();
        if (passos.Count > MaximoPassos)
        {
            throw new RegraDeNegocioException($"As instruções podem ter no máximo {MaximoPassos} passos.");
        }

        _secundarios = grupos.Count == 0 ? null : string.Join(',', grupos);
        Equipamento = equipamento;
        _instrucoes = passos.Count == 0 ? null : string.Join('\n', passos);
    }

    /// <summary>Troca a foto. Devolve a chave anterior, para o arquivo antigo ser removido do armazenamento.</summary>
    public string? DefinirFoto(string? chave)
    {
        GarantirAtivo();
        var anterior = FotoChave;
        FotoChave = Guarda.TextoOpcional(chave, "Chave da foto", ChaveDeArquivoMaxima);
        return anterior;
    }

    /// <summary>Troca o vídeo. Devolve a chave anterior, para o arquivo antigo ser removido do armazenamento.</summary>
    public string? DefinirVideo(string? chave)
    {
        GarantirAtivo();
        var anterior = VideoChave;
        VideoChave = Guarda.TextoOpcional(chave, "Chave do vídeo", ChaveDeArquivoMaxima);
        return anterior;
    }

    private void DefinirDados(string nome, GrupoMuscular grupoMuscular)
    {
        Nome = Guarda.Texto(nome, "Nome do exercício", NomeMaximo);
        GrupoMuscular = Guarda.EnumValido(grupoMuscular, "Grupo muscular");
    }
}
