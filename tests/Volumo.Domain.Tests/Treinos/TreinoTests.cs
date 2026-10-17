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

    private Treino CriarTreino() => Treino.CriarPeloPersonal(Dados.NovoId(), _personalId, _alunoId, "Treino A");
}
