# Contexto do backend: onde paramos

Última atualização: 25/10/2026 (presets de séries, treino montado pelo aluno, personal que treina; ver seção 7) · **Beta publicado em 21/10/2026:** API em https://apivolumo.runasp.net e app em https://appvolumo.runasp.net (MonsterASP, variáveis de ambiente no painel, e-mail pelo Brevo, banco com o script idempotente), testado de ponta a ponta pelo usuário · Branch: `Criação-Back-End` · No working tree, sem commit (o usuário commita): front etapa 2, QR code do 2FA e fase 5.3 (painel do personal, com a migration `ConfiguracaoDeEstagnacao`, já aplicada no LocalDB). Testes: 284 passando.

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
| 3. Autenticação | **3.1** Identity, cadastro de personal, confirmação de e-mail, bloqueio após falhas, JWT de 15 min, refresh token rotativo, logout, login obrigatório por padrão · **3.2** 2FA obrigatório para personal, recuperação de senha, Turnstile · **3.3** convite e cadastro de aluno, reativação de conta, suspensão | ✅ Feito (16/10) |
| 4. Autorização contra IDOR | Roles e policies (já feitas na fase 3), autorização por recurso, filtros globais do EF por dono, 404 para recurso alheio | ✅ Feito (16/10) |
| 5. Funcionalidades do MVP | **5.1** catálogo de exercícios, templates, treinos, atribuição ao aluno, alunos e encerramento de vínculo · **5.2** registro de sessão com sincronização offline e idempotência · **5.3** painel do personal (último treino, sumido há mais de 5 dias, estagnado) | 5.1 ✅ · 5.2 ✅ (16/10) · 5.3 ✅ (20/10) |
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

**2. Login de contas inativas ou suspensas** — implementado na fase 3.3 (ver "Contas, convites e suspensão")
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
│   │   ├── Auth/                      → AuthSetup (Identity + JWT + política padrão), JwtSettings, FrontSettings,
│   │   │                                AccessTokenIssuer, RefreshTokenService, RefreshTokenCookie, AccountEmails,
│   │   │                                PortugueseIdentityErrorDescriber
│   │   ├── Errors/BusinessRuleExceptionHandler.cs → RegraDeNegocioException vira 400 com a mensagem do domínio
│   │   ├── Controllers/               → AuthController (/auth), DoisFatoresController (/auth/2fa), ContaController (/conta),
│   │   │                                ConvitesController (/convites), AdminController (/admin/usuarios),
│   │   │                                ExerciciosController, TemplatesController, TreinosController, AlunosController
│   │   ├── Treinos/PlanMapper.cs      → contratos ↔ domínio (treinos/templates), valida exercícios visíveis
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── Volumo.Client           → Blazor WebAssembly PWA (ver seção "Front")
│   ├── Volumo.Shared           → DTOs da API com DataAnnotations: Autenticacao/, Convites/, Admin/, Treinos/, Alunos/
│   ├── Volumo.Domain           → Comum/ (Entidade, Guarda, RegraDeNegocioException), Usuarios/, Vinculos/,
│   │                             Exercicios/, Treinos/, Sessoes/
│   └── Volumo.Infrastructure
│       ├── DependencyInjection.cs        → AddVolumoInfrastructure(connectionString)
│       ├── Identity/                     → Usuario, TokenDeAtualizacao (refresh token)
│       ├── Email/                        → IEnvioDeEmail, EmailEmArquivo (só desenvolvimento)
│       ├── Contas/ExclusaoDeConta.cs     → anonimização da conta (LGPD)
│       └── Persistencia/
│           ├── VolumoDbContext.cs        → IdentityDbContext + IDataProtectionKeyContext
│           ├── VolumoDbContextFactory.cs → usada só pelo dotnet ef
│           ├── Configuracoes/            → IEntityTypeConfiguration por entidade (+ seed dos papéis)
│           └── Migrations/               → Inicial, InativacaoEAnonimizacao, Autenticacao, DoisFatores, Suspensao,
│                                            CatalogoDeExercicios
└── tests/
    ├── Directory.Build.props   → pacotes e usings comuns dos testes (xUnit v3, Shouldly, cobertura)
    ├── Volumo.Domain.Tests     → regras das entidades (sem banco)
    ├── Volumo.Api.Tests        → integração (WebApplicationFactory + LocalDB) e jornadas completas (Jornadas/)
    └── Volumo.Architecture.Tests → regras de dependência entre camadas (NetArchTest)
