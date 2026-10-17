# Contexto do backend: onde paramos

Última atualização: 16/10/2026 · Branch: `Criação-Back-End` · Fases 0–2 no working tree (o usuário commita).

Use este arquivo junto com o `CLAUDE.md` (planejamento do produto) para retomar o trabalho.

---

## 1. Decisões tomadas

| Tema | Decisão |
|---|---|
| Hospedagem | **MonsterASP.NET** (https://admin.monsterasp.net/) para API e site (IIS + SQL Server). Já registrado no CLAUDE.md. |
| Estrutura | Reorganizada em `src/` e `tests/`, como na seção 8 do CLAUDE.md |
| Banco nos testes | **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`, versão 17). Sem Docker na máquina, então nada de Testcontainers |
| Asserções | **Shouldly** (a FluentAssertions passou a ser paga na v8) |
| Domínio | **Um aluno tem só um personal** (Personal 1 → N Alunos) |
| Domínios em produção | Mesmo site para front e API (ex.: `app.volumo.com.br` + `api.volumo.com.br`), por causa do cookie SameSite=Strict do refresh token |
| E2E | Começa com jornadas completas da API; Playwright só quando o front tiver telas |
| Regra de ouro | Segurança em primeiro lugar. **Nenhum endpoint fica pronto sem os testes da matriz de autorização:** anônimo → 401, role errada → 403, recurso de outro dono → 404 |

---

## 2. Plano de ação (fases)

| Fase | Conteúdo | Status |
|---|---|---|
| 0. Fundação | Estrutura src/tests, global.json, Directory.Build.props, versões centralizadas, .editorconfig | ✅ Feito |
| 1. Proteções da API | Cabeçalhos de segurança, HSTS, CORS, rate limiting, ProblemDetails, limite de requisição, Serilog, health check | ✅ Feito |
| 2. Domínio + persistência | Entidades, EF Core, migrations, LocalDB, IDs GUID v7 gerados no cliente (sincronização offline) | ✅ Feito (16/10) |
| 3. Autenticação | Identity, confirmação de e-mail, bloqueio após falhas, JWT de 15 min, refresh token rotativo (hash no banco, revogação da sessão se um token antigo for reutilizado, cookie HttpOnly/Secure/SameSite=Strict), 2FA obrigatório para personal, Turnstile, convite de aluno | ⏭️ **Próxima** |
| 4. Autorização contra IDOR | Roles e policies, autorização por recurso, filtros globais do EF por dono, 404 para recurso alheio | Pendente |
| 5. Funcionalidades do MVP | CRUD de templates e treinos, atribuição ao aluno, registro de sessão com idempotência, painel do personal (último treino, sumido há mais de 5 dias, estagnado) | Pendente |
| 6. Cobrança + LGPD | Webhook do gateway com validação de assinatura e idempotência, consentimento, exportação e exclusão de conta | Pendente |
| 7. CI/CD | GitHub Actions (build, testes, `dotnet list package --vulnerable`, CodeQL, Dependabot, OWASP ZAP) e deploy no MonsterASP.NET | Pendente |

### Modelo de domínio (fase 2)

| Entidade | Onde | Regras principais |
|---|---|---|
| `Usuario` | Infrastructure/Identity (`IdentityUser<Guid>`) | Fica fora do Domain para o Domain não depender do Identity. Id GUID v7 |
| `PerfilPersonal` | Domain/Usuarios | **Id = Id do usuário** (1:1). `Cref` opcional, objeto de valor `000000-G/UF` normalizado; único no banco |
| `PerfilAluno` | Domain/Usuarios | Id = Id do usuário |
| `VinculoPersonalAluno` | Domain/Vinculos | Índice único filtrado `UX_VinculosPersonalAluno_AlunoAtivo` (`AlunoId WHERE EncerradoEm IS NULL`): um vínculo ativo por aluno; encerrados ficam como histórico |
| `ConviteAluno` | Domain/Vinculos | Token de 32 bytes aleatórios (Base64Url) só no e-mail; banco guarda o SHA-256 (`binary(32)`, índice único). Validade 7 dias. `Aceitar` compara em tempo constante e devolve o vínculo |
| `Exercicio` | Domain/Exercicios | `PersonalId` nulo = catálogo da plataforma. `GrupoMuscular` gravado como texto |
| `TemplateTreino` / `Treino` | Domain/Treinos | Base comum `PlanoDeTreino` (nome + itens ordenados, máx. 40). `GerarTreinoPara` copia o template com Ids novos. Treino com `PersonalId` nulo = montado pelo aluno |
| `ItemTreino` + `Serie` | Domain/Treinos | Tipos *owned* do EF, com tabelas próprias por dono (`ItensTreino`/`SeriesTreino`, `ItensTemplateTreino`/`SeriesTemplateTreino`). 1–20 séries por item; ordem mantida pelo agregado |
| `SessaoTreino` + `RegistroSerie` | Domain/Sessoes | Ids vêm do app. `RegistrarSerie` com Id repetido devolve o existente (reenvio offline); `Finalizar` de novo não muda nada. Reps 0–200, carga 0–1000 kg (2 casas), RIR 0–10 |

Convenções:
- Regras violadas lançam `RegraDeNegocioException`, com mensagem em português para o usuário final (a API vai convertê-la em 400/422 na fase 5).
- Ids das entidades precisam ser **GUID v7** (`Guid.CreateVersion7()`); referências só não podem ser vazias.
- Coleções expostas como `IReadOnlyList` (cópia ordenada); EF acessa os campos `_itens`, `_series`, `_registros`.
- Datas como `DateTimeOffset`, sempre recebidas por parâmetro (o domínio não lê o relógio).
- Ainda não modelado: auditoria (criado/alterado em), concorrência otimista (rowversion), catálogo inicial de exercícios (seed).

### Nada importante é apagado: inativação e anonimização (decisão de 16/10/2026)

**Regra:** registros importantes nunca saem do banco. No uso normal são **inativados**; quando a pessoa pede a exclusão da conta (LGPD, art. 18), os dados pessoais são **anonimizados**.

- **`EntidadeInativavel`** (Domain/Comum): `InativadoEm`, `Ativo`, `Inativar(agora)` (repetir mantém a data original), `Reativar()`. Um registro inativo não pode ser alterado (`GarantirAtivo`). Usada por: perfis, `Exercicio`, `TemplateTreino`, `Treino`. Vínculo (`EncerradoEm`), convite (`RevogadoEm`) e sessão (histórico, nunca some) têm o próprio ciclo de vida.
- **Itens e séries de treino** só mudam pelo agregado (`plano.DefinirSeries(itemId, …)`, `plano.DefinirObservacao(itemId, …)`), para a regra "inativo não muda" valer também para eles.
- **`Perfil`** (base de `PerfilPersonal`/`PerfilAluno`): `Anonimizar(agora)` troca o nome por "Usuário removido", apaga o CREF, grava `AnonimizadoEm` e inativa. Perfil anonimizado **não pode ser reativado**.
- **Filtro global do EF** com nome `FiltrosDeConsulta.Ativos` (filtros nomeados do EF 10) em perfis, exercícios, templates e treinos: inativos somem das consultas comuns. Para enxergá-los: `IgnoreQueryFilters([FiltrosDeConsulta.Ativos])`. Na fase 4, o filtro por dono entra como **outro** filtro nomeado, sem misturar os dois.
- **Bloqueio de exclusão física**, em duas camadas:
  1. `VolumoDbContext.SaveChanges` lança `InvalidOperationException` se houver entidade em estado `Deleted`. Exceções: tipos *owned* (itens e séries de um treino em edição) e credenciais auxiliares do Identity (logins, tokens, claims, roles do usuário).
  2. Todas as FKs entre entidades importantes são `NO ACTION`: um `DELETE` direto no banco também falha.
  - Não cobre `ExecuteDelete`/`ExecuteUpdate`: **não usar nessas tabelas**.
- **`ExclusaoDeConta.AnonimizarAsync(usuarioId)`** (Infrastructure/Contas, registrada no DI), tudo num único `SaveChanges` e repetível sem efeito:
  - `Usuario.Anonimizar`: e-mail, telefone e senha apagados, `UserName = removido-{id}`, novo `SecurityStamp` (derruba sessões), login bloqueado para sempre; logins externos, tokens, claims e roles removidos.
  - Personal: perfil anonimizado, vínculos ativos encerrados, convites anonimizados (pendentes revogados), templates e exercícios próprios inativados. **Os treinos que ele prescreveu continuam com os alunos.**
  - Aluno: perfil anonimizado, vínculo encerrado, convites anonimizados, treinos inativados. **Sessões e séries registradas ficam** (sem dados pessoais, servem para estatística).
- CREF continua único mesmo entre contas inativas (quem volta reativa a conta antiga); a anonimização libera o CREF.
### Decisões de produto sobre contas (16/10/2026)

**1. Personal inativado (sem excluir a conta)** — implementado
- Os vínculos com os alunos **continuam**; ele reencontra tudo se voltar.
- O aluno não fica preso: ao aceitar convite de outro personal, `AceiteDeConvite.Aceitar` (Domain/Vinculos) encerra o vínculo antigo automaticamente **se o personal atual estiver inativo**. Com personal atual ativo, o aluno precisa encerrar o vínculo antes. O token é validado antes de mexer no vínculo atual.
- Uso na fase 3/5: carregar o vínculo ativo do aluno e o personal dele com `IgnoreQueryFilters([FiltrosDeConsulta.Ativos])`, chamar `AceiteDeConvite.Aceitar` e salvar tudo num único `SaveChanges` (testado: o índice único aceita o encerramento e o novo vínculo juntos).

**1b. Personal que não paga (inadimplência)** — implementar na fase 6, junto com a assinatura (todas as regras aprovadas pelo usuário em 16/10/2026)
- **Carência:** após a falha de pagamento, 5 a 7 dias com acesso normal e avisos (e-mail e aviso no app) antes de qualquer bloqueio. Prazo exato a definir com o gateway (tentativas automáticas de cobrança).
- **Depois da carência, o personal fica impedido de:** criar treinos, editar treinos de alunos e convidar alunos.
- **O personal continua podendo:** ver o painel, os alunos e o histórico, e **exportar os dados** (portabilidade da LGPD; não "sequestrar" dados para cobrar).
- **Escolha oferecida ao personal:** regularizar o pagamento **ou** cair para o plano Grátis, escolhendo até 3 alunos que continuam com edição liberada; os demais ficam só leitura. Assim o inadimplente nunca fica mais limitado que um personal grátis.
- **Alunos não são afetados:** continuam vendo os treinos e registrando sessões normalmente (regra: quem treina com personal nunca paga).
- **Saída do aluno:** aluno de personal inadimplente (após a carência) pode aceitar convite de outro personal, com o vínculo antigo encerrado automaticamente, como no caso do personal inativo. Estender `AceiteDeConvite` para considerar também a situação da assinatura.
- Implementação provável: estado da assinatura no personal (em dia, em carência, inadimplente, rebaixado para Grátis) + policy de autorização (ex.: `PersonalPodeEditar`) nos endpoints de escrita, com teste na matriz de autorização. Rebaixamento para Grátis precisa marcar quais alunos seguem editáveis.

**2. Login de contas inativas ou suspensas** — implementar na fase 3
- Conta anonimizada: login impossível (já garantido: sem senha, sem e-mail, bloqueio permanente).
- Conta **inativada pela própria pessoa** (aluno ou personal): o login é permitido e oferece "Reativar minha conta", mostrando o que foi preservado.
- **Suspensão pela plataforma** (abuso, fraude, CREF falso): estado **separado** da inativação, com motivo e data, que só um Admin desfaz; a pessoa não consegue se reativar. Modelar com as roles de Admin.
- Mensagem de erro do login sempre genérica ("e-mail ou senha inválidos"): nunca revelar que a conta existe, foi excluída ou está suspensa para quem não provou ser o dono.

**3. Check-ins (fotos, peso, medidas, anamnese)** — aplicar quando forem modelados
- Na exclusão de conta, são **apagados de verdade** (única exceção à regra "nada é apagado"): fotos e vídeos removidos do storage, anamnese apagada, peso e medidas apagados (guardar números de saúde "para estatística" é difícil de justificar perante a LGPD).
- O bloqueio de exclusão física no `SaveChanges` precisará de uma lista de tipos liberados para a exclusão por LGPD.
- Consentimento específico e destacado para dados de saúde, gravado com data e versão do termo; revogar o consentimento tem efeito equivalente à exclusão desses dados.
- `Observacao` do treino (texto livre): por ora, sem tratamento; revisar se aparecer dado de saúde de aluno ali.

---

## 3. Estrutura atual da solução

```
Volumo.slnx
├── global.json                 → SDK 10.0.401 + executor de testes Microsoft.Testing.Platform
├── Directory.Build.props       → net10.0, Nullable, aviso vira erro, analisadores de segurança, NuGetAudit
├── Directory.Packages.props    → versões centralizadas + fixação de transitivas (correções de segurança)
├── .editorconfig               → Migrations/ marcadas como código gerado (analisadores ignoram)
├── dotnet-tools.json           → dotnet-ef 10.0.13 como ferramenta local (`dotnet tool restore`)
├── nuget.config                → só nuget.org
├── src/
│   ├── Volumo.Api              → ASP.NET Core Web API (Controllers)
│   │   ├── Program.cs
│   │   ├── Security/
│   │   │   ├── SecurityHeadersMiddleware.cs
│   │   │   ├── CorsSetup.cs
│   │   │   └── RateLimitingSetup.cs   (RateLimitPolicies.Login / .Cadastro)
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── Volumo.Client           → Blazor WebAssembly (ainda é o template; NÃO é PWA ainda: falta manifest/service worker)
│   ├── Volumo.Shared           → vazio (só AssemblyReference); DTOs entram na fase 5
│   ├── Volumo.Domain           → Comum/ (Entidade, Guarda, RegraDeNegocioException), Usuarios/, Vinculos/,
│   │                             Exercicios/, Treinos/, Sessoes/
│   └── Volumo.Infrastructure
│       ├── DependencyInjection.cs        → AddVolumoInfrastructure(connectionString)
│       ├── Identity/Usuario.cs
│       └── Persistencia/
│           ├── VolumoDbContext.cs        → IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>
│           ├── VolumoDbContextFactory.cs → usada só pelo dotnet ef
│           ├── Configuracoes/            → IEntityTypeConfiguration por entidade
│           └── Migrations/               → Inicial, InativacaoEAnonimizacao
│       └── Contas/ExclusaoDeConta.cs     → anonimização da conta (LGPD)
└── tests/
    ├── Directory.Build.props   → pacotes e usings comuns dos testes (xUnit v3, Shouldly, cobertura)
    ├── Volumo.Domain.Tests     → regras das entidades (sem banco)
    ├── Volumo.Api.Tests        → testes de integração (WebApplicationFactory + LocalDB)
    └── Volumo.Architecture.Tests → regras de dependência entre camadas (NetArchTest)
```

### Banco de dados
- Development: LocalDB, banco `Volumo` (connection string em `appsettings.Development.json`; não é segredo, usa autenticação do Windows).
- Produção: variável de ambiente `ConnectionStrings__Volumo`. Sem connection string a API **não sobe**.
- Migrations (rodar na raiz):
  - nova: `dotnet ef migrations add <Nome> -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure -o Persistencia/Migrations`
  - aplicar no LocalDB: `dotnet ef database update -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure`
  - script para produção: `dotnet ef migrations script --idempotent -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure -o migracao.sql`
- Logs do EF em `Warning` (o SQL das consultas não vai para o log em produção).

Referências: Client → Shared · Api → Shared, Domain, Infrastructure · Infrastructure → Domain · Domain → nenhuma.

Os nomes antigos (`VolumoAPI`, `VolumoWebApp`) foram renomeados com `git mv`, então o histórico foi preservado. Os namespaces do Client agora são `Volumo.Client`.

---

## 4. O que a API já faz (fase 1)

Ordem do pipeline em `Program.cs`:

1. `SecurityHeadersMiddleware`: usa `OnStarting` para os cabeçalhos valerem também nas respostas de erro.
   - `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`
   - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
   - `Referrer-Policy: no-referrer`, `Permissions-Policy`, `Cross-Origin-Resource-Policy: same-site`
   - `Cache-Control: no-store` (se não houver outro definido)
2. `UseExceptionHandler` + `UseStatusCodePages`: erros sempre em ProblemDetails com `traceId`, sem stack trace.
3. `UseHsts` fora do Development: 365 dias, includeSubDomains.
4. `UseHttpsRedirection`.
5. `UseSerilogRequestLogging`: loga o caminho sem query string e sem corpo.
6. `UseCors`: só origens de `Cors:AllowedOrigins`, com credenciais. Se houver `*`, barra final ou URL inválida, a API **não sobe**.
7. `UseRateLimiter`: janela fixa por IP.
   - Global: 100 requisições por minuto.
   - `login`: 5 por minuto.
   - `cadastro`: 3 a cada 10 minutos.
   - Excesso → 429 com `Retry-After` em ProblemDetails.
8. `UseAuthorization` → `/health` e Controllers.

`/health` inclui o banco (`AddDbContextCheck`): 200 `Healthy` ou 503 `Unhealthy`, sem detalhes.

Outros ajustes:
- Kestrel sem o cabeçalho `Server`.
- Limite de 1 MB no corpo das requisições (Kestrel e IIS).
- OpenAPI só em Development.

### Configuração (`appsettings.json`)
- `Serilog:MinimumLevel`
- `Cors:AllowedOrigins`: vazio em produção. Em Development: `https://localhost:7184` e `http://localhost:5140` (portas do Client).
- `RateLimiting:Global|Login|Cadastro` (`PermitLimit`, `WindowSeconds`)
- User Secrets habilitado na API (`UserSecretsId` no csproj).

### Portas locais
- API: `https://localhost:7010` / `http://localhost:5095`
- Client: `https://localhost:7184` / `http://localhost:5140`

---

## 5. Testes

Rodar tudo: **`dotnet test --solution Volumo.slnx`** (o `--solution` é necessário com o executor novo).

Situação atual: **137 testes, todos passando** (~10 s). Precisa do LocalDB instalado.

### Volumo.Domain.Tests
- Uma classe por entidade/área (`PerfisTests`, `ConviteAlunoTests`, `VinculoPersonalAlunoTests`, `ExercicioTests`, `SerieTests`, `TreinoTests`, `SessaoTreinoTests`).
- `Dados`: data fixa (`Dados.Agora`), `NovoId()` e séries prontas.

### Volumo.Api.Tests
- `Support/BancoDeTestes`: *assembly fixture* que apaga e recria o banco **`Volumo_Testes`** no LocalDB a cada execução, aplicando as migrations reais. O banco fica lá no fim, para inspeção.
  - **Sem Respawn/limpeza entre testes:** cada teste cria os próprios usuários com Ids novos (`Support/Cenario`), e as consultas da aplicação serão sempre filtradas pelo dono. Os testes não se enxergam e rodam em paralelo. Se algum teste precisar de banco vazio, reavaliar.
  - `BancoDeTestes.NovoContexto()` para testes de persistência; a `VolumoApiFactory` aponta para o mesmo banco.
- `Persistencia/`: ida e volta de treino/template/sessão, alteração de *owned types* em agregado já salvo, inserção de registros com Id do cliente, índice único do vínculo ativo, CREF único, convite localizado pelo hash, inativação com filtro global, bloqueio de exclusão física (`ExclusaoFisicaTests`) e anonimização de conta de personal e de aluno (`ExclusaoDeContaTests`, que resolve o serviço pelo DI da API).
- `VolumoApiFactory`: sobe a API no ambiente **"Testing"**, que se comporta como produção.
  - Base `https://api.volumo.test`: precisa ser HTTPS e não pode ser localhost, senão o HSTS não é enviado.
  - Configurações passadas com `UseSetting`, e **não** com `ConfigureAppConfiguration`: o Program.cs lê o CORS antes do `Build()`, e a configuração do segundo jeito chega tarde demais.
  - `WithSetting(chave, valor)` muda configurações por teste.
  - `https_port=443` precisa estar definido, senão não há redirecionamento para HTTPS.
- `Support/TestOnlyController`: rota `/__test/throw`, que só existe no assembly de testes e serve para testar o tratamento de exceções.
- `Security/SecurityHeadersTests`: cabeçalhos em 200, 404 e 500; HSTS; HTTP → HTTPS; ausência do cabeçalho Server.
- `Security/ErrorHandlingTests`: 500 sem vazar mensagem nem stack trace; 404 em ProblemDetails.
- `Security/CorsTests`: origem permitida, origens maliciosas e configurações inválidas que impedem a API de subir.
- `Security/RateLimitingTests`: 429 + Retry-After. Usa uma fábrica própria porque o limitador guarda estado entre requisições.
- `HealthTests`: 200 com banco; 503 `Unhealthy` com banco inacessível; API não sobe sem connection string.

### Volumo.Architecture.Tests
- O Domain não depende de Api, Infrastructure, Shared, Client, ASP.NET Core nem EF Core.
- O Shared não depende do servidor, porque roda no navegador.
- A Infrastructure não depende da Api.

### Convenções de teste
- xUnit v3 (4.x): sempre passar `TestContext.Current.CancellationToken` nas chamadas assíncronas (o analisador exige, e aviso vira erro).
- Nome do teste: frase em português com sublinhados (`Excecao_vira_problem_details_sem_vazar_detalhes_internos`).
- `IClassFixture<VolumoApiFactory>` para compartilhar a API entre os testes de uma classe.
- Banco real (LocalDB) nos testes de integração. **Não usar o EF InMemory.**
- Testes de persistência: gravar com um contexto e ler com **outro** (`await using` separados), para não testar o cache do EF.
- O analisador de segurança vale nos testes: nada de `System.Random` (CA5394); usar `RandomNumberGenerator`.

### Pacotes (versões em `Directory.Packages.props`)
ASP.NET Core / EF Core / Identity 10.0.13 · Serilog.AspNetCore 10.0.0 · xunit.v3 4.0.2 · xunit.runner.visualstudio 4.0.1 · Microsoft.NET.Test.Sdk 18.11.0 · Shouldly 4.3.0 · NSubstitute 6.2.0 · Microsoft.AspNetCore.Mvc.Testing 10.0.13 · NetArchTest.Rules 1.3.2 · Microsoft.Testing.Extensions.CodeCoverage 18.12.0

**Transitiva fixada:** `System.Security.Cryptography.Pkcs` 10.0.13. O `Microsoft.Data.SqlClient` 6.1.7 (via EF Core) traz a 9.0.11, com duas vulnerabilidades de DoS publicadas em 13/10/2026 (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7). Remover a fixação quando o EF trouxer versão corrigida (testar apagando a linha e rodando `dotnet restore`).

---

## 6. Pendências e lembretes

### Para o deploy no MonsterASP.NET (configurar no painel ou em variáveis de ambiente, nunca no repositório)
- `https_port=443`: sem isso não há redirecionamento para HTTPS.
- `Cors__AllowedOrigins__0=https://app.volumo.com.br` (ou o domínio definitivo).
- `AllowedHosts`: o domínio da API (hoje está `*`).
- Connection string e chave do JWT: confirmar se o painel aceita variáveis de ambiente.
- Migrations em produção via `dotnet ef migrations script --idempotent`, nunca aplicadas automaticamente na inicialização. Assim o usuário do banco da aplicação não precisa de permissão para alterar tabelas.
- Se houver Cloudflare ou outro proxy na frente, ajustar o IP do rate limiting (Forwarded Headers), senão todos os usuários caem no mesmo limite.

### Para a fase 3 (autenticação)
- Ligar a **FallbackPolicy** de autorização (toda rota exige login, exceto as marcadas com `[AllowAnonymous]`). Ficou de fora agora porque ainda não há esquema de autenticação.
- Aplicar `[EnableRateLimiting(RateLimitPolicies.Login)]` e `RateLimitPolicies.Cadastro` nos endpoints.
- Criar o projeto `tests/Volumo.E2E.Tests` com a primeira jornada (cadastro → confirmação → login).
- Cadastro cria `Usuario` + `PerfilPersonal`/`PerfilAluno` **na mesma transação** (o Id do perfil é o do usuário).
- Convite: na aceitação, conferir também se o e-mail da conta é o do convite (hoje o domínio só valida o token).
- Roles `Aluno`/`Personal`/`Admin` em `AspNetRoles` (as tabelas do Identity já existem na migration Inicial).

### Para a fase 4/5
- Filtros globais do EF por dono e mapeamento de `RegraDeNegocioException` → 400/422 em ProblemDetails.
- Violação do índice `UX_VinculosPersonalAluno_AlunoAtivo` → mensagem amigável ("este aluno já tem um personal").
- Seed do catálogo de exercícios.
- Limite de alunos por plano (Grátis: 3) ao aceitar convite.
- Login (fase 3): regras de conta inativa/suspensa da seção "Decisões de produto sobre contas", item 2.
- Aceite de convite (fase 3/5) via `AceiteDeConvite.Aceitar`.
- Inadimplência (fase 6): item 1b da mesma seção.
- Endpoint de exclusão de conta (fase 6) chama `ExclusaoDeConta`, com reautenticação antes.

### Outras
- **Atualizar SDK/runtime para 10.0.13** (a máquina tem runtime 10.0.12; os pacotes já estão em 10.0.13) e ajustar `global.json` se o SDK mudar de feature band.
- GUID v7 no SQL Server: o SQL Server ordena `uniqueidentifier` pelos últimos bytes, então v7 não é sequencial para o índice clusterizado. Irrelevante no MVP; se as tabelas de registros crescerem muito, avaliar PK não clusterizada.
- CI no GitHub: usar runner **Windows**, que tem LocalDB, ou parametrizar a connection string.
- Client: transformar em PWA (manifest + service worker), trabalho do front.
- Commits: feitos pelo usuário (o Claude não faz commits; regra no CLAUDE.md).

---

## 7. Próximo passo

**Fase 3: autenticação** (ver tabela da seção 2 e lembretes acima)
1. Identity com `Usuario` já existente: senha forte, bloqueio após falhas, confirmação de e-mail obrigatória.
2. JWT de 15 min + refresh token rotativo (hash no banco, família de tokens revogada em caso de reuso, cookie HttpOnly/Secure/SameSite=Strict).
3. FallbackPolicy exigindo login; rate limiting nos endpoints de login/cadastro.
4. 2FA obrigatório para personal; Turnstile no cadastro.
5. Fluxo de convite de aluno usando `ConviteAluno`.
6. Testes: matriz 401/403/404 e primeira jornada E2E.
