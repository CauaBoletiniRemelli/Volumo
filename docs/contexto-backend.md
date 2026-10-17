# Contexto do backend: onde paramos

Última atualização: 14/10/2026 · Branch: `Criação-Back-End` · **Nada commitado ainda** (todas as mudanças estão no working tree).

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
| 2. Domínio + persistência | Entidades, EF Core, migrations, LocalDB, IDs GUID v7 gerados no cliente (sincronização offline) | ⏭️ **Próxima** |
| 3. Autenticação | Identity, confirmação de e-mail, bloqueio após falhas, JWT de 15 min, refresh token rotativo (hash no banco, revogação da sessão se um token antigo for reutilizado, cookie HttpOnly/Secure/SameSite=Strict), 2FA obrigatório para personal, Turnstile, convite de aluno | Pendente |
| 4. Autorização contra IDOR | Roles e policies, autorização por recurso, filtros globais do EF por dono, 404 para recurso alheio | Pendente |
| 5. Funcionalidades do MVP | CRUD de templates e treinos, atribuição ao aluno, registro de sessão com idempotência, painel do personal (último treino, sumido há mais de 5 dias, estagnado) | Pendente |
| 6. Cobrança + LGPD | Webhook do gateway com validação de assinatura e idempotência, consentimento, exportação e exclusão de conta | Pendente |
| 7. CI/CD | GitHub Actions (build, testes, `dotnet list package --vulnerable`, CodeQL, Dependabot, OWASP ZAP) e deploy no MonsterASP.NET | Pendente |

### Entidades previstas para a fase 2
`Usuario` (Identity) · `PerfilPersonal` (com CREF) · `PerfilAluno` · `VinculoPersonalAluno` (1 aluno → 1 personal) · `ConviteAluno` · `Exercicio` · `TemplateTreino` · `Treino` · `ItemTreino` · `Serie` · `SessaoTreino` · `RegistroSerie`

---

## 3. Estrutura atual da solução

```
Volumo.slnx
├── global.json                 → SDK 10.0.401 + executor de testes Microsoft.Testing.Platform
├── Directory.Build.props       → net10.0, Nullable, aviso vira erro, analisadores de segurança, NuGetAudit
├── Directory.Packages.props    → versões centralizadas de todos os pacotes
├── .editorconfig
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
│   ├── Volumo.Shared           → vazio (só AssemblyReference)
│   ├── Volumo.Domain           → vazio (só AssemblyReference)
│   └── Volumo.Infrastructure   → vazio (só AssemblyReference)
└── tests/
    ├── Directory.Build.props   → pacotes e usings comuns dos testes (xUnit v3, Shouldly, cobertura)
    ├── Volumo.Api.Tests        → testes de integração (WebApplicationFactory)
    └── Volumo.Architecture.Tests → regras de dependência entre camadas (NetArchTest)
```

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

Situação atual: **22 testes, todos passando.** `dotnet list package --vulnerable --include-transitive` não encontrou nada.

### Volumo.Api.Tests
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
- `HealthTests`.

### Volumo.Architecture.Tests
- O Domain não depende de Api, Infrastructure, Shared, Client, ASP.NET Core nem EF Core.
- O Shared não depende do servidor, porque roda no navegador.
- A Infrastructure não depende da Api.

### Convenções de teste
- xUnit v3 (4.x): sempre passar `TestContext.Current.CancellationToken` nas chamadas assíncronas (o analisador exige, e aviso vira erro).
- Nome do teste: frase em português com sublinhados (`Excecao_vira_problem_details_sem_vazar_detalhes_internos`).
- `IClassFixture<VolumoApiFactory>` para compartilhar a API entre os testes de uma classe.
- Banco real (LocalDB) nos testes de integração a partir da fase 2. **Não usar o EF InMemory.**

### Pacotes (versões em `Directory.Packages.props`)
ASP.NET Core 10.0.12 · Serilog.AspNetCore 10.0.0 · xunit.v3 4.0.2 · xunit.runner.visualstudio 4.0.1 · Microsoft.NET.Test.Sdk 18.11.0 · Shouldly 4.3.0 · NSubstitute 6.2.0 · Microsoft.AspNetCore.Mvc.Testing 10.0.12 · NetArchTest.Rules 1.3.2 · Microsoft.Testing.Extensions.CodeCoverage 18.12.0

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

### Outras
- Criar `tests/Volumo.Domain.Tests` na fase 2.
- Atualizar a estrutura de testes no CLAUDE.md (seção 8) para incluir `Volumo.Architecture.Tests` e `Volumo.E2E.Tests`.
- CI no GitHub: usar runner **Windows**, que tem LocalDB, ou parametrizar a connection string.
- Client: transformar em PWA (manifest + service worker), trabalho do front.
- Commit do que foi feito hoje: feito pelo usuário (o Claude não faz commits; regra no CLAUDE.md).

---

## 7. Próximo passo (amanhã)

**Fase 2: domínio e persistência**
1. Modelar as entidades em `Volumo.Domain`, com as regras dentro delas (invariantes) e testes em `Volumo.Domain.Tests`.
2. Em `Volumo.Infrastructure`: `DbContext`, configurações do EF, primeira migration, LocalDB no Development (connection string via User Secrets).
3. Testes de integração com LocalDB: um banco por execução de testes, limpo entre os testes (ex.: Respawn).
4. Health check do banco no `/health`.