```

### Autenticação (fase 3)

| Rota | Acesso | O que faz |
|---|---|---|
| `POST /auth/cadastro/personal` | anônimo, limite `cadastro` | Cria conta + papel Personal + perfil numa transação e envia o link de confirmação. **Sempre 202**, inclusive para e-mail já cadastrado (o dono recebe um aviso); senha fraca → 400; CREF inválido → 400; CREF em uso → 409 |
| `POST /auth/confirmar-email` | anônimo | `{usuarioId, token}` do link → 204; link inválido → 400 |
| `POST /auth/reenviar-confirmacao` | anônimo, limite `cadastro` | Sempre 202 |
| `POST /auth/login` | anônimo, limite `login` | 200 `{tokenDeAcesso, expiraEm}` + cookie do refresh token; e-mail inexistente e senha errada → **mesmo 401**; e-mail não confirmado (senha certa) → 403 |
| `POST /auth/refresh` | cookie | Rotaciona o refresh token e devolve novo token de acesso; falha → 401 e apaga o cookie |
| `POST /auth/logout` | cookie | Revoga a família do token (só aquele dispositivo) e apaga o cookie → 204 |
| `POST /auth/login/2fa` | anônimo, limite `login` | `{desafio, codigo}`: segunda etapa com o código TOTP → sessão com 2FA |
| `POST /auth/login/recuperacao` | anônimo, limite `login` | `{desafio, codigoDeRecuperacao}`: alternativa sem o celular; cada código vale uma vez |
| `POST /auth/2fa/iniciar` | login | Gera a chave do autenticador (`chave` em grupos de 4 + `uriOtpauth` para QR code); 409 se já ativo |
| `POST /auth/2fa/ativar` | login | `{codigo}` → ativa, devolve 10 códigos de recuperação **e troca a sessão** por uma com 2FA (cookie novo; o antigo é revogado) |
| `POST /auth/2fa/codigos-de-recuperacao` | sessão com 2FA | Gera 10 códigos novos; os antigos deixam de valer |
| `POST /auth/esqueci-senha` | anônimo, limite `cadastro`, Turnstile | Sempre 202; e-mail com link de 1 hora |
| `POST /auth/redefinir-senha` | anônimo, limite `login` | `{usuarioId, token, novaSenha}` → 204: troca a senha, encerra **todas** as sessões, desbloqueia, confirma o e-mail se faltava e avisa por e-mail. O 2FA continua valendo |
| `GET /conta` | login | Id, e-mail, papéis, `doisFatoresAtivo`, `precisaConfigurarDoisFatores` (personal sem 2FA), `contaInativa` |
| `POST /conta/inativar` | login | `{senha}` → pausa a conta (perfis inativados, nada apagado) e encerra todas as sessões |
| `POST /conta/reativar` | login | Reativa os perfis e devolve um token de acesso novo (mantém o 2FA da sessão) |
| `POST /auth/cadastro/aluno` | anônimo, limite `cadastro` | `{tokenConvite, nome, senha}` → conta (e-mail já confirmado) + papel Aluno + perfil + vínculo numa transação, e já abre a sessão (200). Convite inválido/usado/vencido ou personal inativo → 400; e-mail já tem conta → 409 |
| `POST /convites` | `Personal` (com 2FA) | `{email}` → 201; envia o link por e-mail; convite pendente para o mesmo e-mail é revogado; quem já é aluno do personal → 409 |
| `GET /convites` | `Personal` | Convites do próprio personal (até 200), com situação Pendente/Aceito/Revogado/Expirado |
| `POST /convites/{id}/revogar` | `Personal` | 204; convite de outro personal → **404** |
| `POST /convites/consultar` | anônimo (com o token) | `{token}` → nome do personal, e-mail, validade e `contaExistente` |
| `POST /convites/aceitar` | `Aluno` | `{token}`: aluno que já tem conta. Convite para outro e-mail → 403; personal atual ativo → 400 ("encerre o vínculo"); personal atual inativo → troca automática |
| `POST /admin/usuarios/{id}/suspender` | `Admin` (com 2FA) | `{motivo}` → suspende, troca o `SecurityStamp` e encerra todas as sessões; a si mesmo → 400; inexistente → 404 |
| `POST /admin/usuarios/{id}/remover-suspensao` | `Admin` | 204 |

### Proteção contra IDOR (fase 4)

- **Filtro de dono** (`FiltrosDeConsulta.Dono`, filtro nomeado do EF 10, separado do `Ativos`) em `VolumoDbContext.ConfigurarFiltrosDeDono`. Lê o usuário de `IContextoDeAcesso` (na API, `HttpAccessContext`, a partir do token) a cada consulta:

  | Entidade | Personal vê | Aluno vê |
  |---|---|---|
  | `PerfilPersonal` | o próprio | os personais com quem tem ou teve vínculo |
  | `PerfilAluno` | os alunos com vínculo **ativo** | o próprio |
  | `VinculoPersonalAluno`, `ConviteAluno` | os seus | os seus |
  | `Exercicio` | catálogo + os próprios | catálogo + os dos personais com quem tem ou teve vínculo |
  | `TemplateTreino` | os próprios | nenhum |
  | `Treino`, `SessaoTreino`, `RegistroSerie` | os dos alunos com vínculo **ativo** | os próprios |

- **2FA do personal também no filtro** (correção de 25/10/2026): as rotas `ContaAtiva` (ver treino, evolução, exportar, sessão) não exigiam 2FA, então um personal que entrou sem 2FA lia dados dos alunos por elas. Agora `IContextoDeAcesso.ComDoisFatores` (claim `amr` = `mfa`) entra em todo ramo "personal com vínculo ativo" dos filtros (perfil do aluno, vínculos, treinos, sessões, séries, cardio, anotações). Sem 2FA, o personal enxerga só o que é dele (modelos, presets, exercícios, convites e os próprios treinos), em qualquer rota atual ou futura; o treino do aluno responde 404. Aluno não depende disso. Testes: `FiltrosDeDonoTests` (personal e aluno sem 2FA) e `TreinosPropriosTests` (rotas de qualquer conta com e sem 2FA). `BancoDeTestes.NovoContexto(id, comDoisFatores: true)` por padrão.
- **Sem usuário autenticado não há filtro de dono** (rotas anônimas de `/auth`, tarefas do sistema, testes de persistência com `BancoDeTestes.NovoContexto()`). Por isso as rotas anônimas são poucas e aprovadas por teste.
- **Desligar o filtro é sempre explícito** (`IgnoreQueryFilters([FiltrosDeConsulta.Dono])`) e só em: convite lido pelo token (é de outro personal), checagem de CREF duplicado, status da própria conta (`SessionIssuer.IsInactiveAsync`) e `ExclusaoDeConta`.
- **Padrão das rotas:** recurso invisível → 404; visível mas sem permissão de alterar (catálogo, treino de outro prescritor) → 403.
- **Teste de arquitetura** (`AuthorizationRulesTests`): toda ação de controller declara `[Authorize]` (com ou sem política) ou `[AllowAnonymous]`; anônimo só em `AuthController` e `ConvitesController.Preview`.
- Testes: `Persistencia/FiltrosDeDonoTests` (cenário com dois personais, alunos ativos e ex-aluno, consultando como cada um) + matrizes 401/403/404 por rota.

### Sessões de treino e sincronização offline (fase 5.2)

| Rota | Acesso | O que faz |
|---|---|---|
| `POST /sessoes/sincronizar` | `Aluno` | Lote de até 20 sessões `{id, treinoId, iniciadaEm, finalizadaEm, series[{id, exercicioId, repeticoes, cargaKg, rir, realizadaEm}]}`. Sempre 200 com o resultado **por sessão**: `Sincronizada` (gravada ou já estava) ou `Recusada` + motivo (o app para de reenviar e avisa) |
| `GET /sessoes` | `Aluno` | Últimas 50 sessões: treino, início/fim, nº de séries, volume (reps × carga) |
| `GET /sessoes/{id}` | `ContaAtiva` | Detalhe com as séries e o nome dos exercícios (aluno dono ou personal com vínculo ativo) |
| `GET /alunos/{id}/sessoes` | `Personal` | Histórico do aluno com vínculo ativo; outro → 404 |

- `SessionSynchronizer`: cada sessão é independente (uma recusada não derruba as outras) e gravada no próprio `SaveChanges`.
  - Recusa: horário mais de 5 min no futuro; Id de sessão que já existe e é de outro aluno (sem dizer de quem); treino que não é do aluno; exercício que ele não enxerga; regras do domínio (Id não v7, série antes do início, sessão finalizada...).
  - Treino arquivado e exercício inativado depois de começar offline continuam aceitos.
  - Conflito de gravação (dois aparelhos mandando a mesma sessão ao mesmo tempo): limpa e tenta de novo uma vez pelo caminho idempotente.
- Testes: `Sessoes/SincronizacaoDeSessoesTests` (reenvio não duplica, sessão chegando aos poucos, lote misto, dados de outra pessoa, matriz 401/403/404, lote acima de 20 → 400).

### Painel do personal (fase 5.3, 20/10/2026)

| Rota | Acesso | O que faz |
|---|---|---|
| `GET /painel` | `Personal` | Alunos com vínculo ativo: situação (`EmDia`, `Sumido` = mais de 5 dias sem treinar, `NuncaTreinou`), dias sem treinar, último treino, treinos nos últimos 7 dias, critério de estagnação do aluno e exercícios estagnados |
| `PUT /alunos/{id}/estagnacao` | `Personal` | `{criterio: Tempo/Sessoes, limite}` → 204; fora dos limites → 400; aluno alheio ou ex-aluno → 404 |

- **Estagnação configurável por aluno** (decisão do usuário): o personal escolhe **dias** (7–90) ou **sessões** (2–10) sem subir peso. **Padrão: 15 dias.** Guardado no vínculo (`VinculoPersonalAluno.CriterioDeEstagnacao`/`LimiteDeEstagnacao`, migration `ConfiguracaoDeEstagnacao`), porque é a visão daquele personal sobre aquele aluno.
- **Regra** (`Domain/Sessoes/AnaliseDeEstagnacao`): "subir peso" = sessão cuja maior carga supera a de todas as anteriores do exercício. Por tempo: último aumento há N dias ou mais, com o exercício feito nos últimos N dias e ao menos uma sessão depois do aumento. Por sessões: N sessões ou mais desde o último aumento. Peso corporal (carga 0) e exercício com uma sessão só não entram.
- `Api/Painel/PainelQueries`: últimas sessões sem limite de data; estagnação sobre as sessões finalizadas dos últimos 120 dias (maior carga por sessão e exercício agregada no banco).
- Front: `Pages/Personal/Painel.razor` (indicadores, "Precisam de atenção", "Estagnados", "Todos os alunos") vira a página inicial do personal; `Pages/Personal/Aluno.razor` ganhou "Alerta de estagnação" (Dias/Sessões, limite, "Voltar ao padrão").
- Testes: `Domain.Tests/Sessoes/AnaliseDeEstagnacaoTests`, casos novos em `VinculoPersonalAlunoTests` e `Api.Tests/Painel/PainelTests` (situações, troca de critério, visibilidade e matriz 401/403/404/400). Verificado no navegador (computador e celular) em 20/10.

### Exercícios, templates e treinos (fase 5.1)

| Rota | Acesso | O que faz |
|---|---|---|
| `GET /exercicios?busca=&grupo=` | `ContaAtiva` | Catálogo + exercícios visíveis (até 500), por nome |
| `POST /exercicios` | `Personal` | `{nome, grupoMuscular}` → exercício personalizado (201) |
| `PUT /exercicios/{id}` · `POST /exercicios/{id}/inativar` | `Personal` | Só os próprios; catálogo → 403; de outro → 404 |
| `GET /templates` · `GET /templates/{id}` | `Personal` | Próprios (detalhe com nomes dos exercícios, ordem e séries) |
| `POST /templates` · `PUT /templates/{id}` | `Personal` | Plano inteiro `{nome, itens[{exercicioId, observacao, series[...]}]}`; a gravação **substitui** tudo. Exercício inexistente/invisível → 400; série inválida → 400 com a mensagem do domínio |
| `POST /templates/{id}/inativar` | `Personal` | Some da lista e não pode mais ser atribuído |
| `POST /templates/{id}/atribuir` | `Personal` | `{alunoId}` (vínculo ativo) → treino independente (201) |
| `GET /alunos` | `Personal` | Alunos com vínculo ativo |
| `GET /alunos/{id}/treinos` · `POST /alunos/{id}/treinos` | `Personal` | Treinos do aluno / montar treino direto, sem template |
| `POST /alunos/{id}/encerrar-vinculo` | `Personal` | Encerra; o personal deixa de ver os dados do aluno, que mantém tudo |
| `GET /treinos` | `Aluno` | Treinos ativos do aluno (tela principal) |
| `GET /treinos/{id}` | `ContaAtiva` | Detalhe (aluno dono ou personal com vínculo ativo) |
| `PUT /treinos/{id}` · `POST /treinos/{id}/inativar` | `Personal` | Só quem prescreveu, com vínculo ativo; outro → 404; visível mas de outro prescritor → 403 |
| `POST /conta/vinculo/encerrar` | `Aluno` | O aluno encerra o vínculo com o personal atual |

- **Catálogo inicial:** 42 exercícios em `CatalogoInicial` (Infrastructure), semeados pela migration `CatalogoDeExercicios` com Ids fixos. Só acrescentar no fim; para tirar, inativar por migration.
- `PlanoDeTreino.SubstituirItens` (domínio) valida tudo antes de trocar: se um item for inválido, nada muda.
- Enums nos contratos (`GrupoMuscular`, `TipoDeSerie`) trafegam como texto (`JsonStringEnumConverter` no próprio enum do Shared).
- Exercício inativado depois continua aparecendo pelo nome nos treinos que o usam; se ficar invisível, aparece como "Exercício indisponível".

**Contas, convites e suspensão (fase 3.3):**
- **Conta inativa:** login permitido, mas o token leva a claim `conta_inativa`; as políticas `Personal`, `Aluno` e `Admin` recusam essa claim. Só `/conta` e `/conta/reativar` funcionam. Conta inativa = tem perfil e todos os perfis estão inativos (calculado a cada emissão de token, também na renovação).
- **Suspensão:** `Usuario.SuspensoEm` + `MotivoDaSuspensao` (até 500 caracteres; apagado na anonimização). Login com a senha **certa** → 403 "conta suspensa"; senha errada → o 401 genérico de sempre. Renovação, segunda etapa do 2FA e `/conta` também recusam. O access token já emitido vale até vencer (até 15 min).
- **Não existe rota para criar Admin:** dar o papel direto no banco (`AspNetUserRoles` com o papel `Admin`, Id `0199ecb0-0000-7000-8000-000000000003`) e a pessoa ativa o 2FA pela API.
- Os links do e-mail apontam para o front: `/confirmar-email`, `/redefinir-senha` e `/aceitar-convite?token=...`.

**2FA (fase 3.2):**
- **Obrigatório para personal** pela política `Policies.Personal` (papel Personal **e** claim `amr` com `mfa`). Personal sem 2FA consegue entrar (amr só `pwd`), mas toda rota de personal responde 403; o `/conta` avisa `precisaConfigurarDoisFatores` para o app levar à configuração. Outras políticas: `Aluno`, `Admin` (com 2FA) e `MultiFactor`.
- **Login com 2FA ativo:** senha certa devolve só `desafio2Fa` (sem token, sem cookie). O desafio é cifrado pelo Data Protection, vale 5 min e é amarrado ao `SecurityStamp` (troca de senha ou de 2FA invalida desafios pendentes). A contagem de erros só zera depois do código certo: 5 códigos errados bloqueiam a conta.
- **Sessão lembra o 2FA:** `TokenDeAtualizacao.ComDoisFatores`; a renovação mantém `mfa` no token novo.
- Códigos de recuperação no formato do Identity (`XXXXX-XXXXX`); a API aceita digitados sem hífen, com espaços ou em minúsculas.
- Aplicativos compatíveis: Google Authenticator, Microsoft Authenticator, Authy etc. (TOTP padrão, 6 dígitos, 30 s).
- Limitações conhecidas: o Identity guarda a chave do autenticador e os códigos de recuperação **em texto** na `AspNetUserTokens` (proteger o banco: backup cifrado/TDE); um mesmo código TOTP pode ser reaproveitado dentro da janela de ~30 s.

**Recuperação de senha (fase 3.2):** token próprio (`PasswordResetTokenProvider`) com validade de **1 hora**, separado do link de confirmação de e-mail; vale uma vez (a troca de senha muda o `SecurityStamp`).

**Turnstile (fase 3.2):** atributo `[RequireHumanVerification]` nas ações cujo corpo implementa `IComVerificacaoHumana` (campo `tokenTurnstile`): cadastro de personal, reenvio de confirmação e esqueci-senha (tudo que dispara e-mail). O servidor valida em `challenges.cloudflare.com/turnstile/v0/siteverify` com o IP do cliente; rede fora ou resposta estranha → recusa (falha fechada). Em dev, chave pública de teste da Cloudflare que sempre aprova (secret `1x0000000000000000000000000000000AA`, site key `1x00000000000000000000AA`). Login ainda não usa Turnstile (tem limite de tentativas e bloqueio); avaliar exigir depois de algumas falhas.

Decisões e detalhes:
- **Política padrão:** toda rota exige login; rotas públicas usam `[AllowAnonymous]` (`/auth/*`, `/health`, OpenAPI em dev). Vale também para rotas inexistentes: anônimo recebe **401** (não revela quais rotas existem); logado recebe 404.
- **Senha:** mínimo 10 caracteres e 4 diferentes, sem regras de composição (NIST SP 800-63B); máximo 128. Bloqueio de 15 min após 5 erros. Mensagens do Identity em português.
- **Token de acesso:** JWT HS256, 15 min, claims `sub`, `role`, `jti`, `amr=["pwd"]` (sem dados pessoais). Validação com emissor, audiência, algoritmo fixo (recusa `alg: none`) e tolerância de 30 s. `MapInboundClaims = false`.
- **Refresh token:** 32 bytes aleatórios, só o SHA-256 no banco (`TokensDeAtualizacao`), 30 dias renovados a cada uso. Cada login abre uma família; cada renovação marca o token como usado (`ExecuteUpdate` condicional: de duas renovações simultâneas, só uma vence). **Token já usado ou revogado → família inteira revogada.** Sem período de tolerância: o front precisa serializar as renovações entre abas.
- **Cookie:** `__Secure-volumo-rt`, HttpOnly, Secure, SameSite=Strict, `Path=/auth`, sem `Domain`.
- **Bloqueio temporário não derruba sessões abertas** (senão um atacante deslogaria alguém só errando a senha); conta excluída derruba (`ExclusaoDeConta` revoga todos os refresh tokens).
- **Login com e-mail inexistente** verifica a senha contra um hash fictício, para levar o mesmo tempo que um login real.
- **Data Protection:** chaves na tabela `DataProtectionKeys` (tokens de confirmação de e-mail sobrevivem a reinícios). As chaves ficam sem criptografia em repouso; avaliar proteção extra (certificado) no deploy.
- **E-mail:** `Email:Provider=File` grava os e-mails em `%TEMP%/volumo-emails` (só desenvolvimento; proibido em produção). **Provedor de produção ainda não escolhido.**
- **Jornadas E2E** ficaram em `Volumo.Api.Tests/Jornadas/` (mesma fábrica e banco), em vez de um projeto separado. Um projeto `Volumo.E2E.Tests` faz sentido quando houver ambiente publicado ou Playwright.

### Banco de dados
- Development: LocalDB, banco `Volumo` (connection string em `appsettings.Development.json`; não é segredo, usa autenticação do Windows).
- Produção: variável de ambiente `ConnectionStrings__Volumo`. Sem connection string a API **não sobe**.
- Migrations (rodar na raiz):
  - nova: `dotnet ef migrations add <Nome> -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure -o Persistencia/Migrations`
  - aplicar no LocalDB: `dotnet ef database update -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure`
  - script para produção: `dotnet ef migrations script --idempotent -p src/Volumo.Infrastructure -s src/Volumo.Infrastructure -o artifacts/migracao.sql` (pasta ignorada pelo Git). Acrescentar `SET QUOTED_IDENTIFIER ON` + `GO` no topo: o sqlcmd vem com essa opção desligada e os índices filtrados falham sem ela (SSMS já liga). Validado em 22/10/2026: roda duas vezes num banco vazio sem erro (12 migrations, 49 exercícios) e num banco já migrado
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
8. `UseAuthentication` (JWT) → `UseAuthorization` (política padrão: login obrigatório) → `/health` (público) e Controllers.

`/health` inclui o banco (`AddDbContextCheck`): 200 `Healthy` ou 503 `Unhealthy`, sem detalhes.

Outros ajustes:
- Kestrel sem o cabeçalho `Server`.
- Limite de 1 MB no corpo das requisições (Kestrel e IIS).
- OpenAPI só em Development.

### Configuração (`appsettings.json`)
- `Serilog:MinimumLevel`
- `Cors:AllowedOrigins`: vazio em produção. Em Development: `https://localhost:7184` e `http://localhost:5140` (portas do Client).
- `RateLimiting:Global|Login|Cadastro` (`PermitLimit`, `WindowSeconds`)
- `Jwt:Issuer|Audience|SigningKey|AccessTokenMinutes`: chave em Base64 com 32+ bytes, **só em User Secrets/variável de ambiente**. Configuração inválida impede a API de subir.
- `Front:BaseUrl`: endereço do Client, usado nos links dos e-mails (HTTPS, sem caminho).
- `Email:Provider` (`File` em dev) e `Email:Folder` (opcional).
- `Turnstile:SecretKey`: obrigatória (a API não sobe sem ela). Em dev, a chave de teste da Cloudflare.
- User Secrets habilitado na API (`UserSecretsId` no csproj). Nesta máquina já existe `Jwt:SigningKey`. Em outra máquina:
  `dotnet user-secrets set "Jwt:SigningKey" "<base64 de 32+ bytes aleatórios>" --project src/Volumo.Api`
- HTTPS local exige o certificado de desenvolvimento: `dotnet dev-certs https --trust` (nesta máquina ele não foi encontrado em 16/10).

### Portas locais
- API: `https://localhost:7010` / `http://localhost:5095`
- Client: `https://localhost:7184` / `http://localhost:5140`

---

## 5. Testes

Rodar tudo: **`dotnet test --solution Volumo.slnx`** (o `--solution` é necessário com o executor novo).

Situação atual: **261 testes, todos passando** (~20 s). Precisa do LocalDB instalado.

- **Cuidado com ordem de GUID em teste:** o SQL Server ordena `uniqueidentifier` pelos últimos bytes; o .NET, pelos primeiros. Não compare lista ordenada no banco com lista ordenada no .NET: use `ShouldBe(..., ignoreOrder: true)`.

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
  - Configura JWT com chave só de teste, `Front:BaseUrl`, limites altos de login/cadastro e troca o envio de e-mail por `Support/CaixaDeEmails` (`factory.Emails`), de onde os testes tiram os links.
  - `CreateHttpsClient(handleCookies: false)` para enviar o cookie do refresh token manualmente.
- `Support/ContasDeTeste`: cadastrar, confirmar, entrar, renovar e sair pela API, como o app faria.
- `Auth/`: `CadastroTests`, `LoginTests` (resposta igual para e-mail inexistente e senha errada, bloqueio, cookie protegido, limite de tentativas), `SessaoTests` (rotação, reuso derruba a família, logout por dispositivo, token vencido, exclusão de conta), `TokenDeAcessoTests` (sem token, adulterado, outra chave, vencido, `alg: none`), `DoisFatoresTests` (personal sem 2FA barrado, ativação troca a sessão, desafio, código errado/adulterado, bloqueio, códigos de recuperação), `RecuperacaoDeSenhaTests` (sempre 202, link de uso único, sessões derrubadas, desbloqueio, 2FA continua valendo), `TurnstileTests` (formulários barrados; verificador testado com `HttpMessageHandler` falso, sem rede), `ContaInativaESuspensaTests` (inativar com senha, conta inativa só reativa, suspensão e matriz do admin).
- `Convites/`: `ConvitesTests` (envio, listagem só do dono, reenvio invalida o link anterior, revogação, **matriz 401/403/404**, consulta, hash no banco) e `AceiteDeConviteTests` (cadastro pelo convite, uso único, vencido, senha fraca não consome, e-mail com conta → 409, personal inativo, troca de personal, personal ativo bloqueia, e-mail diferente → 403).
- `Treinos/`: `ExerciciosTests`, `TemplatesTests`, `TreinosTests` (com matrizes 401/403/404, atribuição, encerramento de vínculo pelos dois lados).
- `Persistencia/FiltrosDeDonoTests`: filtros de dono consultando como cada usuário (`BancoDeTestes.NovoContexto(usuarioId)`).
- Atalhos em `ContasDeTeste`: `ExercicioDoCatalogoAsync`, `Plano(...)`, `IdCriadoAsync`.
- `Support/ContasDeTeste` também cria personal com 2FA, aluno via convite e admin (`TornarAdminAsync`).
- `Jornadas/JornadaDoPersonalTests`: cadastro → confirmação → login → rota protegida → renovação → logout; e personal configurando o 2FA e entrando com o código.
- `Support/Totp`: gera códigos TOTP como um aplicativo autenticador. `Support/VerificacaoHumanaFalsa`: substitui o Turnstile (`TokenAprovado`/`TokenReprovado`). `Support/TestPoliciesController`: rota `/__test/politicas/personal` para testar a política de personal.
- `Support/TestOnlyController`: rota `/__test/throw` (`[AllowAnonymous]`), que só existe no assembly de testes e serve para testar o tratamento de exceções.
- `Security/SecurityHeadersTests`: cabeçalhos em 200, 404 e 500; HSTS; HTTP → HTTPS; ausência do cabeçalho Server.
- `Security/ErrorHandlingTests`: 500 sem vazar mensagem nem stack trace; 404 em ProblemDetails (logado) e 401 para anônimo em rota inexistente.
- `Security/CorsTests`: origem permitida, origens maliciosas e configurações inválidas que impedem a API de subir.
- `Security/RateLimitingTests`: 429 + Retry-After. Usa uma fábrica própria porque o limitador guarda estado entre requisições.
- `HealthTests`: 200 com banco; 503 `Unhealthy` com banco inacessível; API não sobe sem connection string.

### Volumo.Architecture.Tests
- O Domain não depende de Api, Infrastructure, Shared, Client, ASP.NET Core nem EF Core.
- O Shared não depende do servidor, porque roda no navegador.
- A Infrastructure não depende da Api.
- `AuthorizationRulesTests`: toda ação declara quem pode acessar; rota anônima só onde foi aprovada.

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
- Variáveis obrigatórias da autenticação: `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey` (gerar uma chave nova, nunca reaproveitar a de dev), `Front__BaseUrl`, `Email__Provider=Brevo` + `Email__Brevo__ApiKey` + `Email__Brevo__SenderEmail` (remetente verificado no Brevo; `Email__Brevo__SenderName` opcional, padrão "Volumo"; `File` é recusado em produção), `Turnstile__SecretKey` (chave real do site no painel da Cloudflare).
- Migrations em produção via `dotnet ef migrations script --idempotent`, nunca aplicadas automaticamente na inicialização. Assim o usuário do banco da aplicação não precisa de permissão para alterar tabelas.
- Se houver Cloudflare ou outro proxy na frente, ajustar o IP do rate limiting (Forwarded Headers), senão todos os usuários caem no mesmo limite.

### Lembretes da autenticação
- Rotas de personal **precisam** usar `[Authorize(Policy = Policies.Personal)]` (2FA obrigatório e conta ativa dependem disso); de aluno, `Policies.Aluno`. Toda rota nova entra na matriz de testes 401/403/404.
- Provedor de e-mail de produção: **Brevo** (decisão de 21/10/2026), em `Infrastructure/Email/EmailBrevo.cs` pela API HTTP (sem pacote extra), com corpo em texto e HTML. Falha do Brevo vira exceção sem o conteúdo da mensagem (os links têm tokens). Testes em `Api.Tests/Auth/EmailBrevoTests`. Remetente com Gmail funciona no beta, mas pode cair no spam; com domínio próprio, autenticar o domínio (SPF/DKIM/DMARC) no Brevo.
- Sites do beta no MonsterASP (21/10/2026): `https://apivolumo.runasp.net` (API) e `https://appvolumo.runasp.net` (front), HTTPS com Let's Encrypt. O `runasp.net` não está na Public Suffix List, então os dois contam como o mesmo site e o cookie `SameSite=Strict` funciona. Arquivos `.publishSettings` têm a senha de deploy: ficam fora do repositório.
- **Ideia (21/10/2026): login com Google**, depois do beta. Cuidados: personal continua com 2FA do app; aluno novo só entra pelo convite (o Google não cria vínculo); conta existente com o mesmo e-mail só é ligada depois de login com senha; a anonimização já remove logins externos.
- Limpeza periódica de refresh tokens vencidos/revogados (tabela cresce a cada renovação).

### Para a fase 5 em diante
- Consultas de listagem sem paginação (limites fixos de 200–500): paginar quando os volumes crescerem.
- Toda entidade nova com dono precisa de regra em `ConfigurarFiltrosDeDono` e de linha nos `FiltrosDeDonoTests`.
- **Ideias de produto que afetam o modelo de `Exercicio`** (registradas em 16/10/2026, fora do MVP; ver seção 1 do CLAUDE.md):
  - **Vídeo de execução:** URL do vídeo (storage com URL assinada ou plataforma externa) no exercício do catálogo; personal pode ter vídeo próprio nos exercícios personalizados. **Padrão do catálogo (decisão de 20/10/2026, no estilo do Hevy):** vídeos curtos em loop, sem som, todos da mesma fonte e mesmo estilo visual; cada exercício com músculo principal/secundários, equipamento e instruções em passos (ver CLAUDE.md, seção 1). Vídeo do personal fica marcado como dele, fora do padrão do catálogo.
  - **Substituição por aparelho indisponível:** o exercício precisa de **equipamento** (máquina, halteres, barra, polia, peso corporal...), **padrão de movimento** (empurrar horizontal, agachar...) e uma relação de **variações livres** e **similares**. Na sessão, o `RegistroSerie` passa a guardar o exercício executado e o prescrito originalmente, para o personal ver a troca. É a mesma base do "modo academia lotada".
- Limite de alunos por plano (Grátis: 3) ao aceitar convite.
- Inadimplência (fase 6): item 1b da seção "Decisões de produto sobre contas".
- Endpoint de exclusão de conta (fase 6) chama `ExclusaoDeConta`, com reautenticação antes.

### Outras
- **Atualizar SDK/runtime para 10.0.13** (a máquina tem runtime 10.0.12; os pacotes já estão em 10.0.13) e ajustar `global.json` se o SDK mudar de feature band.
- GUID v7 no SQL Server: o SQL Server ordena `uniqueidentifier` pelos últimos bytes, então v7 não é sequencial para o índice clusterizado. Irrelevante no MVP; se as tabelas de registros crescerem muito, avaliar PK não clusterizada.
- CI no GitHub: usar runner **Windows**, que tem LocalDB, ou parametrizar a connection string.
- Commits: feitos pelo usuário (o Claude não faz commits; regra no CLAUDE.md).

---

## 7. Front (Volumo.Client)

Blazor WebAssembly PWA, **sem MudBlazor** (decisão de 16/10/2026): componentes com CSS próprio, seguindo o briefing de design do usuário (nada de estética "gerada por IA": sem gradientes, emojis, glassmorphism, cards fofos, frases de marketing).

**Direção visual:** técnico e sóbrio, cara de ferramenta e não de site.
- Paleta do CLAUDE.md (seção 7) em variáveis CSS (`wwwroot/css/app.css`): fundo `#0F1115`, superfícies `#1C1F26` com borda de 1px, limão `#C6F135` só na ação principal e no número que importa, laranja `#FF6B2C` só para recorde; um vermelho suave só para mensagens de erro.
- Fontes hospedadas no app (`wwwroot/fonts`, licença OFL, @fontsource 5.3.0): **Barlow Condensed** (títulos em caixa-alta, números grandes), **IBM Plex Sans** (texto), **IBM Plex Mono** com algarismos tabulares (cargas, tempos, códigos). Sem Google Fonts: funciona offline e não manda o IP ao Google.
- Celular: uma coluna, barra de navegação inferior, alvos de toque de 48 px (uso com uma mão). Computador: navegação à esquerda (232 px) e área de trabalho; telas de entrada em duas colunas assimétricas (texto à esquerda, formulário à direita, separados por uma linha fina).
- Foco visível em limão para teclado; `prefers-reduced-motion` desliga animações; sem sombras nem cantos muito arredondados (raio de 3 px).

**Estrutura:**
```
src/Volumo.Client
├── Program.cs                 → config, sessão, ApiVolumo, autorização
├── Infra/
│   ├── SessaoDoUsuario.cs     → AuthenticationStateProvider; token só em memória; restaura pelo cookie ao abrir; renova perto de vencer (uma renovação por vez)
│   ├── AnexarToken.cs         → põe o Bearer, inclui o cookie, renova antes de vencer; 401 → encerra a sessão
│   ├── ApiVolumo.cs           → chamadas com Resultado<T> (mensagem do ProblemDetails pronta para mostrar; sem rede → aviso de conexão)
│   └── ConfiguracaoDoFront.cs → Api:BaseUrl e Turnstile:SiteKey (wwwroot/appsettings*.json, público)
├── Layout/                    → EntradaLayout (SectionOutlet "entrada-lado" para o texto da coluna esquerda) e AppLayout
├── Componentes/               → Marca, VerificacaoHumana (Turnstile via js/volumo.js), RedirecionarParaEntrar, ContaPausada
├── Pages/Entrada/             → Entrar (senha → código 2FA → código de recuperação), Cadastro (personal), ConfirmarEmail, EsqueciSenha, RedefinirSenha, AceitarConvite
├── Pages/Conta/               → Conta (dados, sair), DoisFatores (chave + link otpauth, códigos de recuperação)
├── Pages/Personal/Alunos.razor → alunos ativos (link para o aluno) + convidar + convites aguardando
├── Pages/Personal/Aluno.razor  → treinos do aluno (editar/arquivar), mandar modelo, novo treino, últimas sessões
├── Pages/Personal/Modelos.razor, EditarModelo.razor, EditarTreino.razor → modelos e treinos com o construtor
├── Pages/Aluno/MeusTreinos.razor → lista (API ou cópia no aparelho), treino em andamento, últimos treinos
├── Pages/Aluno/Treino.razor   → detalhe do treino + começar/continuar
├── Pages/Aluno/Sessao.razor   → execução: séries com −/+, RIR opcional, descanso automático, finalizar
├── Componentes/ConstrutorDeTreino.razor → monta o plano inteiro (subir/descer, séries, observação) e valida os limites do domínio
├── Componentes/BuscaDeExercicio.razor → busca sem acento e por grupo; cria exercício personalizado na hora
├── Componentes/ExigeDoisFatores.razor, IndicadorDeSincronizacao.razor
├── Infra/ArmazenamentoLocal.cs → IndexedDB via volumo.local (chaves por usuário; nunca token)
├── Infra/TreinosDoAluno.cs     → API com cópia no aparelho; baixa os treinos e a última sessão de cada um para offline
├── Infra/SincronizacaoDeSessoes.cs → fila de sessões finalizadas, lotes de 20, envia ao finalizar/abrir/voltar a internet
├── Infra/Rotulos.cs            → textos em português dos enums, kg, datas, plural, busca sem acento
└── wwwroot/                   → index.html, css/app.css, fonts/, js/volumo.js, manifest.webmanifest, service-worker(.published).js, ícones
```

- **Início (`/`):** personal sem 2FA → `/conta/2fa`; personal → `/alunos`; aluno → `/treinos`; conta pausada → tela de reativação.
- **Login com `?voltar=`** só aceita caminho do próprio app (contra redirecionamento aberto).
- **PWA:** `ServiceWorkerAssetsManifest` no csproj; a versão publicada guarda o app no aparelho (os dados da API não passam pelo cache). Ícones 192/512 (V limão sobre grafite) gerados por script; trocar quando houver logo definitivo.
- **Rodar local:** API (`https://localhost:7010`) e Client (`https://localhost:7184`) juntos (Multiple startup projects no Visual Studio). Precisa de `dotnet dev-certs https --trust`. O CORS da API já libera o Client em dev; o cookie do refresh token funciona porque as portas do mesmo host contam como o mesmo site.
- **Offline do aluno (etapa 2, 20/10/2026):** se o app abre sem rede e sem token, `SessaoDoUsuario` monta um usuário "offline" a partir do id guardado no aparelho (`aluno-offline`, só id) e as telas leem a cópia local. Com a rede de volta, `AnexarToken` renova pelo cookie antes da primeira chamada; se o cookie não vale mais, a sessão é encerrada e a fila fica guardada para o mesmo usuário. Personal continua exigindo rede. Sair apaga as cópias de treino do usuário, mas não a fila (avisa antes se houver envio pendente).
- **Sessão em andamento** gravada no aparelho a cada série (`{usuario}:em-andamento`); descanso por horário-alvo (certo mesmo com a tela apagada), vibração ao terminar e Wake Lock para manter a tela acesa. A próxima vez do treino vem preenchida com a carga e as repetições da última sessão (cópia local ou `GET /sessoes/{id}`).
- **Verificado no navegador (20/10/2026)** com Playwright contra a versão **publicada** (service worker ativo): contas e convite; modelo com exercício do catálogo e um criado na hora, reordenação, treino a partir do modelo, treino do zero, arquivar, editar; aluno no celular executando o treino (descanso termina sozinho), envio online; app reaberto **sem internet** abrindo do cache, treino feito offline indo para a fila e subindo sozinho quando a rede volta; personal vendo as duas sessões. Dados de teste removidos do LocalDB.
- Dica de teste local: o Client publicado lê `appsettings.json` de produção; para testar o service worker localmente, servir a pasta publicada com a configuração de desenvolvimento (e o hash dela atualizado em `service-worker-assets.js`). O limite de cadastro (3 a cada 10 min) zera ao reiniciar a API.
- **QR code do 2FA** (20/10/2026): gerado no próprio aparelho com QRCoder (SVG, fundo branco para leitura), além da chave em texto e do link `otpauth://`. A chave não passa por serviço externo.
- **Ajustes de 21/10/2026 (pedidos do usuário):**
  - **Lembrar este aparelho** no código do 2FA (`SegundaEtapaDoLogin.LembrarAparelho`): a API grava o cookie `__Secure-volumo-dc` (`Api/Auth/TrustedDeviceCookie`, HttpOnly, Secure, SameSite=Strict, `/auth`, 30 dias, Data Protection com Id + SecurityStamp). No login com senha certa e cookie válido, abre a sessão já com 2FA. A senha continua obrigatória; trocar a senha, refazer o 2FA ou suspender a conta (muda o SecurityStamp) esquece todos os aparelhos. Testes em `DoisFatoresTests`.
  - **Mostrar senha** no login (botão Mostrar/Ocultar dentro do campo).
  - **Tela do treino:** a carga sugerida passa a ser a **prescrita pelo personal**; sem prescrição, a da última vez e depois a da série anterior. "Última vez: X kg × Y" aparece como referência. No descanso, o resto da tela escurece (continua clicável) e o relógio fica maior, com barra de progresso (`SessaoEmAndamento.DescansoDesde`).
  - **Cache:** `ApiVolumo` guarda os GET na memória da aba por 5 min (cópia em JSON); qualquer POST/PUT bem-sucedido ou troca de usuário limpa tudo. Para o aluno, a cópia no IndexedDB vale como cache por 15 min (data numa chave `…:em` à parte), então reabrir o app dentro do prazo não chama a API; o download para offline só roda quando a lista veio da API e só baixa a última sessão de um treino quando ela mudou. Treino finalizado entra no histórico do aparelho na hora. Botão "Atualizar" em Treinos e no Painel força a busca. Dados do personal ficam só na memória (nada no disco).
  - **Construtor:** cada exercício recolhe e expande pela seta do cabeçalho (recolhido mostra "3 séries · 8–12 · 20 kg"); ao adicionar um exercício, os outros recolhem; plano aberto para editar começa recolhido; erro de validação abre o exercício com problema. Botão "Recolher/Expandir todos".
  - **Cardio (aba separada no mesmo plano):** domínio `ItemCardio` (exercício do grupo Cardio, 1–180 min, observação de intensidade) e `MomentoDoCardio` (`Antes`, `Depois` padrão, `AlunoEscolhe`) no `PlanoDeTreino`, máximo de 10; `SubstituirCardios` valida tudo antes de mudar; template copia o cardio para o treino. Tabelas `CardiosTemplateTreino` e `CardiosTreino` (migração `Cardio`, também com 7 exercícios de cardio novos no catálogo). Na sessão, `RegistroCardio` (tabela `RegistrosCardio`, idempotente pelo Id do app, filtro de dono igual ao das séries) fica **fora do volume e da estagnação**; `SessaoResumo.MinutosDeCardio` e `SessaoDetalhe.Cardios`. Contratos com campos opcionais (`Cardios`, `MomentoDoCardio`), então app antigo e cópias antigas no aparelho continuam funcionando. Cardio com exercício que não é do grupo Cardio → 400.
  - **Ordem dos treinos do aluno** (pedido de 21/10/2026): `Treino.Ordem` (migração `OrdemDosTreinos`; existentes ficam 0 e desempatam pelo nome). `PUT /alunos/{alunoId}/treinos/ordem` com `OrdemDosTreinos` (cada treino ativo do aluno uma vez, senão 400; outro personal → 404; aluno → 403). Listas do aluno e do personal por `Ordem` e depois nome; treino novo (direto ou de modelo) entra no fim. Front: setas ↑ ↓ na lista de treinos em `Pages/Personal/Aluno.razor`.
  - **Aluno:** com cardio e musculação, a tela do treino mostra "Começar pelo cardio" e "Começar pela musculação", com a recomendação do personal destacada; a execução vira uma sequência de etapas (cardio "C" no mapa), com minutos feitos (−/+ de 5, começando nos prescritos). Pendente: cronômetro próprio para o cardio.
- **Ajustes de 22/10/2026 (pedidos do usuário):**
  - **Descanso:** botão "Descansar" na tela do treino, que inicia o descanso sem registrar série (descanso da próxima série; sem prescrição, 90 s; cardio, 60 s). O relógio virou uma folha presa embaixo, com fundo escuro por cima de tudo (inclusive da navegação): **tocar fora fecha o descanso** (como "Pular") e esse toque não chega a nenhum botão embaixo; o conteúdo fica `inert`. Botões −15 s, +15 s e Pular com a mesma largura; o tempo cresce com a tela (`clamp`); celular deitado põe tempo e botões lado a lado; no computador vira um cartão centralizado na área de trabalho.
  - **Ferramentas do exercício** ("Como fazer", "Anotação", "Descansar") numa linha só, com o mesmo tamanho (`.ferramenta` em `app.css`). O `ComoFazer` usa `display: contents` para entrar na linha, e o conteúdo aberto (instruções, anotação) ocupa a linha inteira abaixo dos botões.
  - **Observação do aluno** (`AnotacaoDoAluno`, tabela `AnotacoesDoAluno`, migration `AnotacoesDoAluno`): uma por treino e exercício (sobrevive à edição do treino pelo personal, que recria os itens), separada da observação do personal, que o aluno não altera. Na sessão, a anotação fica em `SessaoEmAndamento.Anotacoes` (funciona offline) e sobe em `SessaoRegistrada.Anotacoes`; na tela do treino, grava direto em `PUT /treinos/{id}/exercicios/{exercicioId}/observacao-do-aluno` (só o aluno dono; outro → 404, personal → 403, exercício fora do treino → 404). Vale a mais nova por `AtualizadaEm` (sessão offline antiga não sobrescreve). Texto vazio limpa. Sai em `ItemDoPlanoSaida.ObservacaoDoAluno` e `CardioDoPlanoSaida.ObservacaoDoAluno`, e o personal vê no construtor (só leitura). Filtro de dono igual ao das sessões; a exclusão de conta apaga o texto.
  - **Evolução:** `GET /treinos/{id}/evolucao` (`ContaAtiva`, filtro de dono) devolve, para cada exercício nas últimas 30 sessões do treino, por dia: série mais pesada (carga × repetições), repetições totais, volume e séries. Front: `GraficoDeEvolucao` (SVG próprio, sem biblioteca) na tela do treino, com escolha do exercício e da medida (Carga, Repetições ou Volume, uma por vez, num eixo só), toque ou mouse num dia mostra os números dele, recorde de carga em laranja com texto e "Ver como tabela". Cópia no aparelho como o detalhe; finalizar um treino invalida a cópia.
  - **Exportação** (`Api/Exportacao/WorkoutExporter`, ClosedXML e QuestPDF): `GET /treinos/{id}/exportar?formato=xlsx|pdf&fuso=-180` traz o plano com as observações e a evolução; `GET /sessoes/{id}/exportar` traz o treino do dia. `fuso` em minutos (o app manda o do aparelho; padrão Brasília). PDF em papel branco com a faixa grafite e o "VOLUMO" em limão. QuestPDF na licença **Community** (grátis abaixo de US$ 1 milhão de receita anual). Front: `ExportarArquivo` baixa por `ApiVolumo.BaixarAsync` e salva com `volumo.baixarArquivo` (Blob). Precisa de internet.
  - **Resumo geral do treino** (`ResumoDaSessao` + `Infra/ResumoDeSessao`): tempo total com horário de início e fim, exercícios feitos de previstos, séries, repetições, volume e cardio; por exercício, as séries, o volume, a variação de volume em relação à última vez e "Carga nova". Aparece ao finalizar (com exportação liberada depois do envio) e em `/historico/{id}` (link nos "Últimos treinos"). Corrigido: logo depois de finalizar offline, a tela dizia "Enviado" antes de a fila ser lida.
  - Verificado no navegador com Playwright (modo offline do app, 375, 320 e 1280 px): toque fora fecha o descanso sem acionar o botão embaixo; ferramentas alinhadas; gráfico; resumo. PDFs gerados e conferidos. Testes: 341 passando (`Treinos/AcompanhamentoDoAlunoTests`, `AnotacaoDoAlunoTests`, linha nova nos `FiltrosDeDonoTests`).
  - **Foto do exercício no treino:** `ItemDoPlanoSaida.FotoUrl` e `CardioDoPlanoSaida.FotoUrl` (URL de leitura com prazo de 6 h, gerada pelo `PlanMapper` com `MidiaDoExercicio`, como na busca). O componente `FotoDoExercicio` mostra a miniatura ao lado do nome no construtor do personal (musculação e cardio), na tela do treino e, maior, na execução; tocar amplia e tocar de novo fecha. Se a foto não carregar (offline, link vencido na cópia do aparelho), a miniatura some. Teste em `CatalogoDaTiTests`. Sem migration.
  - **Mapa muscular em cada exercício** (pedido de 22/10/2026): no "Como fazer", boneco de frente e de costas com o músculo principal em limão e os secundários em limão apagado, mais uma legenda em texto. Desenhos em `wwwroot/img/corpo/{masculino,feminino}-{frente,costas}.svg`, gerados a partir dos dados do **react-native-body-highlighter 3.2.0 (MIT, © 2022 ELABBASSI Hicham; licença em `wwwroot/img/corpo/LICENSE-body-highlighter.txt`)**, lidos como texto (o pacote não entra no projeto). Cada músculo é um `<g class="m-{slug}">`; `Infra/DesenhosDoCorpo` baixa o SVG do próprio site, marca as áreas (`principal`/`secundario`) e guarda na memória; mapeamento `GrupoMuscular` → áreas em `DesenhosDoCorpo.Areas` (Abdutores e Cardio sem área: sem boneco). CSS global em `app.css` (o SVG entra como `MarkupString`). Não usamos a API do Anatome (limite de 50 chamadas por dia; fotos desativadas; mídia da GymVisual só com licença).
  - **Desenho do corpo na conta:** `Perfil.CorpoDoDesenho` (Masculino padrão, Feminino), migration `CorpoDoDesenho` (coluna nas duas tabelas de perfil, existentes ficam Masculino). É uma preferência de exibição, não cadastro de sexo; a anonimização volta ao padrão. `DadosDaConta.CorpoDoDesenho` (nulo sem perfil) e `PUT /conta/desenho-do-corpo` com `PreferenciaDeDesenho`. Front: escolha Masculino/Feminino em "Conta". Testes em `Auth/DesenhoDoCorpoTests` e `PerfisTests`.
  - **Série preparatória** (pedido de 22/10/2026): `TipoSerie.Preparatoria` / `TipoDeSerie.Preparatoria` (aproximação antes da primeira série de trabalho). Gravado como texto, sem migration. Aparece no construtor, na tela do treino e na execução (" · preparatória", `Rotulos.Marcador`) e na exportação.
  - **Responsividade (22/10/2026, depois do teste do usuário no celular):** varredura com Playwright de 15 telas (aluno, personal, TI e entrada) em 320, 360 e 412 px, com a API simulada (login falso só para o front ler o papel) e dados longos, procurando rolagem horizontal, elementos passando da borda, irmãos sobrepostos e texto saindo da caixa. Correções em `app.css`: linhas de `.lista` quebram em vez de sobrepor (`flex-wrap`, nome com `flex: 1 1 auto`, `overflow-wrap: anywhere`); `.duas-colunas > *` com `min-width: 0` (o e-mail longo do convite deixava a tela "Alunos" com 673 px); `.dados dd` quebra o e-mail. Construtor: setas e × descem para a linha de baixo quando o nome é longo. Barra de navegação com cinco abas: letras mais justas abaixo de 400 px. Depois: zero apontamentos.
  - **Sem soma de kg da sessão** (decisão de 22/10/2026): listas de treinos feitos (aluno e personal) mostram séries, duração e cardio (`Rotulos.NumerosDaSessao`); o resumo troca "Volume" por "Cardio" e, por exercício, só a variação de volume em relação à última vez; o PDF do treino do dia troca o total de kg por "Exercícios". Motivo: somar carga de exercícios diferentes (leg press com rosca) não diz nada e o número enorme confunde; o volume que importa é por exercício (gráfico de evolução) e, mais à frente, séries por músculo na semana (mapa de volume). `SessaoResumo.VolumeKg` continua no contrato e no Excel.
  - **Fonte das fotos e vídeos (conversa de 22/10/2026):** não usar material de outros apps ou sites (ex.: BeFit) sem licença por escrito; caminhos: licenciar uma biblioteca, gravar os próprios vídeos ou encomendar ilustrações com cessão de direitos.
  - **Deploy:** aplicar as migrations `AnotacoesDoAluno` e `CorpoDoDesenho` no banco do MonsterASP (script idempotente `artifacts/migracao.sql`) antes de publicar a API. Conferir a exportação de PDF depois de publicar (o QuestPDF usa uma biblioteca nativa, que o pacote já traz para Windows x64 e x86).
- **Ajustes de 25/10/2026 (pedidos do usuário): presets de séries, treino do próprio aluno e personal que treina** (migration `PresetsEPersonalQueTreina`, já aplicada no LocalDB):
  - **Presets de séries** (`Domain/Treinos/PresetDeSeries`): nome (até 60) + 1–20 séries com tipo, repetições e descanso, **sem carga** (o domínio recusa). Os **prontos ficam no código** (`PresetDeSeries.Prontos`, Ids v7 fixos, sem dono, só leitura): Clássico 3 × 10, Hipertrofia 4 × 8–12, Força 5 × 5, Pirâmide crescente (10 · 8 · 5 · 3), Resistência 3 × 15–20 e Com aquecimento. Os próprios ficam em `PresetsDeSeries`/`SeriesPresetDeSeries`, dono = a conta (`DonoId` → `AspNetUsers`, serve para personal e aluno), filtro de dono e de ativos, até 50 por conta, inativados na exclusão de conta. Aplicar **copia** as séries: mudar o preset não altera treinos.
  - Rotas `/presets` (política nova `PersonalOuAluno`: papel Personal ou Aluno, conta ativa, sem exigir 2FA porque não expõe dados de alunos): `GET` (prontos primeiro, depois os próprios por nome), `GET {id}`, `POST`, `PUT {id}`, `POST {id}/inativar`. Pronto → 403 ao alterar; de outra conta → 404. Testes em `Api.Tests/Treinos/PresetsTests` e `Domain.Tests/Treinos/PresetDeSeriesTests`.
  - **Treino montado pelo aluno:** `POST /treinos` (`Aluno`) cria treino sem personal, no fim da lista. `PUT /treinos/{id}` e `POST /treinos/{id}/inativar` passaram para `PersonalOuAluno`: o montado pelo aluno só ele altera (personal → 403); o prescrito só o personal que prescreveu **e com 2FA** (checado com `IAuthorizationService` contra `Policies.Personal`). `TreinoResumo.MontadoPeloAluno` (opcional no contrato). O personal vê o treino e as sessões do aluno, mas não altera.
  - **Personal também treina:** o cadastro de personal cria também o papel `Aluno` e o `PerfilAluno` (mesmo Id e nome); a migration dá os dois aos personais existentes (não anonimizados, mesma situação e desenho do corpo). Assim ele usa as telas de aluno (lista, execução offline, sincronização, histórico) com os mesmos filtros de dono, e os alunos dele não veem nada disso. Convidar a si mesmo → 400 (na criação e no aceite). `PUT /conta/desenho-do-corpo` grava nos dois perfis. **Personal não aceita convite de outro personal** (decisão de 25/10/2026): `POST /convites/aceitar` com conta que tem `PerfilPersonal` (inclusive pausado) → 403; para treinar com outro personal, usa uma conta de aluno com outro e-mail.
  - Consequência nos testes: "papel errado" nas rotas de aluno passou a usar uma conta só Admin (`ContasDeTeste.AdminSemOutrosPapeisAsync`), já que personal tem o papel Aluno.
  - Front: no construtor, cada exercício ganhou **"Aplicar preset"** (troca as séries, sem carga, com "Desfazer") e **"Salvar como preset"**; tela `/presets` (`Pages/Presets.razor` + `Componentes/EditorDePreset`) com os próprios (editar, arquivar) e os prontos ("Criar a partir deste"), com link em "Modelos" e em "Treinos". `EditarTreino.razor` atende os dois casos (`/treinos/novo` e `/treinos/{id}/editar` para o próprio treino; personal com 2FA pode "Começar de um modelo"). Aluno não cria exercício personalizado (busca com `PodeCriar=false`). "Treinos" vira "Meus treinos" para o personal; tela do treino próprio com "Editar treino" e "Arquivar"; textos sem "seu personal" no treino próprio; na tela do aluno, o personal vê "Do aluno" sem editar. "Tipo de conta" mostra só "Personal".
  - Verificado com Playwright em 390 e 1280 px (personal sem 2FA montando o próprio treino com preset, desfazer, salvar preset, tela de presets, execução). Corrigido no caminho: o seletor de preset alargava a tela no celular. Contas de teste `teste-presets-*@volumo.test` ficaram no LocalDB. Testes: 370+ passando.
  - **Deploy:** aplicar a migration `PresetsEPersonalQueTreina` no MonsterASP antes de publicar a API (o script idempotente cobre os INSERTs dos personais).
  - **Aluno reordena os próprios treinos** (pedido de 25/10/2026): `PUT /treinos/ordem` (`Aluno`) com `OrdemDosTreinos` trazendo cada treino montado por ele uma vez (prescrito, faltando ou repetido → 400). Os próprios trocam de lugar entre si, nas vagas que já ocupavam; os prescritos ficam onde o personal pôs, e a lista inteira é renumerada de 1 a N. O personal continua reordenando tudo por `PUT /alunos/{id}/treinos/ordem`. Para o personal nos próprios treinos, tudo é dele. Front: setas ↑ ↓ só nos treinos próprios em "Treinos" (com internet). Testes em `TreinosPropriosTests`; verificado no navegador.
- **Ajustes de 28/10/2026 (pedidos do usuário): volta ao treino em andamento, dias da semana e calendário** (migration `DiasDoTreino`, já aplicada no LocalDB):
  - **Treino em andamento:** `/treinos` (menu "Treinos" e abertura do app) leva direto para `/treinos/{id}/sessao` quando há sessão em andamento no aparelho e o treino ainda está na lista (arquivado, não redireciona, para não prender a pessoa num treino que não abre). Os links "Treinos" de dentro do fluxo (tela do treino, sessão finalizada, histórico, editor) usam `treinos?todos=true`, que mostra a lista com o cartão "Em andamento".
  - **Dias da semana:** `Treino.Dias` (`Domain/Treinos/DiasDaSemana`, enum de flags com o bit de cada `DayOfWeek`; gravado como `int`, exceção à convenção de enum como texto) e `Treino.MarcarDias`. `PUT /treinos/{id}/dias` com `DiasDoTreino` (lista de `DayOfWeek`; vazia desmarca), mesma regra de quem altera o treino (`FindEditableAsync`: prescrito só o personal com 2FA, próprio só o aluno); dia inválido → 400. `TreinoResumo.Dias` e `TreinoDetalhe.Dias` opcionais no contrato (cópias antigas no aparelho continuam valendo). Testes em `TreinoTests` e `TreinosPropriosTests`.
  - Front: `Componentes/EscolhaDeDias` (sete botões seg–dom, grava a cada toque) na tela do aluno do personal (treinos prescritos) e na tela do treino próprio; dias e etiqueta "Hoje" na lista do aluno. `Componentes/CalendarioDeTreinos` (mês de segunda a domingo, dia treinado em limão, ponto nos dias marcados de hoje em diante, toque no dia lista os treinos feitos) em "Treinos" (com link para o histórico) e na tela do aluno do personal. Usa o histórico que já vem (`SessaoResumo`, últimas 50 sessões), então volta só até o mês da sessão mais antiga dele; se precisar de mais, criar uma rota de dias treinados por mês. Não verificado no navegador nesta sessão.
  - **Deploy:** aplicar a migration `DiasDoTreino` no MonsterASP antes de publicar a API.
- Pendências do front: arrastar e soltar no construtor (hoje: subir/descer); série extra além da prescrita na execução; substituição de exercício (aparelho ocupado/inexistente, CLAUDE.md seção 1); CSP do site estático (no hospedeiro, com `script-src 'self' 'wasm-unsafe-eval' https://challenges.cloudflare.com` e `connect-src` da API); testes de componentes (bUnit) e E2E (Playwright).

## 8. Próximo passo

1. ~~Front, etapa 2~~ ✅ (20/10/2026): construtor de treino, modelos, tela de treino do aluno e registro offline (ver seção 7).
2. ~~Fase 5.3: painel do personal~~ ✅ (20/10/2026).
3. ~~Publicação do beta~~ ✅ (21/10/2026). Resumo do dia:
   - **Decisão: beta fechado e gratuito antes da cobrança.** A fase 6 é construída durante o beta, ajustada pelo feedback dos personais; cobrança pronta no fim do beta, com preço de fundador para quem participou.
   - **MonsterASP:** dois websites, `apivolumo.runasp.net` (API) e `appvolumo.runasp.net` (front), HTTPS Let's Encrypt nos dois (ligar também o "HTTPS redirect" no painel). Todas as configurações da API como variáveis de ambiente no painel (*Websites → Manage website → Scripting → Environment Variables*): `AllowedHosts`, `ConnectionStrings__Volumo`, `Cors__AllowedOrigins__0`, `Email__Provider`, `Email__Brevo__ApiKey|SenderEmail|SenderName`, `Front__BaseUrl`, `https_port`, `Jwt__Audience|Issuer|SigningKey`, `Turnstile__SecretKey`. Sem `ASPNETCORE_ENVIRONMENT` (padrão Production). O painel travou ao salvar a connection string com `Password=` e espaços; funcionou com o formato simplificado.
   - **Banco:** criado no MonsterASP (`db74982.public.databaseasp.net`); estrutura criada com `artifacts/migracao.sql` (ver "Banco de dados").
   - **E-mail:** Brevo implementado (`EmailBrevo`), remetente `contato.volumo@gmail.com` (Gmail dedicado ao projeto, usado também nas contas de serviços). Com domínio próprio, autenticar o domínio no Brevo.
   - **Turnstile:** widget da Cloudflare para `appvolumo.runasp.net`; Site Key pública no `wwwroot/appsettings.json` do Client, que agora aponta para `https://apivolumo.runasp.net`.
   - Publicação pelo Visual Studio com os `.publishSettings` (guardados fora do repositório, têm a senha de deploy), "Remove additional files at destination" desmarcado. Testado pelo usuário de ponta a ponta.
   - Segredos (chaves do Brevo, JWT, senha do banco) nunca passam pela conversa: o usuário cola direto no painel ou no `user-secrets`. A chave do Brevo colada no chat foi revogada e trocada.
4. **Próximo: preparar o beta para testadores de fora** (em andamento quando a sessão de 21/10 parou):
   - Termos de uso e política de privacidade (primeira versão escrita pelo Claude, revisar com advogado antes do lançamento pago) + aceite no cadastro gravado com data e versão.
   - Excluir conta pela tela (rota com reautenticação chamando `ExclusaoDeConta`, botão em "Conta").
   - Botão "Enviar opinião" (WhatsApp do Volumo ou formulário).
   - Com o usuário: conferir backup automático do banco no MonsterASP e testar uma restauração; chamar os personais do beta (CLAUDE.md, seção 10).
   - Vídeos dos exercícios: **não implementados** (só a ideia e o padrão de 20/10). Ficam para depois do beta; perguntar aos personais se os alunos sentem falta. O difícil é a fonte (biblioteca licenciada, gravação própria ou ilustração SVG).
5. **Fase 6: cobrança + LGPD**: assinatura do personal com webhook do gateway (validação de assinatura e idempotência) e as regras de inadimplência já aprovadas (seção "Decisões de produto sobre contas", item 1b); limite de alunos por plano; consentimento, exportação e exclusão de conta.
   - **Decisão pendente antes de começar:** qual gateway (Mercado Pago, Asaas ou Stripe — CLAUDE.md, seção 4). Afeta o webhook, o fluxo de assinatura recorrente e o Pix.

4. **Painel administrativo da plataforma (para a TI / dono do Volumo)** — pedido do usuário em 20/10/2026, importante. Área só para o papel `Admin` (já existe, com 2FA obrigatório e as rotas `/admin/usuarios/{id}/suspender|remover-suspensao`). Deve mostrar:
   - **Pagamentos:** assinaturas dos personais (plano, situação: em dia / em carência / inadimplente / rebaixado), cobranças e falhas, comissões das vendas de treinos, MRR e churn (métricas da seção 12 do CLAUDE.md).
   - **Contas:** personais e alunos (ativos, inativos, suspensos), busca, suspender/remover suspensão, validação de CREF.
   - **Uso:** personais ativos, alunos por personal, treinos registrados por semana, conversão grátis → pago.
   - **Saúde do sistema:** erros recentes, webhooks recusados, fila de e-mails.
   - **Catálogo de exercícios** (pedido de 21/10/2026): aba para criar e editar os exercícios do catálogo (nome, grupo, músculos secundários, equipamento, instruções em passos) com **foto e vídeo de técnica**. É o caminho para cumprir o padrão de 20/10 (mesma fonte e estilo para todos). Depende de: (1) storage de arquivos — **decidido em 21/10/2026: Cloudflare R2** (sem cobrança de download, API compatível com S3 via AWSSDK.S3), dois buckets (`volumo-catalogo` e `volumo-privado`), upload passando pela API (para checar o conteúdo real) e leitura por URL assinada até haver domínio próprio (depois, catálogo em domínio próprio com cache); implementação em disco para testes e desenvolvimento; chaves só em variáveis de ambiente / user-secrets — com upload validado por tipo/tamanho/conteúdo, renomeado e servido por URL assinada ou CDN; (2) limite de tamanho e formato do vídeo (curto, sem som, ex.: MP4/WebM até alguns MB). Catálogo hoje vem da migração (`CatalogoInicial`, Ids fixos): a edição pela TI passa a ser a fonte, mantendo os Ids. Exercício do catálogo nunca é apagado, só inativado.
     - ✅ **Feito em 21/10/2026.** Armazenamento: `Infrastructure/Armazenamento/IArmazenamentoDeArquivos` (áreas `Catalogo` e `Privado`) com `ArmazenamentoR2` (AWSSDK.S3 4.0.104.6; região `auto`, path style, checksums só quando obrigatórios, `DisablePayloadSigning`) e `Api/Arquivos/ArmazenamentoEmDisco` (dev/testes; URL `/arquivos/{token}` com token do Data Protection com prazo; recusado em produção). Config `Armazenamento:Provider` = `R2` ou `Disco`; sem ele a API não sobe. Variáveis de produção: `Armazenamento__Provider=R2`, `Armazenamento__R2__AccountId`, `__AccessKeyId`, `__SecretAccessKey`, `__BucketCatalogo=volumo-catalogo`, `__BucketPrivado=volumo-privado`.
     - Mídia (`Api/Arquivos/MidiaDoExercicio`): foto JPEG/PNG/WebP até 2 MB, vídeo MP4/WebM até 10 MB, formato conferido pelos primeiros bytes (não pela extensão), chave nova a cada envio (`exercicios/{id}/foto-{guid}.ext`), arquivo antigo apagado na troca, URLs de leitura com 6 h de prazo.
     - Domínio: `Exercicio` ganhou `MusculosSecundarios` (até 5, sem o principal), `Equipamento` (enum), `Instrucoes` (até 10 passos de 300 caracteres), `FotoChave` e `VideoChave` (migração `DetalhesDoExercicio`). **Não alterar mais as linhas existentes de `CatalogoInicial`**: a TI edita pelo painel.
     - Rotas: `GET /exercicios/{id}` (detalhe, `ContaAtiva`, inclusive inativo); `ExercicioResumo.FotoUrl` na busca; `/admin/exercicios` (Admin com 2FA): listar (com inativos), detalhe, criar, editar, inativar, reativar, `POST {id}/foto` e `{id}/video` (multipart, campo `arquivo`), `{id}/foto/remover` e `{id}/video/remover`. Exercício de personal → 404 ali. Testes em `Api.Tests/Treinos/CatalogoDaTiTests`.
     - Front: `Pages/Admin/Catalogo.razor` e `EditarExercicio.razor` (link "Catálogo" no menu para Admin; Admin sem outro papel abre no catálogo), miniatura da foto na busca de exercícios e `Componentes/ComoFazer` (vídeo em loop, equipamento, músculos e passos) na execução do treino.
     - Pendente: CSP do front com `img-src`/`media-src` liberando `https://*.r2.cloudflarestorage.com` e a API; vídeo offline (URLs mudam a cada 6 h; resolver com domínio próprio e URLs fixas); vídeo próprio do personal (área separada).
   - Faz sentido junto ou logo depois da fase 6, porque os dados de pagamento nascem nela. Toda rota nova entra na matriz 401/403/404 (Admin com 2FA) e nada de dados de saúde dos alunos no painel (LGPD: só números agregados).

**Ideias registradas para depois (20/10/2026):**
- Imagem por exercício no estilo do Hevy: a parte de código é simples (campo de imagem no `Exercicio`); o difícil é a fonte das ilustrações (pacote licenciado ou ilustrador), que deve ser a mesma dos vídeos. Alternativa barata discutida: boneco SVG próprio com o grupo muscular destacado (reaproveita no mapa muscular). O usuário pediu para deixar para depois.

**Para testar localmente (lembrete):**
- API e Client no perfil **https** (`https://localhost:7010` e `https://localhost:7184`); no perfil http o login pode cair ao recarregar (cookie só em https).
- E-mails de desenvolvimento ficam em `%TEMP%\volumo-emails` (confirmação, convite, troca de senha). Conta do usuário no LocalDB: `cauabr2@gmail.com` (e-mail confirmado manualmente em 20/10).
