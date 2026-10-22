# Plataforma de treinos: resumo do planejamento

Site/PWA para quem treina e para personal trainers montarem e venderem treinos.
Planejamento de 14/10/2026.

## Regras de trabalho para o Claude
- **Não fazer commits** (nem push, amend ou merge). O usuário revisa e commita por conta própria; deixar as mudanças no working tree.
- **Ao concluir cada fase ou etapa**, terminar a resposta com duas explicações do que foi feito:
  - **Técnica:** decisões de arquitetura, padrões, bibliotecas e por que foram escolhidos.
  - **Não técnica (resumida):** poucas frases, sem jargão, dizendo o que mudou para o produto e para quem vai usar.

---

## 1. Diferenciais para fugir do padrão

O diferencial está no que acontece **entre** os treinos e na relação entre aluno e personal.

### Para quem treina
- **Progressão automática de carga**: sugere a carga da próxima sessão com base no desempenho, nas repetições de sobra (RIR/RPE) e no objetivo.
- **Modo academia lotada**: se o aparelho está ocupado, sugere um substituto que trabalha o mesmo músculo.
- **Aparelho indisponível ou inexistente na academia** (ideia de 16/10/2026): o aluno escolhe entre (a) fazer uma **variação livre** do exercício (ex.: halteres em vez de máquina) ou (b) **substituir** por outro exercício de uma lista de similares (mesmo músculo e movimento). A troca fica registrada na sessão para o personal ver.
- **Vídeos de execução** (ideia de 16/10/2026): cada exercício do catálogo com um vídeo curto mostrando a execução correta, pensando principalmente em iniciantes.
  - **Padronização (decisão de 20/10/2026), tomando como base a biblioteca de exercícios do Hevy:** vídeos curtos e diretos (poucos segundos, em loop, sem som nem narração, só o movimento), todos da **mesma fonte** e no mesmo estilo (mesmo enquadramento, fundo neutro, mesma pessoa ou modelo, mesma duração aproximada). Nada de misturar vídeos de origens diferentes (YouTube, redes sociais) no catálogo.
  - Junto do vídeo, cada exercício do catálogo traz o mesmo pacote de informações: músculo principal e secundários, equipamento e instruções curtas em passos.
  - Vídeo próprio do personal (exercícios personalizados) fica separado do catálogo e identificado como dele, para não quebrar o padrão.
- **Treino que se adapta ao dia**: "dormi mal" ou "só tenho 30 min" encurta ou ajusta o treino sem perder o foco da semana.
- **Mapa muscular de volume semanal**: boneco colorido mostrando músculos com pouco ou muito volume.
- **Recordes e histórico visual**: gráficos de evolução e alertas de novo recorde pessoal.
- **Offline e uso com uma mão**: botões grandes, cronômetro de descanso automático, sem login a cada uso.

### Para personal trainers
- **Construtor de treino rápido**: arrastar e soltar, templates reutilizáveis, periodização (mesociclos).
- **Painel de alunos**: quem treinou, quem sumiu há 5 dias, quem estagnou numa carga.
  - **Estagnação configurável por aluno** (decisão de 20/10/2026): o personal escolhe medir por dias ou por sessões sem subir peso; padrão de 15 dias sem subir peso, indicado na tela.
- **Vitrine/loja própria**: página com o nome do personal para vender programas prontos, com Pix e cartão.
- **Feedback por vídeo**: o aluno grava a execução e o personal comenta no ponto exato.
- **Check-ins automáticos**: fotos, peso e medidas com comparativo lado a lado.
- **White label leve**: logo e cores do personal no app do aluno.

### Para a plataforma (TI / dono do Volumo)
- **Painel administrativo** (pedido de 20/10/2026, importante): visão de pagamentos e assinaturas (situação de cada personal, falhas de cobrança, comissões, MRR, churn), contas (personais e alunos, suspensão, CREF), uso da plataforma e saúde do sistema. Só para o papel Admin com 2FA; dados de alunos apenas agregados (LGPD). Detalhes e ordem em `docs/contexto-backend.md`, seção 8.

