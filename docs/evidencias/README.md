# Evidências

## Estrutura acadêmica

- Seção 7.1 — capturas das telas, código de validação, SQL antes/depois e testes executados.
- Seção 7.2 — comprovação dos ambientes de desenvolvimento, homologação e produção.
- Apêndice D — captura do protótipo e aceite real do avaliador.
- Apêndice F — execução e resultados dos cenários RF02, RF05 e RF06.
- Apêndice G — participantes, tarefas, dificuldades e feedback reais de usabilidade.

Cada registro deve relacionar requisito, cenário, evidência, figura/documento e resultado. As consultas SQL antes/depois serão preparadas na Etapa 7.

## Validações executadas da Etapa 1

Registro da implementação e da revisão técnica realizada nesta sessão em 2026-11-17. O responsável aprovou a estrutura, compilação e responsividade com base nos resultados apresentados.

| Verificação | Resultado observado |
| --- | --- |
| Instalador SDK .NET | Assinatura Authenticode Microsoft válida. Instalação concluída com código 0. |
| dotnet --info | SDK 10.0.404, Windows x64. |
| npm install | 133 pacotes adicionados. Auditoria retornou 11 alertas altos. |
| Build React/Vite — npm run build | Aprovado. TypeScript e Vite 6.4.6 concluídos, com 29 módulos transformados. |
| Inicialização — npm run dev -- --host 127.0.0.1 --port 5173 --strictPort | Vite pronto em 548 ms na execução inicial. |
| GET http://127.0.0.1:5173/ | HTTP 200. |
| Build .NET 10 — dotnet build apps/api/TMS.sln | Aprovado. Zero avisos e zero erros na revisão técnica. |
| Desktop — 1440 × 900 | Aprovado. Sidebar e header alinhados, navegação funcionando, sem sobreposição observada. Largura de rolagem de 1440 pixels. |
| Tablet — 768 × 1024 | Aprovado. Sidebar visível, texto com quebra adequada, navegação funcionando, sem sobreposição observada. Largura de rolagem de 768 pixels. |
| Mobile — 390 × 844 | Aprovado. Menu abriu e fechou ao selecionar Início, com navegação para #inicio. Sem sobreposição observada. Largura de rolagem de 390 pixels com menu aberto e fechado. |
| Rolagem horizontal | Ausência de rolagem horizontal indevida nas três resoluções verificadas. |
| Console do navegador | Sem erros ou avisos observados na consulta realizada durante a revisão. |
| xUnit — dotnet test apps/api/TMS.sln --no-build | Runner executado. Zero testes implementados e nenhum caso disponível para execução. |
| git diff --check | Concluído sem erros de whitespace na revisão técnica. |
| npm audit | 11 alertas altos na cadeia de dependências de desenvolvimento. |
| npm audit --omit=dev | Zero vulnerabilidades reportadas. |

As verificações visuais foram realizadas no navegador integrado. Este registro descreve observações e saídas efetivamente obtidas. Capturas persistidas para o relatório acadêmico ainda estão pendentes.

## Verificações e evidências pendentes

- Validação da persistência em PostgreSQL e da autenticação com usuários do projeto Supabase de desenvolvimento.
- Cenários funcionais RF01–RF06, incluindo os registros do Apêndice F para RF02, RF05 e RF06.
- Consultas SQL antes/depois, dependentes da implementação da persistência.
- Comprovação de homologação e produção publicadas.
- Capturas para o relatório acadêmico e identificação das figuras.
- Aceite de avaliadores do protótipo e avaliação de usabilidade com participantes.

## Risco de dependências

A auditoria identificou ReDoS em picomatch, propagado pela cadeia de ferramentas de desenvolvimento Vite/Tailwind. A decisão e o compromisso de reavaliação estão registrados em `../decisoes.md`. O servidor frontend utilizado na validação foi iniciado em loopback.

## 2026-11-19 — Fundação da API HTTP

Restore concluído. Build Release com zero avisos e zero erros. A compilação Debug estava em uso por uma instância local da API. A execução Release de xUnit aprovou 99 testes de domínio e 12 testes HTTP com chaves de teste e EF InMemory. Zero falhas.

