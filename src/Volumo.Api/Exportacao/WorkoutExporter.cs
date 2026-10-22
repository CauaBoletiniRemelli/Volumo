using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Volumo.Shared.Sessoes;
using Volumo.Shared.Treinos;

namespace Volumo.Api.Exportacao;

/// <summary>Fuso de quem pediu a exportação, em minutos em relação ao UTC (o app manda o do aparelho).</summary>
public static class WorkoutExportTimeZone
{
    /// <summary>Brasília, quando o valor não vem ou é inválido.</summary>
    public static readonly TimeSpan Default = TimeSpan.FromHours(-3);

    public static TimeSpan From(int? minutos) =>
        minutos is { } m and >= -14 * 60 and <= 14 * 60 ? TimeSpan.FromMinutes(m) : Default;
}

/// <summary>Arquivo pronto para download.</summary>
public sealed record ExportedFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Gera o treino (plano + evolução das últimas sessões) ou uma sessão (o treino do dia) em Excel
/// (ClosedXML) ou PDF (QuestPDF). Trabalha só com os contratos já lidos pelas consultas, que passaram
/// pelos filtros de dono. Horários no fuso de quem pediu.
/// </summary>
public sealed class WorkoutExporter
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string Pdf = "application/pdf";

    // PDF em papel branco: grafite no texto e na faixa do topo; o limão só sobre o grafite (nunca sobre branco).
    private const string Grafite = "#0F1115";
    private const string Limao = "#C6F135";
    private const string Cinza = "#5A616C";
    private const string Linha = "#D5D9DE";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    static WorkoutExporter() => QuestPDF.Settings.License = LicenseType.Community;

    public ExportedFile Workout(TreinoDetalhe treino, EvolucaoDoTreino evolucao, FormatoDeExportacao formato, TimeSpan fuso)
    {
        ArgumentNullException.ThrowIfNull(treino);
        ArgumentNullException.ThrowIfNull(evolucao);
        var nome = $"{Slug(treino.Nome)}-{DateTimeOffset.UtcNow.ToOffset(fuso):yyyy-MM-dd}";
        return formato == FormatoDeExportacao.Pdf
            ? new ExportedFile(WorkoutPdf(treino, evolucao, fuso), Pdf, $"{nome}.pdf")
            : new ExportedFile(WorkoutXlsx(treino, evolucao, fuso), Xlsx, $"{nome}.xlsx");
    }

    public ExportedFile Session(SessaoDetalhe sessao, FormatoDeExportacao formato, TimeSpan fuso)
    {
        ArgumentNullException.ThrowIfNull(sessao);
        var nome = $"{Slug(sessao.NomeDoTreino ?? "treino")}-{sessao.IniciadaEm.ToOffset(fuso):yyyy-MM-dd}";
        return formato == FormatoDeExportacao.Pdf
            ? new ExportedFile(SessionPdf(sessao, fuso), Pdf, $"{nome}.pdf")
            : new ExportedFile(SessionXlsx(sessao, fuso), Xlsx, $"{nome}.xlsx");
    }

    // ---------- Excel ----------

    private static byte[] WorkoutXlsx(TreinoDetalhe treino, EvolucaoDoTreino evolucao, TimeSpan fuso)
    {
        using var workbook = new XLWorkbook();
        var plano = workbook.Worksheets.Add("Treino");
        var linhas = new List<object?[]>();
        foreach (var item in treino.Itens.OrderBy(i => i.Ordem))
        {
            for (var s = 0; s < item.Series.Count; s++)
            {
                var serie = item.Series[s];
                linhas.Add([
                    item.Ordem, item.NomeDoExercicio, s + 1, Tipo(serie.Tipo),
                    Repeticoes(serie.RepeticoesMinimas, serie.RepeticoesMaximas), serie.CargaKg, serie.DescansoSegundos,
                    s == 0 ? item.Observacao : null, s == 0 ? item.ObservacaoDoAluno : null,
                ]);
            }
        }

        foreach (var cardio in treino.CardiosOuVazio.OrderBy(c => c.Ordem))
        {
            linhas.Add(["Cardio", cardio.NomeDoExercicio, null, "Cardio", $"{cardio.DuracaoMinutos} min", null, null, cardio.Observacao, cardio.ObservacaoDoAluno]);
        }

        Table(plano, treino.Nome,
            ["Ordem", "Exercício", "Série", "Tipo", "Repetições", "Carga (kg)", "Descanso (s)", "Observação do personal", "Minha observação"], linhas);

        var historico = workbook.Worksheets.Add("Evolução");
        Table(historico, $"{treino.Nome}: últimas sessões",
            ["Data", "Exercício", "Séries", "Maior carga (kg)", "Repetições na maior carga", "Repetições no total", "Volume (kg)"],
            [.. evolucao.Exercicios.SelectMany(e => e.Pontos.Select(p => new object?[]
            {
                p.Data.ToOffset(fuso).DateTime, e.NomeDoExercicio, p.Series, p.CargaMaximaKg, p.RepeticoesNaCargaMaxima, p.Repeticoes, p.VolumeKg,
            })).OrderBy(l => (DateTime)l[0]!)]);
        return Save(workbook);
    }

    private static byte[] SessionXlsx(SessaoDetalhe sessao, TimeSpan fuso)
    {
        using var workbook = new XLWorkbook();
        var planilha = workbook.Worksheets.Add("Sessão");
        var linhas = new List<object?[]>();
        foreach (var exercicio in Grouped(sessao))
        {
            for (var s = 0; s < exercicio.Series.Count; s++)
            {
                var serie = exercicio.Series[s];
                linhas.Add([serie.RealizadaEm.ToOffset(fuso).DateTime, exercicio.Nome, s + 1, serie.CargaKg, serie.Repeticoes, serie.Rir, serie.Repeticoes * serie.CargaKg]);
            }
        }

        foreach (var cardio in sessao.Cardios ?? [])
        {
            linhas.Add([cardio.RealizadoEm.ToOffset(fuso).DateTime, cardio.NomeDoExercicio, null, null, null, null, null, cardio.DuracaoMinutos]);
        }

        Table(planilha, $"{sessao.NomeDoTreino ?? "Treino"} · {Quando(sessao, fuso)}",
            ["Horário", "Exercício", "Série", "Carga (kg)", "Repetições", "Repetições de sobra", "Volume (kg)", "Cardio (min)"], linhas);
        return Save(workbook);
    }

    /// <summary>Título na linha 1 e a tabela (com filtro) a partir da linha 3.</summary>
    private static void Table(IXLWorksheet planilha, string titulo, string[] cabecalho, IReadOnlyList<object?[]> linhas)
    {
        planilha.Cell(1, 1).Value = titulo;
        planilha.Cell(1, 1).Style.Font.Bold = true;
        planilha.Cell(1, 1).Style.Font.FontSize = 14;
        for (var c = 0; c < cabecalho.Length; c++)
        {
            planilha.Cell(3, c + 1).Value = cabecalho[c];
        }

        for (var l = 0; l < linhas.Count; l++)
        {
            for (var c = 0; c < linhas[l].Length; c++)
            {
                var celula = planilha.Cell(4 + l, c + 1);
                switch (linhas[l][c])
                {
                    case null:
                        break;
                    case DateTime data:
                        celula.Value = data;
                        celula.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                        break;
                    case decimal numero:
                        celula.Value = numero;
                        celula.Style.NumberFormat.Format = "0.##";
                        break;
                    case int inteiro:
                        celula.Value = inteiro;
                        break;
                    case var outro:
                        celula.Value = Convert.ToString(outro, PtBr);
                        break;
                }
            }
        }

        var tabela = planilha.Range(3, 1, Math.Max(4, 3 + linhas.Count), cabecalho.Length).CreateTable();
        tabela.Theme = XLTableTheme.TableStyleLight1;
        planilha.Columns(1, cabecalho.Length).AdjustToContents(3, 3 + linhas.Count);
        planilha.SheetView.FreezeRows(3);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var saida = new MemoryStream();
        workbook.SaveAs(saida);
        return saida.ToArray();
    }

    // ---------- PDF ----------

    private static byte[] WorkoutPdf(TreinoDetalhe treino, EvolucaoDoTreino evolucao, TimeSpan fuso) =>
        Document.Create(documento => documento.Page(pagina =>
        {
            Page(pagina, "Treino", treino.Nome, $"Exportado em {DateTimeOffset.UtcNow.ToOffset(fuso).ToString("dd/MM/yyyy HH:mm", PtBr)}");
            pagina.Content().PaddingVertical(16).Column(coluna =>
            {
                coluna.Spacing(14);
                foreach (var item in treino.Itens.OrderBy(i => i.Ordem))
                {
                    coluna.Item().ShowEntire().Column(bloco =>
                    {
                        bloco.Spacing(4);
                        bloco.Item().Text($"{item.Ordem}. {item.NomeDoExercicio}").FontSize(12).SemiBold();
                        bloco.Item().Table(tabela =>
                        {
                            tabela.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                                c.RelativeColumn(2);
                            });
                            Header(tabela, "Série", "Tipo", "Repetições", "Carga", "Descanso");
                            for (var s = 0; s < item.Series.Count; s++)
                            {
                                var serie = item.Series[s];
                                Cells(tabela, (s + 1).ToString(PtBr), Tipo(serie.Tipo),
                                    Repeticoes(serie.RepeticoesMinimas, serie.RepeticoesMaximas), serie.CargaKg is { } carga ? $"{Kg(carga)} kg" : "—",
                                    Descanso(serie.DescansoSegundos));
                            }
                        });
                        Note(bloco, "Personal", item.Observacao);
                        Note(bloco, "Minha observação", item.ObservacaoDoAluno);
                    });
                }

                if (treino.CardiosOuVazio.Count > 0)
                {
                    coluna.Item().ShowEntire().Column(bloco =>
                    {
                        bloco.Spacing(4);
                        bloco.Item().Text("Cardio").FontSize(12).SemiBold();
                        foreach (var cardio in treino.CardiosOuVazio.OrderBy(c => c.Ordem))
                        {
                            bloco.Item().Text($"{cardio.NomeDoExercicio} · {cardio.DuracaoMinutos} min{(string.IsNullOrWhiteSpace(cardio.Observacao) ? null : $" · {cardio.Observacao}")}");
                        }
                    });
                }

                var comHistorico = evolucao.Exercicios.Where(e => e.Pontos.Count > 0).ToList();
                if (comHistorico.Count > 0)
                {
                    coluna.Item().PaddingTop(10).Text("Evolução (últimas sessões)").FontSize(14).Bold();
                    foreach (var exercicio in comHistorico)
                    {
                        coluna.Item().ShowEntire().Column(bloco =>
                        {
                            bloco.Spacing(4);
                            bloco.Item().Text(exercicio.NomeDoExercicio).FontSize(11).SemiBold();
                            bloco.Item().Table(tabela =>
                            {
                                tabela.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(3);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                });
                                Header(tabela, "Data", "Maior carga", "Repetições", "Volume");
                                foreach (var ponto in exercicio.Pontos.TakeLast(10))
                                {
                                    Cells(tabela, ponto.Data.ToOffset(fuso).ToString("dd/MM/yy", PtBr), $"{Kg(ponto.CargaMaximaKg)} kg × {ponto.RepeticoesNaCargaMaxima}",
                                        ponto.Repeticoes.ToString(PtBr), $"{Kg(ponto.VolumeKg)} kg");
                                }
                            });
                        });
                    }
                }
            });
        })).GeneratePdf();

    private static byte[] SessionPdf(SessaoDetalhe sessao, TimeSpan fuso) =>
        Document.Create(documento => documento.Page(pagina =>
        {
            Page(pagina, "Treino do dia", sessao.NomeDoTreino ?? "Treino", Quando(sessao, fuso));
            pagina.Content().PaddingVertical(16).Column(coluna =>
            {
                coluna.Spacing(14);
                var duracao = sessao.FinalizadaEm is { } fim ? Duracao(fim - sessao.IniciadaEm) : "em andamento";
                coluna.Item().Table(tabela =>
                {
                    tabela.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });
                    // Sem total de kg: somar carga de exercícios diferentes não diz nada.
                    Header(tabela, "Duração", "Exercícios", "Séries", "Repetições");
                    Cells(tabela, duracao, sessao.Series.Select(s => s.ExercicioId).Distinct().Count().ToString(PtBr),
                        sessao.Series.Count.ToString(PtBr), sessao.Series.Sum(s => s.Repeticoes).ToString(PtBr));
                });

                foreach (var exercicio in Grouped(sessao))
                {
                    coluna.Item().ShowEntire().Column(bloco =>
                    {
                        bloco.Spacing(4);
                        bloco.Item().Text(exercicio.Nome).FontSize(12).SemiBold();
                        bloco.Item().Table(tabela =>
                        {
                            tabela.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40);
                                c.RelativeColumn(3);
                                c.RelativeColumn(2);
                            });
                            Header(tabela, "Série", "Carga × repetições", "Repetições de sobra");
                            for (var s = 0; s < exercicio.Series.Count; s++)
                            {
                                var serie = exercicio.Series[s];
                                Cells(tabela, (s + 1).ToString(PtBr), $"{Kg(serie.CargaKg)} kg × {serie.Repeticoes}", serie.Rir?.ToString(PtBr) ?? "—");
                            }
                        });
                    });
                }

                if (sessao.Cardios is { Count: > 0 } cardios)
                {
                    coluna.Item().ShowEntire().Column(bloco =>
                    {
                        bloco.Spacing(4);
                        bloco.Item().Text("Cardio").FontSize(12).SemiBold();
                        foreach (var cardio in cardios)
                        {
                            bloco.Item().Text($"{cardio.NomeDoExercicio} · {cardio.DuracaoMinutos} min");
                        }
                    });
                }
            });
        })).GeneratePdf();

    private static void Page(PageDescriptor pagina, string tipo, string titulo, string subtitulo)
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(36);
        pagina.PageColor(Colors.White);
        pagina.DefaultTextStyle(t => t.FontSize(10).FontColor(Grafite));
        pagina.Header().Column(cabecalho =>
        {
            cabecalho.Item().Background(Grafite).PaddingVertical(8).PaddingHorizontal(12).Row(linha =>
            {
                linha.RelativeItem().Text("VOLUMO").FontColor(Limao).Bold().FontSize(12).LetterSpacing(0.1f);
                linha.AutoItem().AlignMiddle().Text(tipo.ToUpperInvariant()).FontColor(Colors.White).FontSize(8).LetterSpacing(0.1f);
            });
            cabecalho.Item().PaddingTop(14).Text(titulo).FontSize(22).Bold();
            cabecalho.Item().Text(subtitulo).FontColor(Cinza);
        });
        pagina.Footer().AlignRight().Text(t =>
        {
            t.DefaultTextStyle(s => s.FontSize(8).FontColor(Cinza));
            t.CurrentPageNumber();
            t.Span(" / ");
            t.TotalPages();
        });
    }

    private static void Header(TableDescriptor tabela, params string[] titulos)
    {
        tabela.Header(cabecalho =>
        {
            foreach (var titulo in titulos)
            {
                cabecalho.Cell().BorderBottom(1).BorderColor(Grafite).PaddingVertical(4).Text(titulo).FontSize(8).SemiBold().FontColor(Cinza);
            }
        });
    }

    private static void Cells(TableDescriptor tabela, params string[] valores)
    {
        foreach (var valor in valores)
        {
            tabela.Cell().BorderBottom(0.5f).BorderColor(Linha).PaddingVertical(4).Text(valor);
        }
    }

    private static void Note(ColumnDescriptor bloco, string quem, string? texto)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            bloco.Item().Text(t =>
            {
                t.Span($"{quem}: ").SemiBold();
                t.Span(texto).FontColor(Cinza);
            });
        }
    }

    // ---------- Comum ----------

    private sealed record ExercicioDaSessao(string Nome, IReadOnlyList<SerieDaSessao> Series);

    /// <summary>Séries agrupadas por exercício, na ordem em que o aluno fez.</summary>
    private static List<ExercicioDaSessao> Grouped(SessaoDetalhe sessao) =>
        [.. sessao.Series.OrderBy(s => s.RealizadaEm).GroupBy(s => s.ExercicioId).Select(g => new ExercicioDaSessao(g.First().NomeDoExercicio, [.. g]))];

    private static string Quando(SessaoDetalhe sessao, TimeSpan fuso)
    {
        var inicio = sessao.IniciadaEm.ToOffset(fuso);
        var texto = inicio.ToString("dd/MM/yyyy, HH:mm", PtBr);
        return sessao.FinalizadaEm is { } fim ? $"{texto} às {fim.ToOffset(fuso).ToString("HH:mm", PtBr)}" : texto;
    }

    private static string Duracao(TimeSpan duracao)
    {
        var minutos = Math.Max(1, (int)Math.Round(duracao.TotalMinutes));
        return minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60:00} min";
    }

    private static string Kg(decimal carga) => carga.ToString("0.##", PtBr);

    private static string Tipo(TipoDeSerie tipo) => tipo switch
    {
        TipoDeSerie.Aquecimento => "Aquecimento",
        TipoDeSerie.Preparatoria => "Preparatória",
        _ => "Trabalho",
    };

    private static string Repeticoes(int minimo, int maximo) => minimo == maximo ? minimo.ToString(PtBr) : $"{minimo}–{maximo}";

    private static string Descanso(int segundos) => segundos switch
    {
        0 => "sem descanso",
        < 60 => $"{segundos} s",
        _ when segundos % 60 == 0 => $"{segundos / 60} min",
        _ => $"{segundos / 60} min {segundos % 60} s",
    };

    /// <summary>Nome de arquivo seguro: "Treino A — Peito" → "treino-a-peito".</summary>
    private static string Slug(string texto)
    {
        var saida = new StringBuilder();
        foreach (var c in texto.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                saida.Append(c);
            }
            else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && saida.Length > 0 && saida[^1] != '-')
            {
                saida.Append('-');
            }
        }

        var slug = saida.ToString().Trim('-');
        return slug.Length == 0 ? "treino" : slug[..Math.Min(slug.Length, 60)];
    }
}