### Diferenciais mais ousados
- Desafios entre alunos do mesmo personal (ranking de frequência, não de carga).
- **Ranking de alunos com foto de check-in** (ideia de 16/10/2026): ao fim de cada treino o aluno tira uma foto, que valida o treino e soma pontos. Rankings por **personal**, **academia** e **cidade**. Cuidados levantados:
  - Pontuar **constância** (treinos concluídos, semanas seguidas), nunca carga, para não incentivar exagero e lesão.
  - **Participação opcional** (opt-in) e com apelido: aparecer num ranking público expõe a rotina e o local da pessoa.
  - Foto é dado pessoal (imagem) e às vezes de saúde (corpo): consentimento próprio, não exibir publicamente por padrão, apagar na exclusão de conta. A foto pode ficar só como prova para o sistema/personal.
  - Antifraude: foto tirada pela câmera dentro do app (não da galeria), horário do servidor, um check-in por treino/dia, e a sessão precisa ter séries registradas.
  - Academia e cidade exigem saber onde a pessoa treina: preferir que ela escolha a academia num cadastro, em vez de rastrear GPS.
  - ~~Começar pelo ranking por personal~~ **Mudança de 22/10/2026: o ranking é por círculo de amigos, não por personal.** O aluno convida quem quiser (amigos, de qualquer personal ou sem personal), e o ranking é feito entre as pessoas do círculo. Continuam valendo os cuidados acima (pontuar constância, nunca carga; participação opcional; foto não pública). Pontos a decidir: convite por link ou por e-mail, se quem é convidado precisa aceitar, se a pessoa pode estar em mais de um círculo e como sair dele. Depende do cadastro de aluno sem convite do personal (o mesmo do "Compartilhar treinos"). Academia e cidade ficam para depois, se fizer sentido.
- Gerar treino com IA a partir de uma conversa, sempre revisável pelo personal.
- Integração com relógios e Apple Health / Google Fit.
- **Compartilhar treinos** (ideia de 21/10/2026): o aluno manda um link para outras pessoas seguirem o treino. Cuidados levantados:
  - Treino montado pelo próprio aluno: compartilhamento livre. Treino prescrito pelo personal é o produto dele (concorre com a loja): só com autorização do personal e com o nome dele no treino, virando propaganda para ele.
  - Link público só leitura com exercícios e séries, sem cargas nem histórico de quem compartilhou; "Copiar para meus treinos" exige criar conta; quem compartilhou pode desativar o link.
  - Depende do cadastro de aluno sem convite (hoje o aluno só entra por convite). Depois do beta.
- **Login com Google** (ideia de 21/10/2026), depois do beta: personal continua com o 2FA do app; aluno novo segue entrando pelo convite; conta existente com o mesmo e-mail só é ligada depois de um login com senha.

---

## 2. Anúncios ou venda?

**Não depender de anúncios**, porque:
1. Anúncio em fitness rende pouco por usuário; precisaria de volume enorme.
2. Anúncio atrapalha justamente o momento de uso (entre séries).
3. O personal trainer usa a ferramenta para ganhar dinheiro e aceita pagar mensalidade.

**Modelo recomendado: freemium em dois lados.**
- Quem treina sozinho: grátis, com premium barato.
- Personal: assinatura mensal por faixa de alunos.
- Venda de treinos prontos: comissão sobre cada venda.

Se houver anúncio, só no plano grátis e em telas que não atrapalham (resumo pós-treino), de preferência parcerias diretas (suplementos, academias locais).

**Caminho sugerido:** começar pelo personal trainer (público menor, fácil de alcançar e que paga desde o início). Validar com 5 a 10 personais e depois abrir a versão grátis para quem treina sozinho.

---

## 3. Site ou aplicativo?

**Começar com PWA (Progressive Web App) responsivo.**

Vantagens:
- Um código só para celular, tablet e computador.
- Sem lojas de apps no início (sem taxas, revisões e comissão de 15–30% da Apple sobre assinaturas).
- Funciona offline com service workers.
- Notificações push no Android e no iPhone (com o app instalado na tela inicial).
- Atualização instantânea.
- Mais barato e rápido de validar.