O EF gerou o SQL da migration inicial e confirmou correspondência com o modelo. A execução foi offline. Os quatro testes de PostgreSQL foram ignorados por ausência de conexão local de teste. O teste com dois access tokens reais foi ignorado por ausência de configuração do projeto Supabase. A aplicação da migration, a repetição do seed, a rejeição das FKs e o isolamento com dados e identidades reais seguem pendentes de execução.

## 2026-11-24 — Pré-validação do seed no Supabase de desenvolvimento

Consulta feita por Npgsql em transação somente leitura no projeto TMS configurado em User Secrets com TLS `VerifyFull`. O destino configurado corresponde ao Project Ref autorizado. A migration `20261119165808_Inicial` está registrada. Antes de qualquer execução, as oito tabelas de negócio estavam vazias:

| Tabela | Registros antes do seed |
| --- | ---: |
| empresas | 0 |
| usuarios | 0 |
| transportadoras | 0 |
| tabelas_frete | 0 |
| fretes | 0 |
| propostas | 0 |
| historico_status | 0 |
| documentos_fiscais | 0 |

Consultas de leitura utilizadas:

```sql
SELECT "MigrationId"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT count(*) FROM public.empresas;
SELECT count(*) FROM public.usuarios;
SELECT count(*) FROM public.transportadoras;
SELECT count(*) FROM public.tabelas_frete;
SELECT count(*) FROM public.fretes;
SELECT count(*) FROM public.propostas;
SELECT count(*) FROM public.historico_status;
SELECT count(*) FROM public.documentos_fiscais;

SELECT count(*)
FROM public.usuarios
WHERE id IN (
  '00000000-0000-4000-8000-000000000001',
  '00000000-0000-4000-8000-000000000002'
);
```

A migration retornou uma linha: `20261119165808_Inicial`. A consulta de colisão dos UUIDs locais fixos de usuário retornou zero. A inspeção integral de `SeedDesenvolvimento.cs` confirmou nomes e dados explicitamente fictícios, e-mails no domínio reservado `.invalid`, execução apenas em `Development` com `--seed-development`, transação única e advisory lock para serializar execuções. O seed não provisiona usuários no Supabase Auth.

Na pré-validação inicial, o seed não foi executado enquanto se aguardava decisão sobre os `Guid.NewGuid()` das entidades. Depois de aprovada a manutenção desses IDs, a execução e sua verificação de idempotência foram realizadas e registradas abaixo. O código do seed não foi alterado.

## 2026-11-24 — Seed fictício e idempotência no Supabase de desenvolvimento

Destino revalidado por Npgsql: User Secrets da API apontou para o projeto TMS autorizado, banco `postgres`, Session Pooler, TLS `VerifyFull` e certificado raiz configurado. A migration `20261119165808_Inicial` continuava registrada. As oito tabelas de negócio estavam vazias antes da execução; não havia CNPJs de seed nem UUIDs locais de usuário em `usuarios`.

### Revisão da idempotência antes da execução

`SeedDesenvolvimento.ExecutarAsync` abre uma transação, adquire `pg_advisory_xact_lock(20406731)` e processa as duas empresas antes de confirmar. Para cada empresa, `InserirEmpresa` procura o CNPJ fictício pela chave única. Se já existe, retorna antes de construir qualquer filho. A primeira execução cria a empresa e todo o conjunto relacionado uma única vez. Após o commit, a próxima execução encontra as duas chaves CNPJ e não gera novos GUIDs nem grava filhos. A chave única de CNPJ também protege o banco contra duplicidade. A estratégia não atualiza empresas preexistentes. O seed só é disparado com `--seed-development` e rejeita qualquer ambiente diferente de `Development`; não chama Supabase Auth.

Os IDs de entidades continuam sendo gerados com `Guid.NewGuid()`. Na base vazia verificada antes da execução, não houve colisão. A segunda execução comprovou que a guarda pelas chaves estáveis das empresas impede duplicação apesar dos GUIDs aleatórios.

### Execuções e contagens

Comandos executados, em `Development`, usando a configuração local de User Secrets:

```powershell
dotnet run --project apps/api/src/Tms.Api -- --seed-development
dotnet run --project apps/api/src/Tms.Api -- --seed-development
```

