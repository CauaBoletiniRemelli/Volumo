using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Volumo.Api.Arquivos;
using Volumo.Api.Tests.Support;
using Volumo.Infrastructure.Armazenamento;
using Volumo.Shared.Admin;
using Volumo.Shared.Treinos;
using static Volumo.Api.Tests.Support.ContasDeTeste;

namespace Volumo.Api.Tests.Treinos;

/// <summary>Catálogo de exercícios mantido pela TI (Admin com 2FA): dados, foto e vídeo de execução.</summary>
public sealed class CatalogoDaTiTests(VolumoApiFactory factory) : IClassFixture<VolumoApiFactory>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52, 1, 2, 3];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 2, 3, 4, 5];
    private static readonly byte[] Mp4 = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D, 0, 0, 2, 0, 9, 9];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exercicio_criado_pela_ti_aparece_para_o_personal_com_os_detalhes()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var nome = $"Supino máquina {Guid.NewGuid():N}";

        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados(nome)));

        var detalhe = await GetAsync<ExercicioDetalhe>(client, $"/exercicios/{id}", tokenPersonal);
        detalhe.Nome.ShouldBe(nome);
        detalhe.DoCatalogo.ShouldBeTrue();
        detalhe.MusculosSecundarios.ShouldBe([GrupoMuscular.Triceps, GrupoMuscular.Ombros]);
        detalhe.Equipamento.ShouldBe(Equipamento.Maquina);
        detalhe.Instrucoes.ShouldBe(["Ajuste o banco na altura do peito.", "Empurre sem travar os cotovelos."]);
        detalhe.FotoUrl.ShouldBeNull();
        (await GetAsync<List<ExercicioResumo>>(client, $"/exercicios?busca={Uri.EscapeDataString(nome)}", tokenPersonal)).Single().Id.ShouldBe(id);
    }

    [Fact]
    public async Task Editar_troca_dados_e_detalhes()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados("Antes")));

        (await EnviarAsync(client, HttpMethod.Put, $"/admin/exercicios/{id}", tokenAdmin,
            corpo: new DadosDoExercicioDoCatalogo("Depois", GrupoMuscular.Costas, [], null, ["Um passo só."])))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detalhe = await GetAsync<ExercicioDetalhe>(client, $"/admin/exercicios/{id}", tokenAdmin);
        (detalhe.Nome, detalhe.GrupoMuscular, detalhe.Equipamento).ShouldBe(("Depois", GrupoMuscular.Costas, (Equipamento?)null));
        detalhe.MusculosSecundarios.ShouldBeEmpty();
        detalhe.Instrucoes.ShouldBe(["Um passo só."]);
    }

    [Fact]
    public async Task Foto_enviada_fica_disponivel_por_url_com_prazo_e_aparece_na_busca()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var nome = $"Remada {Guid.NewGuid():N}";
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados(nome)));

        using var envio = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", Png, "foto.png");

        envio.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fotoUrl = (await envio.Content.ReadFromJsonAsync<ExercicioDetalhe>(Ct))!.FotoUrl!;
        using var arquivo = await client.GetAsync(new Uri(fotoUrl), Ct);
        arquivo.StatusCode.ShouldBe(HttpStatusCode.OK, "a URL vale sem login: o token é a autorização");
        arquivo.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await arquivo.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Png);
        (await GetAsync<List<ExercicioResumo>>(client, $"/exercicios?busca={nome}", tokenPersonal)).Single().FotoUrl.ShouldNotBeNull();
    }

    [Fact]
    public async Task Foto_do_exercicio_vem_no_treino_para_o_aluno_e_o_personal()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var (emailAluno, tokenAluno, _, tokenPersonal) = await AlunoViaConviteAsync(client, factory);
        var comFoto = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados($"Crucifixo {Guid.NewGuid():N}")));
        (await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{comFoto}/foto", Png, "foto.png")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var semFoto = await ExercicioDoCatalogoAsync(client, tokenPersonal);
        var alunoId = await IdDoUsuarioAsync(emailAluno);
        var treinoId = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, $"/alunos/{alunoId}/treinos", tokenPersonal, corpo: Plano("Treino A", [comFoto, semFoto])));

        foreach (var token in new[] { tokenAluno, tokenPersonal })
        {
            var itens = (await GetAsync<TreinoDetalhe>(client, $"/treinos/{treinoId}", token)).Itens;
            using var foto = await client.GetAsync(new Uri(itens.Single(i => i.ExercicioId == comFoto).FotoUrl!), Ct);
            (await foto.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Png);
            itens.Single(i => i.ExercicioId == semFoto).FotoUrl.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Trocar_a_foto_apaga_o_arquivo_anterior_e_remover_limpa()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados("Com foto")));
        using var primeira = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", Png, "a.png");
        var urlAntiga = (await primeira.Content.ReadFromJsonAsync<ExercicioDetalhe>(Ct))!.FotoUrl!;

        using var segunda = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", Jpeg, "b.jpg");

        var nova = (await segunda.Content.ReadFromJsonAsync<ExercicioDetalhe>(Ct))!.FotoUrl!;
        (await client.GetAsync(new Uri(urlAntiga), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using (var arquivoNovo = await client.GetAsync(new Uri(nova), Ct))
        {
            arquivoNovo.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");
        }

        (await EnviarAsync(client, HttpMethod.Post, $"/admin/exercicios/{id}/foto/remover", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<ExercicioDetalhe>(client, $"/admin/exercicios/{id}", tokenAdmin)).FotoUrl.ShouldBeNull();
        (await client.GetAsync(new Uri(nova), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Video_mp4_e_aceito_e_conteudo_que_nao_bate_com_o_formato_e_recusado()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados("Com vídeo")));

        using var video = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/video", Mp4, "execucao.mp4");
        using var textoComoFoto = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", "<script>alert(1)</script>"u8.ToArray(), "foto.png");
        using var fotoComoVideo = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/video", Png, "video.mp4");
        using var vazio = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", [], "vazio.png");

        video.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await video.Content.ReadFromJsonAsync<ExercicioDetalhe>(Ct))!.VideoUrl.ShouldNotBeNull();
        textoComoFoto.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        fotoComoVideo.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        vazio.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Foto_acima_de_2_mb_e_recusada()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados("Foto grande")));
        var grande = new byte[MidiaDoExercicio.FotoMaxima + 1];
        Png.CopyTo(grande, 0);

        using var envio = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", grande, "grande.png");

        envio.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_de_arquivo_adulterado_ou_inventado_responde_404()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados("Token")));
        using var envio = await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{id}/foto", Png, "a.png");
        var url = (await envio.Content.ReadFromJsonAsync<ExercicioDetalhe>(Ct))!.FotoUrl!;

        (await client.GetAsync(new Uri(url[..^4] + "AAAA"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/arquivos/inventado", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inativado_some_da_busca_mas_o_detalhe_continua_para_treinos_antigos()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var nome = $"Antigo {Guid.NewGuid():N}";
        var id = await IdCriadoAsync(await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenAdmin, corpo: Dados(nome)));

        (await EnviarAsync(client, HttpMethod.Post, $"/admin/exercicios/{id}/inativar", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await GetAsync<List<ExercicioResumo>>(client, $"/exercicios?busca={nome}", tokenPersonal)).ShouldBeEmpty();
        (await GetAsync<ExercicioDetalhe>(client, $"/exercicios/{id}", tokenPersonal)).Ativo.ShouldBeFalse();
        (await GetAsync<List<ExercicioDoCatalogoResumo>>(client, "/admin/exercicios", tokenAdmin)).Single(e => e.Id == id).Ativo.ShouldBeFalse();

        (await EnviarAsync(client, HttpMethod.Post, $"/admin/exercicios/{id}/reativar", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<List<ExercicioResumo>>(client, $"/exercicios?busca={nome}", tokenPersonal)).Single().Id.ShouldBe(id);
    }

    [Fact]
    public async Task Matriz_de_autorizacao_do_catalogo_da_ti()
    {
        using var client = factory.CreateHttpsClient();
        var tokenAdmin = await AdminComDoisFatoresAsync(client, factory);
        var (_, _, tokenPersonal) = await PersonalComDoisFatoresAsync(client, factory);
        var emailAdminSemDoisFatores = await PersonalConfirmadoAsync(client, factory);
        await TornarAdminAsync(factory, emailAdminSemDoisFatores);
        var (tokenAdminSemDoisFatores, _) = await EntrarAsync(client, emailAdminSemDoisFatores);
        using var doPersonal = await EnviarAsync(client, HttpMethod.Post, "/exercicios", tokenPersonal, corpo: new DadosDoExercicio("Meu", GrupoMuscular.Costas));
        var exercicioDoPersonal = (await doPersonal.Content.ReadFromJsonAsync<ExercicioResumo>(Ct))!.Id;

        (await EnviarAsync(client, HttpMethod.Get, "/admin/exercicios", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await EnviarAsync(client, HttpMethod.Get, "/admin/exercicios", tokenPersonal)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Get, "/admin/exercicios", tokenAdminSemDoisFatores)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(client, HttpMethod.Post, "/admin/exercicios", tokenPersonal, corpo: Dados("Invasão"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarArquivoAsync(client, tokenPersonal, $"/admin/exercicios/{exercicioDoPersonal}/foto", Png, "a.png")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Exercício de personal não é do catálogo: a TI não edita por aqui.
        (await EnviarAsync(client, HttpMethod.Get, $"/admin/exercicios/{exercicioDoPersonal}", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarArquivoAsync(client, tokenAdmin, $"/admin/exercicios/{exercicioDoPersonal}/foto", Png, "a.png")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(client, HttpMethod.Get, "/admin/exercicios", tokenAdmin)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Url_do_r2_aponta_para_o_bucket_da_area_e_e_assinada_com_prazo()
    {
        using var r2 = new ArmazenamentoR2(new ConfiguracaoR2("conta123", "chave-de-teste", "segredo-de-teste", "volumo-catalogo", "volumo-privado"));

        var catalogo = r2.UrlDeLeitura(AreaDeArquivos.Catalogo, "exercicios/a/foto-1.jpg", TimeSpan.FromHours(6));
        var privado = r2.UrlDeLeitura(AreaDeArquivos.Privado, "checkins/b.jpg", TimeSpan.FromMinutes(10));

        catalogo.Scheme.ShouldBe("https");
        catalogo.Host.ShouldBe("conta123.r2.cloudflarestorage.com");
        catalogo.AbsolutePath.ShouldBe("/volumo-catalogo/exercicios/a/foto-1.jpg");
        catalogo.Query.ShouldContain("X-Amz-Signature=");
        catalogo.Query.ShouldContain("X-Amz-Expires=21600");
        privado.AbsolutePath.ShouldStartWith("/volumo-privado/");
    }

    [Fact]
    public void R2_sem_alguma_credencial_nao_sobe_e_disco_e_recusado_em_producao()
    {
        var semSegredo = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Armazenamento:R2:AccountId"] = "conta",
            ["Armazenamento:R2:AccessKeyId"] = "chave",
            ["Armazenamento:R2:BucketCatalogo"] = "volumo-catalogo",
            ["Armazenamento:R2:BucketPrivado"] = "volumo-privado",
        }).Build();

        Should.Throw<InvalidOperationException>(() => ArmazenamentoSetup.LoadR2(semSegredo)).Message.ShouldContain("Armazenamento__R2__SecretAccessKey");

        using var semProvider = new VolumoApiFactory().WithSetting("Armazenamento:Provider", "");
        Should.Throw<InvalidOperationException>(() => semProvider.CreateHttpsClient()).Message.ShouldContain("Armazenamento:Provider");
    }

    private static DadosDoExercicioDoCatalogo Dados(string nome) => new(
        nome,
        GrupoMuscular.Peitoral,
        [GrupoMuscular.Triceps, GrupoMuscular.Ombros],
        Equipamento.Maquina,
        ["Ajuste o banco na altura do peito.", "Empurre sem travar os cotovelos."]);

    private static async Task<HttpResponseMessage> EnviarArquivoAsync(HttpClient client, string token, string caminho, byte[] conteudo, string nomeDoArquivo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, caminho);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var arquivo = new ByteArrayContent(conteudo);

        // O tipo informado pelo navegador é ignorado: a API olha o conteúdo.
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        request.Content = new MultipartFormDataContent { { arquivo, "arquivo", nomeDoArquivo } };
        return await client.SendAsync(request, Ct);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string caminho, string token)
    {
        using var response = await EnviarAsync(client, HttpMethod.Get, caminho, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