Onde o app nativo ganha:
- Descoberta e credibilidade nas lojas.
- Integração com Apple Health / Google Fit.
- Cronômetro com tela bloqueada, widgets, Apple Watch.
- Notificações mais confiáveis no iPhone.

**Plano:** PWA agora; depois de validar, empacotar o mesmo front como app com .NET MAUI Blazor Hybrid (ou Capacitor, se o front for React/Vue).

---

## 4. Stack .NET

### Linguagem
**C#.** F# é nicho (cálculo e dados) e VB.NET está em modo de manutenção.

### Back-end
- ASP.NET Core Web API (Minimal APIs ou Controllers)
- Entity Framework Core
- ASP.NET Core Identity + JWT (ou Auth0 / Keycloak)
- SignalR para tempo real (chat, painel ao vivo)

### Front-end

| Critério | Blazor WebAssembly | React/Vue + API .NET |
|---|---|---|
| Linguagem | C# no front também | JavaScript/TypeScript |
| Curva de aprendizado | Baixa, mesmo ecossistema | Maior, se não usa JS |
| PWA | Template pronto | Simples com Vite + plugin PWA |
| Primeiro carregamento | Mais pesado (runtime .NET) | Leve |
| Componentes | MudBlazor, Radzen, DevExpress Blazor | Ecossistema enorme |
| Caminho para app nativo | .NET MAUI Blazor Hybrid | Capacitor ou React Native |

Escolha: **Blazor WebAssembly**, pela produtividade em C# e modelos compartilhados entre API e front.