Ambas as execuções terminaram com código 0. A primeira criou o conjunto. Na segunda, os logs mostraram o advisory lock e as duas verificações de existência por CNPJ; não houve comandos de inserção ou atualização. As contagens antes, depois da primeira e depois da segunda execução foram:

| Tabela | Antes | Após 1ª execução | Após 2ª execução |
| --- | ---: | ---: | ---: |
| empresas | 0 | 2 | 2 |
| usuarios | 0 | 2 | 2 |
| transportadoras | 0 | 5 | 5 |
| tabelas_frete | 0 | 5 | 5 |
| fretes | 0 | 13 | 13 |
| propostas | 0 | 9 | 9 |
| historico_status | 0 | 39 | 39 |
| documentos_fiscais | 0 | 0 | 0 |

Distribuição após a segunda execução, agrupada pela empresa fictícia:

| Empresa | Usuários locais | Transportadoras | Autônomos | Tabelas | Fretes | Propostas | Históricos | Documentos |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Alfa | 1 | 4 | 1 | 4 | 12 | 9 | 38 | 0 |
| Beta | 1 | 1 | 0 | 1 | 1 | 0 | 1 | 0 |

A migration permaneceu `20261119165808_Inicial`. Consultas de vínculos entre fretes, usuários, transportadoras, propostas, histórico e documentos encontraram **0 relacionamentos cruzados entre empresas**. Os resultados da primeira e segunda execução foram iguais. Nenhum usuário foi criado no Supabase Auth; não houve exclusão de dados nem nova migration.

Consultas de leitura realizadas (as contagens foram repetidas após cada execução):

```sql
SELECT "MigrationId"
FROM public."__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT count(*) FROM public.empresas;
SELECT count(*) FROM public.usuarios;
SELECT count(*) FROM public.transportadoras;
SELECT count(*) FROM public.tabelas_frete;
SELECT count(*) FROM public.fretes;
SELECT count(*) FROM public.propostas;
SELECT count(*) FROM public.historico_status;
SELECT count(*) FROM public.documentos_fiscais;

SELECT e.nome,
       (SELECT count(*) FROM usuarios x WHERE x.empresa_id = e.id) AS usuarios,
       (SELECT count(*) FROM transportadoras x WHERE x.empresa_id = e.id) AS transportadoras,
       (SELECT count(*) FROM transportadoras x WHERE x.empresa_id = e.id AND x.tipo = 'AUTONOMO') AS autonomos,
       (SELECT count(*) FROM tabelas_frete x WHERE x.empresa_id = e.id) AS tabelas,
       (SELECT count(*) FROM fretes x WHERE x.empresa_id = e.id) AS fretes,
       (SELECT count(*) FROM propostas x WHERE x.empresa_id = e.id) AS propostas,
       (SELECT count(*) FROM historico_status x WHERE x.empresa_id = e.id) AS historicos,
       (SELECT count(*) FROM documentos_fiscais x WHERE x.empresa_id = e.id) AS documentos
FROM empresas e
ORDER BY e.nome;
```

## 2026-11-24 — Validação HTTP local da autenticação

Comando executado:

```powershell
dotnet test apps/api/tests/Tms.IntegrationTests/Tms.IntegrationTests.csproj -c Release --filter FullyQualifiedName~ApiHttpTests
```

Resultado observado: **12 aprovados, 0 falhas, 0 ignorados**. Os casos exercitaram `GET /api/health` (200), `GET /api/me` sem token e com token malformado, assinatura, audience ou issuer inválidos (401), usuário ausente/inativo (403), e usuário ativo (200). O caso 200 também enviou `empresa_id` de outra empresa na query e confirmou que a resposta manteve a empresa do cadastro local.

Esta suíte usa uma chave RSA efêmera e EF InMemory com usuários fictícios; ela valida o pipeline HTTP/JWT da API, mas **não usa usuários nem tokens reais do Supabase**. Os seis parâmetros exigidos pela suíte externa (`TmsTestSupabase__Url`, `Postgres`, `TokenAlfa`, `TokenBeta`, `EmpresaAlfa`, `EmpresaBeta`) não estavam configurados nesta execução. Assim, não foi executado o teste de token real. Nenhum usuário foi criado e nenhuma linha do Supabase foi alterada nesta validação.

