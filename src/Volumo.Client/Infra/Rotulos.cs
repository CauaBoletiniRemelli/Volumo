using System.Globalization;
using System.Text;
using Volumo.Shared.Painel;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;

namespace Volumo.Client.Infra;

/// <summary>Textos em português para os enums que chegam da API e formatação de números da tela.</summary>
public static class Rotulos
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string De(GrupoMuscular grupo) => grupo switch
    {
        GrupoMuscular.Peitoral => "Peitoral",
        GrupoMuscular.Costas => "Costas",
        GrupoMuscular.Ombros => "Ombros",
        GrupoMuscular.Biceps => "Bíceps",
        GrupoMuscular.Triceps => "Tríceps",
        GrupoMuscular.Antebraco => "Antebraço",
        GrupoMuscular.Trapezio => "Trapézio",
        GrupoMuscular.Abdomen => "Abdômen",
        GrupoMuscular.Lombar => "Lombar",
        GrupoMuscular.Quadriceps => "Quadríceps",
        GrupoMuscular.Posteriores => "Posteriores",
        GrupoMuscular.Gluteos => "Glúteos",
        GrupoMuscular.Panturrilhas => "Panturrilhas",
        GrupoMuscular.Adutores => "Adutores",
        GrupoMuscular.Abdutores => "Abdutores",
        GrupoMuscular.CorpoInteiro => "Corpo inteiro",
        GrupoMuscular.Cardio => "Cardio",
        _ => grupo.ToString(),
    };

    public static string De(TipoDeSerie tipo) => tipo switch
    {
        TipoDeSerie.Aquecimento => "Aquecimento",
        TipoDeSerie.Preparatoria => "Preparatória",
        _ => "Trabalho",
    };

    /// <summary>" · aquecimento" ou " · preparatória" depois da meta; nada para série de trabalho.</summary>
    public static string? Marcador(TipoDeSerie tipo) => tipo == TipoDeSerie.Trabalho ? null : $" · {De(tipo).ToLower(PtBr)}";

    public static string De(Equipamento equipamento) => equipamento switch
    {
        Equipamento.Barra => "Barra",
        Equipamento.Halteres => "Halteres",
        Equipamento.Maquina => "Máquina",
        Equipamento.Polia => "Polia",
        Equipamento.PesoCorporal => "Peso corporal",
        Equipamento.Kettlebell => "Kettlebell",
        Equipamento.Elastico => "Elástico",
        Equipamento.Anilha => "Anilha",
        Equipamento.Suspensao => "Suspensão (fita)",
        _ => "Outro",
    };

    public static string De(MomentoDoCardio momento) => momento switch
    {
        MomentoDoCardio.Antes => "Antes da musculação",
        MomentoDoCardio.Depois => "Depois da musculação",
        _ => "O aluno escolhe",
    };

    /// <summary>Carga sem casas desnecessárias: 20 → "20", 22.5 → "22,5".</summary>
    public static string Kg(decimal carga) => carga.ToString("0.##", PtBr);

    /// <summary>
    /// Tira os zeros de escala que vêm do banco (20.0000 → 20), senão o campo numérico mostra "20,00".
    /// </summary>
    public static decimal SemZeros(decimal valor) =>
        decimal.Parse(valor.ToString("0.##", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static string Plural(int quantidade, string singular, string plural) => quantidade == 1 ? singular : plural;

    /// <summary>"há 18 dias sem subir" (critério por tempo) ou "4 sessões sem subir" (por sessões).</summary>
    public static string Estagnacao(CriterioDeEstagnacao criterio, ExercicioEstagnado exercicio, DateTimeOffset agora)
    {
        if (criterio == CriterioDeEstagnacao.Sessoes)
        {
            var sessoes = exercicio.SessoesDesdeOAumento;
            return $"{sessoes} {Plural(sessoes, "sessão", "sessões")} sem subir";
        }

        var dias = (agora - exercicio.UltimoAumentoEm).Days;
        return $"há {dias} {Plural(dias, "dia", "dias")} sem subir";
    }

    /// <summary>Texto da configuração: "15 dias sem subir peso" ou "3 sessões sem subir peso".</summary>
    public static string Criterio(ConfiguracaoDeEstagnacao configuracao) =>
        configuracao.Criterio == CriterioDeEstagnacao.Tempo
            ? $"{configuracao.Limite} dias sem subir peso"
            : $"{configuracao.Limite} {Plural(configuracao.Limite, "sessão", "sessões")} sem subir peso";

    public static string Repeticoes(int minimo, int maximo) =>
        minimo == maximo ? minimo.ToString(PtBr) : $"{minimo.ToString(PtBr)}–{maximo.ToString(PtBr)}";

    public static string Descanso(int segundos) => segundos switch
    {
        0 => "sem descanso",
        < 60 => $"{segundos} s",
        _ when segundos % 60 == 0 => $"{segundos / 60} min",
        _ => $"{segundos / 60} min {segundos % 60} s",
    };

    public static string Data(DateTimeOffset quando) => quando.ToLocalTime().ToString("dd/MM/yy", PtBr);

    public static string Hora(DateTimeOffset quando) => quando.ToLocalTime().ToString("HH:mm", PtBr);

    /// <summary>
    /// Linha de uma sessão nas listas: "31 séries · 1 h 05 min · 20 min de cardio". Sem soma de kg: somar
    /// carga de exercícios diferentes não diz nada (o volume que importa é por exercício, no gráfico e no resumo).
    /// </summary>
    public static string NumerosDaSessao(SessaoResumo sessao)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        var partes = new List<string> { $"{sessao.QuantidadeDeSeries} {Plural(sessao.QuantidadeDeSeries, "série", "séries")}" };
        if (sessao.FinalizadaEm is { } fim && fim > sessao.IniciadaEm)
        {
            partes.Add(Duracao(fim - sessao.IniciadaEm));
        }

        if (sessao.MinutosDeCardio > 0)
        {
            partes.Add($"{sessao.MinutosDeCardio} min de cardio");
        }

        return string.Join(" · ", partes);
    }

    /// <summary>"42 min" ou "1 h 05 min".</summary>
    public static string Duracao(TimeSpan duracao)
    {
        var minutos = Math.Max(1, (int)Math.Round(duracao.TotalMinutes));
        return minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60:00} min";
    }

    /// <summary>Nome de arquivo sem acento nem espaço: "Treino A — Peito" → "treino-a-peito".</summary>
    public static string ParaArquivo(string texto)
    {
        var saida = new StringBuilder();
        foreach (var c in ParaBusca(texto))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                saida.Append(c);
            }
            else if (saida.Length > 0 && saida[^1] != '-')
            {
                saida.Append('-');
            }
        }

        var nome = saida.ToString().Trim('-');
        return nome.Length == 0 ? "treino" : nome;
    }

    /// <summary>Para busca: minúsculas e sem acento ("Bíceps" encontra "biceps").</summary>
    public static string ParaBusca(string texto)
    {
        var decomposto = texto.ToLower(PtBr).Normalize(NormalizationForm.FormD);
        var saida = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                saida.Append(c);
            }
        }

        return saida.ToString();
    }
}