### Banco e infraestrutura
- **SQL Server**: Express (gratuito, limite de 10 GB por banco) no MVP; Azure SQL Database ou licença Standard ao crescer.
- **Hospedagem**: **MonsterASP.NET** (https://admin.monsterasp.net/) para a API e o site (Blazor WebAssembly). Alternativas se crescer: Azure App Service + Azure SQL ou VPS Linux (Hetzner, DigitalOcean, Contabo) com SQL Server Express em Docker.
- **Pagamentos**: Mercado Pago, Asaas ou Stripe (Pix e assinatura recorrente).
- **Fotos e vídeos**: Azure Blob, AWS S3 ou Cloudflare R2.

### Stack final
**C# + ASP.NET Core + EF Core + SQL Server + Blazor WebAssembly (PWA) + componentes próprios**

Mudança de 16/10/2026: no lugar do MudBlazor, componentes com CSS próprio. O MudBlazor segue o Material Design, com visual padronizado de "app gerado", e o briefing de design pede identidade própria (ver `docs/contexto-backend.md`, seção "Front"). Também deixa o app mais leve para carregar no celular.

---

## 5. Segurança

A plataforma guarda dados pessoais e de saúde e processa pagamentos, então a LGPD se aplica.

| Área | Prática recomendada |
|---|---|
| Autenticação | ASP.NET Core Identity, senhas fortes, bloqueio após tentativas falhas, 2FA (obrigatório para personal), confirmação de e-mail |
| Tokens | JWT de vida curta (15–30 min) + refresh token rotativo em cookie HttpOnly, Secure, SameSite=Strict; nunca em localStorage |
| Autorização | Policies e roles (Aluno, Personal, Admin); checar no servidor se o recurso pertence ao usuário (contra IDOR) |
| Banco de dados | EF Core ou consultas parametrizadas; usuário com permissões mínimas, nunca `sa`; Always Encrypted ou TDE para dados sensíveis |
| Segredos | Azure Key Vault ou variáveis de ambiente; nada em `appsettings.json` versionado; User Secrets no desenvolvimento |
| Transporte | HTTPS obrigatório com HSTS |
| Cabeçalhos | Content-Security-Policy, X-Content-Type-Options, X-Frame-Options, Referrer-Policy |
| Entrada de dados | Validação no servidor (FluentValidation ou DataAnnotations); CORS só para o próprio domínio |
| Uploads | Limitar tipo e tamanho, checar conteúdo real, renomear, guardar fora da pasta pública, servir por URL assinada |
| Abuso | Rate limiting do ASP.NET Core em login, cadastro e APIs públicas; CAPTCHA (Cloudflare Turnstile) no cadastro |
| Pagamentos | Nunca tocar em dados de cartão; validar assinatura dos webhooks |
| Logs | Serilog + Application Insights, sem senhas, tokens ou dados de saúde |
| Dependências | `dotnet list package --vulnerable` no CI e Dependabot |
| Backup | Backups automáticos com teste periódico de restauração |
| LGPD | Termo de consentimento, política de privacidade, exportação e exclusão de conta pelo usuário |

Antes de lançar: checagem com OWASP ZAP e revisão do OWASP Top 10.

---

## 6. Planos e preços (ponto de partida para testar)

Regra: **quem treina com um personal nunca paga nada.** Quem paga é o personal.

### Quem treina sozinho

| Plano | Preço sugerido | O que inclui |
|---|---|---|
| Grátis | R$ 0 | Montar treinos, registrar cargas, cronômetro, histórico recente |
| Premium | R$ 14,90/mês ou R$ 119/ano | Progressão automática, mapa muscular, histórico completo, gráficos, sem anúncios |

### Personal trainers

| Plano | Preço sugerido | Alunos | Diferencial |
|---|---|---|---|
| Grátis | R$ 0 | Até 3 | Testar com alunos reais |
| Start | R$ 39,90/mês | Até 15 | Construtor, painel de alunos, check-ins |
| Pro | R$ 79,90/mês | Até 50 | Loja de treinos, feedback por vídeo, relatórios |
| Elite | R$ 149,90/mês | Ilimitado | Marca própria, suporte prioritário |

Plano anual: equivalente a 2 meses grátis.

### Comissão na venda de treinos prontos

| Plano | Comissão |
|---|---|
| Grátis | 15% |
| Start | 10% |
| Pro | 5% |
| Elite | 0% |

Considerar a taxa do gateway na conta. Transparência nas taxas pode ser diferencial (há reclamações públicas sobre as taxas da carteira do MFIT).

### Por que esses valores
- Referência de ticket: um personal vende acompanhamento anual pelo MFIT por R$ 997,00 (≈ R$ 83/mês por aluno). O plano Start custa menos que metade disso.
- Cobrança por faixa de alunos acompanha o crescimento do personal.
- Grátis com 3 alunos permite testar sem risco.
- Antes de fixar, conferir os preços dos concorrentes cadastrando-se como personal.

### Táticas de lançamento
1. Teste grátis sem cartão (o MFIT oferece 10 dias; 14 ou 30 dias destacam).
2. Preço de fundador para os primeiros 50 personais.
3. Indicação: personal que traz outro ganha um mês grátis.
4. Lançar com dois planos (Grátis + um pago de ~R$ 49,90, até 30 alunos) e só depois dividir em faixas.

---

## 7. Identidade visual

### Paleta recomendada: grafite + verde-limão

| Papel | Cor | Hex | Uso |
|---|---|---|---|
| Fundo | Grafite | `#0F1115` | Fundo das telas |
| Cards | Grafite claro | `#1C1F26` | Cards de exercício, painéis |
| Ação | Verde-limão | `#C6F135` | Botões e destaques; texto `#1A2200` por cima |
| Conquista | Laranja | `#FF6B2C` | Só para recordes e marcos |
| Texto | Quase branco | `#F2F4F5` | Texto principal |

Por quê: tema escuro combina com academia e cansa menos a vista; o limão se diferencia do vermelho, laranja e azul que dominam o nicho. Nunca usar limão como texto sobre fundo branco.

Alternativa mais conservadora: grafite `#121212` + laranja elétrico `#FF5A1F` + turquesa `#2EC4B6` para progresso.

### Nome da marca

Nenhum destes apareceu como app ou marca de treino nas buscas de 14/10/2026:

| Nome | Ideia | Avaliação |
|---|---|---|
| **Volumo** | Volume de treino, principal indicador de hipertrofia | Favorito: curto, fácil de falar, bom como domínio |
| Repforte | Rep + forte | Bem brasileiro, comunica força |
| Progreda | Progredir, ligado à progressão de carga | Menos óbvio que é treino |
| Treinora | Treino + sufixo | Evitar: perto demais do app Treino, da Pacto Soluções |

Checagem antes de decidir:
- [ ] INPI, classes 9, 41 e 42 — https://busca.inpi.gov.br
- [ ] Domínio .com.br (https://registro.br) e .com
- [ ] Arroba no Instagram e TikTok
- [ ] Nome exato na App Store e Google Play

---

## 8. Estrutura da solução

Uma solução só, com vários projetos, para compartilhar DTOs e validações entre API e front.

```
Volumo.sln (ou .slnx no .NET 10)
├── src/
│   ├── Volumo.Api             → ASP.NET Core Web API (Controllers, auth, SignalR)
│   ├── Volumo.Client          → Blazor WebAssembly Autônomo (PWA)
│   ├── Volumo.Shared          → DTOs, enums, validações
│   ├── Volumo.Domain          → Entidades e regras de negócio
│   └── Volumo.Infrastructure  → EF Core, SQL Server, pagamentos, arquivos
└── tests/
    ├── Volumo.Domain.Tests        → regras das entidades
    ├── Volumo.Api.Tests           → integração (API + LocalDB) e jornadas completas (Jornadas/)
    └── Volumo.Architecture.Tests  → dependências entre camadas
    (Volumo.E2E.Tests separado só quando houver ambiente publicado ou Playwright)
```

Estado atual do backend, decisões e próximos passos: `docs/contexto-backend.md`.

Referências:
- Client → Shared
- Api → Shared, Domain, Infrastructure
- Infrastructure → Domain
- Domain → nenhuma

Decisões:
- Front: **Aplicativo Autônomo Blazor WebAssembly** com PWA marcado (não o "Vazio", não o "Blazor Web App"), para funcionar offline, manter a API independente e permitir hospedagem estática.
- API: **Controllers** (`dotnet new webapi --use-controllers`), mais organizados quando a API cresce.
- Rodar junto no Visual Studio: solução → Configure Startup Projects → Multiple startup projects (Api e Client); liberar CORS da URL do Client em desenvolvimento.

### Ajustes de ambiente
- Erro **NU1301** (fonte local de NuGet inexistente): criar `nuget.config` na raiz com `dotnet new nugetconfig` (usa só o nuget.org) e rodar `dotnet restore`. Opcional: `dotnet nuget disable source "<nome>"` para desativar a fonte quebrada no computador.
- No CMD, `dotnet sln add` não aceita `**`; adicionar um projeto por vez ou, no PowerShell:
  `Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }`
- Conferir com `dotnet sln list` e `dotnet build`.

---

## 9. Escopo do MVP

**Entra na primeira versão:**
- Cadastro e login (Aluno e Personal)
- Construtor de treino com templates
- Painel de alunos (quem treinou, quem sumiu)
- App do aluno: ver treino, registrar carga e repetições, cronômetro de descanso, funcionar offline
- Cobrança da assinatura do personal

**Fica para depois:** progressão automática de carga, mapa muscular, loja de treinos, feedback por vídeo, IA, desafios, integrações com relógios, app nativo.

Primeiras entidades a modelar: **Usuário** (perfil Aluno ou Personal) e **Treino** (exercícios e séries).

---

## 10. Plano de validação

Antes de programar muito:
1. Conversar com 10 personais: como montam e entregam treinos hoje, o que mais incomoda, quanto pagam por ferramentas.
2. Mostrar um protótipo navegável (telas, sem back-end completo).
3. Meta para seguir em frente: pelo menos 5 personais dispostos a testar e 2 dispostos a pagar o preço de fundador.
4. Beta fechado com esses personais e seus alunos, com conversas semanais de feedback.

---

## 11. Aquisição de clientes

- **Instagram e TikTok:** é onde os personais vendem; conteúdo mostrando o produto resolvendo dores reais (montar treino em 2 minutos, ver aluno sumido).
- **Academias da região:** apresentar para os personais que atendem lá.
- **Grupos e comunidades de educação física** (WhatsApp, Telegram, Facebook).
- **Faculdades de educação física:** parceria com estágio ou plano gratuito para recém-formados.
- **Indicação:** personal que traz outro ganha um mês grátis.
- **Efeito rede:** cada aluno convidado por um personal conhece a plataforma e pode virar usuário grátis ou indicar o próprio personal.

---

## 12. Métricas

| Métrica | O que mostra |
|---|---|
| Personais ativos | Tamanho real da base que paga |
| Alunos por personal | Quanto a ferramenta está sendo usada de verdade |
| Treinos registrados por semana | Engajamento dos alunos |
| Conversão grátis → pago | Se o plano grátis está atraindo o público certo |
| Churn mensal | Quantos personais cancelam por mês |
| MRR (receita recorrente mensal) | Saúde financeira |

---

## 13. Custos e ponto de equilíbrio

Levantar os custos mensais e calcular quantos personais pagantes cobrem tudo:

| Custo | Observação |
|---|---|
| Hospedagem da API e do site | MonsterASP.NET (https://admin.monsterasp.net/) |
| Domínio | .com.br e .com |
| Armazenamento de fotos e vídeos | Cresce com o uso; Cloudflare R2 tende a ser o mais barato |
| Gateway de pagamento | Taxa por transação |
| E-mail transacional | Confirmação de conta, recuperação de senha |
| Registro de marca no INPI | Custo único por classe |
| Contabilidade / CNPJ | Mensal |

Ponto de equilíbrio = custo mensal total ÷ ticket médio por personal.

---

## 14. Questões legais

- **CREF:** personal trainer precisa de registro no conselho regional. Validar o número do CREF no cadastro pode virar selo de confiança e diferencial.
- **Termos de uso:** a prescrição do treino é responsabilidade do personal, não da plataforma. Quem treina sozinho usa por conta própria.
- **LGPD:** medidas, fotos e anamnese são **dados de saúde**, considerados sensíveis pela lei; exigem consentimento específico e destacado, além de política de privacidade clara.
- **Formalização:** CNPJ (MEI ou ME) para emitir nota e receber pelos gateways. Confirmar com um contador o enquadramento correto para SaaS.

---

## 15. Retenção

O que faz o personal não cancelar:
- Histórico de todos os alunos concentrado na plataforma.
- Exportação fácil dos dados (passa confiança e reduz o medo de ficar preso).
- Lembretes automáticos para alunos sumidos.
- Relatório mensal de evolução para o personal mostrar ao aluno e justificar o preço dele.
- Suporte rápido e próximo no início (WhatsApp direto com você).

---

## 16. Roadmap

| Fase | Período | Entregas |
|---|---|---|
| Validação | Mês 0 | Conversas com personais, protótipo, nome e marca registrados |
| MVP | Meses 1–3 | Escopo da seção 9, beta fechado com personais fundadores |
| Lançamento | Mês 4 | Planos pagos abertos, aquisição pelo Instagram e indicações |
| Crescimento | Meses 4–6 | Loja de treinos, feedback por vídeo, progressão automática de carga |
| Expansão | Depois de validar | Plano premium para quem treina sozinho, app nativo com MAUI, integrações com relógios, IA |

---

## Fontes
- MFIT Personal na App Store (teste de 10 dias sem cartão): https://apps.apple.com/BR/app/id1283273690
- Reclamação sobre taxas da carteira MFIT: https://www.reclameaqui.com.br/mfit-personal/insatisfacao-de-profissional-de-educacao-fisica-com-o-sistema-mfit-personal-e-suas-constantes-e-negativas-mudancas_Rg7G1bQAigfP8a_Z/
- Exemplo de plano anual de personal (R$ 997): https://www.asaas.com/c/edm8kvgy76zopyxi
- App Treino (Pacto Soluções): https://appgoblin.info/apps/862662527

## Documentos relacionados
- Stack .NET para o PWA de treinos: https://claude.ai/code/artifact/ceb9088e-b42a-4d15-9aa4-c07bf2df9d70
- Planos e preços da plataforma de treinos: https://claude.ai/code/artifact/1f6c371d-1506-4a02-adfe-f19e2e6973d7