## 2026-11-24 — Vinculação do primeiro usuário de desenvolvimento

No projeto Supabase TMS (`bdyyutxdrxsjrpztlxow`), a conexão foi confirmada para o banco `postgres` com TLS `VerifyFull`. A conta Auth UUID (omitido) foi encontrada confirmada, não banida, não excluída e não anônima. A Empresa Alfa (`a3e380f5-456e-4ee7-a18d-128d6e7c0a40`) existia com o nome `Embarcadora Fictícia Alfa`.

Antes da inserção, consultas parametrizadas confirmaram uma conta Auth, uma Empresa Alfa e zero registros em `public.usuarios` com o UUID ou com o e-mail confirmado. Uma transação `SERIALIZABLE` inseriu exatamente uma linha: nome `Harley Santos`, perfil `ADMIN`, ativa. A transação foi confirmada; consulta posterior somente leitura encontrou exatamente uma linha e validou UUID, empresa, perfil e estado ativo. Nenhuma linha de `auth.users`, usuário fictício ou outra empresa foi alterada. Não foram executados migration ou seed.

Consulta de verificação (valores sensíveis tratados como parâmetros e omitidos desta evidência):

```sql
SELECT id, empresa_id, perfil, ativo
FROM public.usuarios
WHERE id = @auth_user_id;
```

Resultado observado: `1 linha`; UUID corresponde ao Auth autorizado; empresa corresponde à Alfa; `perfil = ADMIN`; `ativo = true`. E-mail não registrado nesta evidência.

## 2026-11-24 — Validação local de health e autenticação anônima

Worktree `C:\Users\msant\TMS-Continuacao`, branch `feat/continuacao-inicio`, commit `e0e3a6ec14bc6cd6dfab2e606cc176b66406adc2`. A configuração JWT da API exige emissor HTTPS do projeto, audience `authenticated`, assinatura e validade; valida `sub` como UUID e `role=authenticated`. O cadastro local é localizado pelo `sub` e a empresa vem exclusivamente da linha ativa de `public.usuarios`.

A API foi iniciada em `Development` localmente. Resultado observado: `GET /api/health` **200**; `GET /api/me` sem token **401**. Não foi executado login real nem chamada autenticada: User Secrets contêm URL Supabase e conexão PostgreSQL, mas nenhuma chave pública (`anon`/publishable) necessária para o endpoint de token Auth está configurada. Nenhuma senha ou token foi solicitado, criado ou registrado. Não houve alteração do banco nesta validação.

## 2026-11-24 — Tentativa de login real Supabase Auth

Na worktree de continuidade, a API .NET foi validada em Development: `GET /api/health` **200**, `GET /api/me` sem token **401**, Swagger de Development disponível. O binário atendendo na porta local pertence a `apps/api/src/Tms.Api` desta worktree.

Foi feita uma tentativa de login por HTTPS em `https://bdyyutxdrxsjrpztlxow.supabase.co/auth/v1/token?grant_type=password`, com Publishable Key e credenciais digitadas localmente. O Supabase Auth retornou **401**. O corpo do erro foi deliberadamente omitido, sem imprimir ou persistir credenciais/tokens; portanto a causa exata (credenciais rejeitadas ou chave de projeto inválida) não foi distinguida. Nenhum access/refresh token foi obtido; `GET /api/me` autenticado não foi executado. Não houve alteração em `auth.users`, `public.usuarios` ou demais dados.

## 2026-11-24 — Login real e GET /api/me autenticado

Após corrigir os dados de entrada local, o login por HTTPS no Supabase Auth do projeto TMS retornou **200**. A API da worktree `feat/continuacao-inicio` estava em Development. Resultados observados: `GET /api/health` **200**; `GET /api/me` sem token **401**; `GET /api/me` com access token real mantido apenas em memória **200**. A resposta autenticada confirmou `nome = Harley Santos`, `perfil = ADMIN` e `empresa.nome = Embarcadora Fictícia Alfa`. E-mail, Publishable Key, access token e refresh token foram omitidos; nenhum token foi escrito em arquivo ou log. Nenhum dado ou configuração do banco/Auth foi alterado nesta validação.

