using Volumo.Domain.Comum;
using Volumo.Domain.Treinos;

namespace Volumo.Domain.Tests.Treinos;

public sealed class TreinoTests
{
    private readonly Guid _personalId = Dados.NovoId();
    private readonly Guid _alunoId = Dados.NovoId();

    [Fact]
    public void Itens_ficam_na_ordem_em_que_foram_adicionados()
    {
        var treino = CriarTreino();
        var supino = Dados.NovoId();
        var crucifixo = Dados.NovoId();

        treino.AdicionarItem(supino, Dados.Series(4));
        treino.AdicionarItem(crucifixo, Dados.Series(3), "Cadência 3-0-1");

        treino.Itens.Select(i => i.ExercicioId).ShouldBe([supino, crucifixo]);
        treino.Itens.Select(i => i.Ordem).ShouldBe([1, 2]);
        treino.Itens[1].Observacao.ShouldBe("Cadência 3-0-1");
    }

    [Fact]
    public void Series_recebem_a_ordem_da_lista()
    {
        var treino = CriarTreino();

        var item = treino.AdicionarItem(Dados.NovoId(), [Dados.SerieDeTrabalho(cargaKg: 20), Dados.SerieDeTrabalho(cargaKg: 22.5m)]);

        item.Series.Select(s => s.Ordem).ShouldBe([1, 2]);
        item.Series.Select(s => s.CargaKg).ShouldBe([20m, 22.5m]);
    }

    [Fact]
    public void Mesma_instancia_de_serie_pode_ser_usada_em_varios_itens()
    {
        var treino = CriarTreino();
        var serie = Dados.SerieDeTrabalho();

        var primeiro = treino.AdicionarItem(Dados.NovoId(), [serie, serie]);
        var segundo = treino.AdicionarItem(Dados.NovoId(), [serie]);

        primeiro.Series.Select(s => s.Ordem).ShouldBe([1, 2]);
        segundo.Series[0].ShouldNotBeSameAs(primeiro.Series[0]);
    }