## 2026-11-24 — RF02 Transportadoras: build e testes

Validação executada na branch `feat/continuacao-inicio`, commit base `e0e3a6ec14bc6cd6dfab2e606cc176b66406adc2`:

- `dotnet build apps/api/TMS.sln -c Release --no-restore`: sucesso, 0 avisos e 0 erros.
- `dotnet test apps/api/TMS.sln -c Release --no-build`: 121 aprovados, 0 falhas, 5 ignorados. Os ignorados são ensaios externos Supabase/PostgreSQL sem variáveis de teste configuradas.
- `npm ci`: restaurou as dependências existentes no lockfile, sem alterar os manifests; auditoria reportou 0 vulnerabilidades.
- `npm run build` em `apps/web`: sucesso (TypeScript e Vite).
- Na API atual em Development, `GET /api/health` retornou 200, `GET /api/me` sem token retornou 401 e `GET /api/transportadoras` sem token retornou 401.

Testes automatizados exercitaram listagem isolada por empresa mesmo com `empresa_id` malicioso na query, criação ignorando `empresa_id` no body, documento formatado normalizado, validação de CPF/CNPJ, conflito de documento, edição, desativação sem exclusão e 404 ao tentar editar cadastro de outra empresa. Não foram executadas migrations ou seed.

A validação autenticada foi concluída com login real no Supabase Auth (HTTP 200); `GET /api/transportadoras` retornou HTTP 200. A leitura confirmou **4 registros ativos** da Empresa Alfa: 3 transportadoras e 1 motorista autônomo. A consulta PostgreSQL somente leitura no projeto TMS (`bdyyutxdrxsjrpztlxow`, banco `postgres`, TLS `VerifyFull`) confirmou os mesmos totais. Uma contagem inicial do JSON no PowerShell agrupou o array como um único objeto; a interpretação foi corrigida e os quatro itens foram confirmados. Nenhum dado foi escrito no banco; não houve migration ou seed.

## 2026-11-24 — RF03–RF06 e navegação do MVP

Implementação validada na worktree `C:\Users\msant\TMS-Continuacao`, branch `feat/continuacao-inicio`:

- `dotnet build apps/api/TMS.sln -c Release --no-restore`: sucesso, 0 avisos e 0 erros.
- `dotnet test apps/api/TMS.sln -c Release`: **125 aprovados, 0 falhas e 5 ignorados**. Os cinco ignorados dependem de configuração de PostgreSQL local de teste ou tokens/credenciais de ensaio Supabase.
- Testes HTTP com chaves efêmeras e banco EF InMemory comprovaram as rotas de tarifa, isolamento por empresa, conflito de rota ativa, tentativa de transição inválida (409), cotação manual, remoção de proposta, cotação por tabela, seleção, status coletado/entregue, NF-e de metadados e indicadores/ranking. Também verificaram a geração OpenAPI em Development.
- `npm run build` em `apps/web`: sucesso, TypeScript sem erros e Vite 6.4.6 gerou o bundle de produção.
- A validação autenticada prévia no projeto TMS confirmou leitura de quatro parceiros da Empresa Alfa. Os novos endpoints RF03–RF06 não foram executados com escrita no Supabase. Nenhuma migration, seed ou alteração remota foi feita nesta etapa.

As telas Painel, Fretes, Transportadoras e Tabelas de frete estão integradas à API e usam respostas do backend. As capturas de tela acadêmicas ainda precisam ser feitas pela equipe em execução local com sessão autenticada; não foram anexadas imagens nesta validação.

## 2026-11-24 — Fluxo HTTP persistente em PostgreSQL isolado

Executado `ApiPostgresFlowTests` contra PostgreSQL 17 em contêiner local descartável, no banco local `tms_demo` e schema temporário isolado. O teste criou estrutura usando exclusivamente a migration já existente e registros fictícios em espaço local; não utilizou o seed nem conectou ao Supabase. Resultado real: **1 aprovado, 0 falhas, 0 ignorados**. O fluxo verificou leitura de transportadoras e tabela de frete, criação do frete ignorando `empresa_id` forjado, cotação pela tabela, contratação, estados `COLETADO`/`EM_TRANSITO`/`ENTREGUE`, metadados NF-e, histórico e dashboard. A listagem filtrada também permaneceu no cadastro da empresa autenticada. O contêiner local era descartável e não representava o banco compartilhado.

O frontend tinha `.env.local` criado localmente para o Project Ref TMS (conteúdo não lido nem registrado); o arquivo está coberto por `.gitignore`. O servidor local respondeu `GET /` com HTTP 200 e serviu os módulos do aplicativo. Não foi possível concluir validação visual/interação por navegador: o controle de computador bloqueou a janela por não confirmar a URL atual, e tentativas independentes de iniciar Node/Vite neste contexto falharam com `EPERM` ao acessar o perfil Windows. Portanto login visual e submissões a partir das telas **não foram observados nesta execução**. Não se afirma evidência de UI além do build anterior informado (125 testes API aprovados, 5 ignorados; build frontend aprovado).

Nesta retomada, `dotnet test` com restore tentou ler o NuGet.Config do perfil e recebeu acesso negado. A repetição `--no-restore` e o build Release também falharam antes da conclusão por acesso negado a arquivos intermediários de build/CodeCoverage na worktree. Foi observado aviso de conflito de versão entre `Microsoft.EntityFrameworkCore.Relational` 10.0.4 e 10.0.8 na compilação do projeto de integração. O teste PostgreSQL local havia passado antes dessas tentativas. Nenhum dado remoto, migration, seed, commit ou push foi executado.

## 2026-11-24 — CORS local e validação final para continuidade

O CORS em Development foi fixado para a única origem `http://localhost:5173`; `UseCors` continua antes de autenticação/autorização. A instância antiga que já ocupava a porta 5000 respondia health 200 mas não enviava `Access-Control-Allow-Origin`, indicando que precisava ser reiniciada. Sem interromper esse processo, a versão atual foi iniciada temporariamente em `http://localhost:5001`, com `ASPNETCORE_ENVIRONMENT=Development`.

Resultados reais na instância atualizada: OPTIONS `/api/me` de `http://localhost:5173`, com método solicitado GET e cabeçalho `authorization`, retornou **204**, `Access-Control-Allow-Origin: http://localhost:5173`, `Access-Control-Allow-Methods: GET` e `Access-Control-Allow-Headers: authorization`. A mesma solicitação de origem `http://localhost:5174` não recebeu cabeçalho CORS. `GET /api/health` retornou **200** e `GET /api/me` sem token retornou **401**. Não foi usado token real nesta validação. A configuração JWT permaneceu inalterada.

Validação para publicação, sem restore e sem acesso de escrita ao Supabase: `dotnet build apps/api/TMS.sln -c Release --no-restore` terminou com **0 erros e 1 aviso** de conflito preexistente entre `Microsoft.EntityFrameworkCore.Relational` 10.0.4 e 10.0.8. `dotnet test apps/api/TMS.sln -c Release --no-build --no-restore` terminou com **126 aprovados, 0 falhas e 6 ignorados**. Os ignorados requerem PostgreSQL de teste/Supabase e variáveis externas de teste; o teste local PostgreSQL de fluxo completo passou anteriormente (1 aprovado) contra contêiner PostgreSQL descartável. `npm run build` em `apps/web` terminou com sucesso (TypeScript e Vite 6.4.6). `git diff --check` sem erros. Não houve migration, seed, escrita no banco compartilhado, commit ou push nesta validação.

Em 24/11/2026, o usuário relatou ter observado login pela interface e dashboard funcionando. Esse relato é registrado como validação manual informada pelo usuário; não comprova inspeção manual de todas as telas. Permanecem pendentes a conferência visual de transportadoras, tabelas, fluxo completo de frete/propostas/status/documentos e capturas acadêmicas, bem como ensaios que exigem ambientes externos. O `.env.local` da web não foi lido nem adicionado; `.env.example` usa placeholder para a Publishable Key. Nenhuma credencial, token ou connection string foi incluída nesta evidência.