    [Fact]
    public void Item_sem_series_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() => CriarTreino().AdicionarItem(Dados.NovoId(), []));
    }

    [Fact]
    public void Item_com_series_demais_e_rejeitado()
    {
        Should.Throw<RegraDeNegocioException>(() =>
            CriarTreino().AdicionarItem(Dados.NovoId(), Dados.Series(ItemTreino.MaximoSeries + 1)));
    }

    [Fact]
    public void Treino_com_exercicios_demais_e_rejeitado()
    {
        var treino = CriarTreino();
        for (var i = 0; i < PlanoDeTreino.MaximoItens; i++)
        {
            treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        }

        Should.Throw<RegraDeNegocioException>(() => treino.AdicionarItem(Dados.NovoId(), Dados.Series(1)));
    }

    [Fact]
    public void Remover_item_renumera_os_seguintes()
    {
        var treino = CriarTreino();
        var a = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        var b = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        var c = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));

        treino.RemoverItem(b.Id);

        treino.Itens.Select(i => i.Id).ShouldBe([a.Id, c.Id]);
        treino.Itens.Select(i => i.Ordem).ShouldBe([1, 2]);
    }

    [Fact]
    public void Remover_item_inexistente_falha()
    {
        Should.Throw<RegraDeNegocioException>(() => CriarTreino().RemoverItem(Dados.NovoId()));
    }

    [Fact]
    public void Reordenar_muda_a_ordem_dos_itens()
    {
        var treino = CriarTreino();
        var a = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        var b = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        var c = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));

        treino.ReordenarItens([c.Id, a.Id, b.Id]);

        treino.Itens.Select(i => i.Id).ShouldBe([c.Id, a.Id, b.Id]);
        treino.Itens.Select(i => i.Ordem).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Reordenar_exige_cada_item_exatamente_uma_vez()
    {
        var treino = CriarTreino();
        var a = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));
        var b = treino.AdicionarItem(Dados.NovoId(), Dados.Series(1));

        Should.Throw<RegraDeNegocioException>(() => treino.ReordenarItens([a.Id]));
        Should.Throw<RegraDeNegocioException>(() => treino.ReordenarItens([a.Id, a.Id]));
        Should.Throw<RegraDeNegocioException>(() => treino.ReordenarItens([a.Id, Dados.NovoId()]));
        treino.Itens.Select(i => i.Id).ShouldBe([a.Id, b.Id]);
    }

    [Fact]
    public void Treino_gerado_do_template_e_uma_copia_independente()
    {
        var template = TemplateTreino.Criar(Dados.NovoId(), _personalId, "Superiores A");
        var itemDoTemplate = template.AdicionarItem(Dados.NovoId(), Dados.Series(3), "Pegada aberta");

        var treino = template.GerarTreinoPara(Dados.NovoId(), _alunoId);
        template.DefinirSeries(itemDoTemplate.Id, Dados.Series(5));
        template.Renomear("Superiores A (v2)");

        treino.Nome.ShouldBe("Superiores A");
        treino.AlunoId.ShouldBe(_alunoId);
        treino.PersonalId.ShouldBe(_personalId);
        treino.TemplateOrigemId.ShouldBe(template.Id);
        treino.Itens.Count.ShouldBe(1);
        treino.Itens[0].Id.ShouldNotBe(itemDoTemplate.Id);
        treino.Itens[0].Observacao.ShouldBe("Pegada aberta");
        treino.Itens[0].Series.Count.ShouldBe(3);
    }

    [Fact]
    public void Substituir_itens_troca_tudo_na_ordem_recebida()
    {
        var treino = CriarTreino();
        treino.AdicionarItem(Dados.NovoId(), Dados.Series(2));
        var a = Dados.NovoId();
        var b = Dados.NovoId();

        treino.SubstituirItens([new DefinicaoDeItem(a, Dados.Series(3), "Pausa de 2 s"), new DefinicaoDeItem(b, Dados.Series(1))]);

        treino.Itens.Select(i => (i.ExercicioId, i.Ordem, i.Series.Count)).ShouldBe([(a, 1, 3), (b, 2, 1)]);
        treino.Itens[0].Observacao.ShouldBe("Pausa de 2 s");
    }

    [Fact]
    public void Substituir_com_item_invalido_nao_muda_nada()
    {
        var treino = CriarTreino();
        var original = treino.AdicionarItem(Dados.NovoId(), Dados.Series(2));

        Should.Throw<RegraDeNegocioException>(() =>
            treino.SubstituirItens([new DefinicaoDeItem(Dados.NovoId(), Dados.Series(1)), new DefinicaoDeItem(Dados.NovoId(), [])]));

        treino.Itens.Single().Id.ShouldBe(original.Id);
    }

    [Fact]
    public void Substituir_itens_de_treino_inativo_falha()
    {
        var treino = CriarTreino();
        treino.Inativar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => treino.SubstituirItens([]));
    }

    [Fact]
    public void Treino_montado_pelo_aluno_nao_tem_personal()
    {
        Treino.CriarPeloAluno(Dados.NovoId(), _alunoId, "Meu treino").PersonalId.ShouldBeNull();
    }

    [Fact]
    public void Inativar_guarda_a_primeira_data_e_reativar_volta_ao_normal()
    {
        var treino = CriarTreino();

        treino.Inativar(Dados.Agora);
        treino.Inativar(Dados.Agora.AddDays(1));

        treino.Ativo.ShouldBeFalse();
        treino.InativadoEm.ShouldBe(Dados.Agora);

        treino.Reativar();

        treino.Ativo.ShouldBeTrue();
        treino.InativadoEm.ShouldBeNull();
    }

    [Fact]
    public void Treino_inativo_nao_pode_ser_alterado()
    {
        var treino = CriarTreino();
        var item = treino.AdicionarItem(Dados.NovoId(), Dados.Series(2));
        treino.Inativar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => treino.Renomear("Outro nome"));
        Should.Throw<RegraDeNegocioException>(() => treino.AdicionarItem(Dados.NovoId(), Dados.Series(1)));
        Should.Throw<RegraDeNegocioException>(() => treino.DefinirSeries(item.Id, Dados.Series(1)));
        Should.Throw<RegraDeNegocioException>(() => treino.DefinirObservacao(item.Id, "Nova"));
        Should.Throw<RegraDeNegocioException>(() => treino.RemoverItem(item.Id));
        Should.Throw<RegraDeNegocioException>(() => treino.ReordenarItens([item.Id]));
        treino.Itens.Single().Series.Count.ShouldBe(2);
    }

    [Fact]
    public void Template_inativo_nao_gera_treino()
    {
        var template = TemplateTreino.Criar(Dados.NovoId(), _personalId, "Antigo");
        template.Inativar(Dados.Agora);

        Should.Throw<RegraDeNegocioException>(() => template.GerarTreinoPara(Dados.NovoId(), _alunoId));
    }

    [Fact]
    public void Treino_novo_nao_tem_cardio_e_recomenda_depois()
    {
        var treino = CriarTreino();

        treino.Cardios.ShouldBeEmpty();
        treino.MomentoDoCardio.ShouldBe(MomentoDoCardio.Depois);
    }

    [Fact]
    public void Substituir_cardios_guarda_ordem_duracao_e_momento()
    {
        var treino = CriarTreino();
        var esteira = Dados.NovoId();
        var bike = Dados.NovoId();

        treino.SubstituirCardios([new DefinicaoDeCardio(esteira, 10, "Aquecimento leve"), new DefinicaoDeCardio(bike, 20)], MomentoDoCardio.Antes);

        treino.Cardios.Select(c => (c.ExercicioId, c.Ordem, c.DuracaoMinutos)).ShouldBe([(esteira, 1, 10), (bike, 2, 20)]);
        treino.Cardios[0].Observacao.ShouldBe("Aquecimento leve");
        treino.MomentoDoCardio.ShouldBe(MomentoDoCardio.Antes);
        treino.Itens.ShouldBeEmpty("cardio não entra na musculação");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ItemCardio.DuracaoMaximaMinutos + 1)]
    public void Cardio_com_duracao_invalida_falha_sem_mudar_nada(int minutos)
    {
        var treino = CriarTreino();
        treino.SubstituirCardios([new DefinicaoDeCardio(Dados.NovoId(), 15)], MomentoDoCardio.Depois);

        Should.Throw<RegraDeNegocioException>(() =>
            treino.SubstituirCardios([new DefinicaoDeCardio(Dados.NovoId(), 10), new DefinicaoDeCardio(Dados.NovoId(), minutos)], MomentoDoCardio.Antes));

        treino.Cardios.Single().DuracaoMinutos.ShouldBe(15);
        treino.MomentoDoCardio.ShouldBe(MomentoDoCardio.Depois);
    }

    [Fact]
    public void Cardios_demais_ou_momento_invalido_falham()
    {
        var treino = CriarTreino();
        var demais = Enumerable.Range(0, PlanoDeTreino.MaximoCardios + 1).Select(_ => new DefinicaoDeCardio(Dados.NovoId(), 10)).ToList();

        Should.Throw<RegraDeNegocioException>(() => treino.SubstituirCardios(demais, MomentoDoCardio.Depois));
        Should.Throw<RegraDeNegocioException>(() => treino.SubstituirCardios([], (MomentoDoCardio)99));
    }

    [Fact]
    public void Treino_gerado_do_template_copia_o_cardio()
    {
        var template = TemplateTreino.Criar(Dados.NovoId(), _personalId, "Modelo");
        template.SubstituirCardios([new DefinicaoDeCardio(Dados.NovoId(), 25, "Zona 2")], MomentoDoCardio.AlunoEscolhe);

        var treino = template.GerarTreinoPara(Dados.NovoId(), _alunoId);

        treino.Cardios.Single().DuracaoMinutos.ShouldBe(25);
        treino.Cardios[0].Id.ShouldNotBe(template.Cardios[0].Id);
        treino.MomentoDoCardio.ShouldBe(MomentoDoCardio.AlunoEscolhe);
    }

    [Fact]
    public void Posicionar_define_a_ordem_e_recusa_negativo_ou_treino_inativo()
    {
        var treino = CriarTreino();

        treino.Posicionar(3);

        treino.Ordem.ShouldBe(3);
        Should.Throw<RegraDeNegocioException>(() => treino.Posicionar(-1));
        treino.Inativar(Dados.Agora);
        Should.Throw<RegraDeNegocioException>(() => treino.Posicionar(1));
    }

    [Fact]
    public void MarcarDias_substitui_os_dias_e_recusa_dia_invalido_ou_treino_inativo()
    {
        var treino = CriarTreino();

        treino.MarcarDias(DiasDaSemana.Segunda | DiasDaSemana.Quinta);
        treino.MarcarDias(DiasDaSemana.Terca);

        treino.Dias.ShouldBe(DiasDaSemana.Terca);
        Should.Throw<RegraDeNegocioException>(() => treino.MarcarDias((DiasDaSemana)128));
        treino.Inativar(Dados.Agora);
        Should.Throw<RegraDeNegocioException>(() => treino.MarcarDias(DiasDaSemana.Nenhum));
    }

    [Fact]
    public void Dias_convertem_de_e_para_a_lista_de_DayOfWeek()
    {
        var dias = new[] { DayOfWeek.Thursday, DayOfWeek.Monday, DayOfWeek.Monday }.ParaDias();

        dias.ShouldBe(DiasDaSemana.Segunda | DiasDaSemana.Quinta);
        dias.ParaLista().ShouldBe([DayOfWeek.Monday, DayOfWeek.Thursday]);
        DiasDaSemana.Nenhum.ParaLista().ShouldBeEmpty();
    }

    private Treino CriarTreino() => Treino.CriarPeloPersonal(Dados.NovoId(), _personalId, _alunoId, "Treino A");
}
