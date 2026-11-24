# TMS Embarcador

Projeto acadêmico de ADS da USCS para apoiar a gestão de transporte de micro e pequenas empresas embarcadoras.

## Estado atual

Frontend React autenticado com navegação para Painel, Fretes, Transportadoras e Tabelas de frete. A API oferece persistência de tarifas e fretes, cotação manual e por tabela, seleção de proposta, histórico de status, metadados fiscais e indicadores por período. A migration inicial existente já contém as tabelas dos módulos.

## Stack oficial

React, TypeScript, Vite e Tailwind CSS no frontend. C# e ASP.NET Core .NET 10 no backend, em monólito modular. EF Core e Npgsql fazem o mapeamento para PostgreSQL. A API valida o access token do Supabase Auth. Hospedagem prevista em Vercel, Render via Docker e Supabase.

## Estrutura

```text
apps/
  web/                      Aplicação React
  api/
    TMS.sln
    src/Tms.Domain/         Regras de domínio
    src/Tms.Infrastructure/ Persistência e integrações
    src/Tms.Api/            Host HTTP
    tests/Tms.Tests/        Projeto xUnit
docs/
  decisoes.md
  regras-de-negocio.md
  spec-mvp-v1.md
  testes/cenarios.md
  evidencias/README.md
```

## Desenvolvimento local

Pré-requisitos verificados na preparação: Node.js 20.16.0, npm 10.8.2 e SDK .NET 10.0.404. Vite 6 mantém compatibilidade com o Node instalado.

Frontend, a partir da raiz do repositório:

```powershell
cd apps/web
npm ci
npm run dev
```

O terminal informa a URL local. Para validar TypeScript e gerar o build, execute `npm run build` no mesmo diretório.

### RF02 — Transportadoras

Configure `apps/web/.env.local` a partir de `apps/web/.env.example`. Preencha a Publishable Key do projeto TMS em **Settings → API Keys**; a chave `anon` legada também é aceita. Não use Secret/Service Role e não versione `.env.local`.

Inicie a API em um terminal, na raiz do repositório:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project apps/api/src/Tms.Api -- --urls http://localhost:5000
```

Em outro terminal, execute `cd apps/web` e `npm run dev -- --host localhost --port 5173 --strictPort`, abra `http://localhost:5173` e entre com a conta Supabase Auth de desenvolvimento. Em Development, a API libera CORS somente para `http://localhost:5173`; em outros ambientes configure `Cors__Origin` para uma origem exata. O access token fica somente em memória; ao recarregar a página será necessário entrar novamente. A tela permite listar, cadastrar e editar transportadoras ou motoristas autônomos, e desativar sem excluir.

Endpoints protegidos por JWT: `GET/POST /api/transportadoras`, `GET/PUT /api/transportadoras/{id}` e `PATCH /api/transportadoras/{id}/ativo`. A empresa é resolvida pela conta autenticada e não é aceita no corpo do cadastro.

Backend, a partir da raiz:

```powershell
dotnet restore apps/api/TMS.sln
dotnet build apps/api/TMS.sln
dotnet run --project apps/api/src/Tms.Api -- --urls http://localhost:5080
```

`GET /api/health` informa se o host está respondendo. `GET /api/me` exige `Authorization: Bearer <access_token>` e devolve id, nome, e-mail, perfil e a empresa do cadastro. Token ausente ou inválido recebe 401. Usuário sem cadastro ativo recebe 403.

Configure estas variáveis no ambiente do processo da API.

| Variável | Uso |
| --- | --- |
| `ConnectionStrings__Default` | Conexão Npgsql do servidor. No Supabase use os dados do pooler de sessão do projeto de desenvolvimento, TLS com `SSL Mode=VerifyFull` e a CA confiável do projeto quando necessária. |
| `Supabase__Url` | URL HTTPS do projeto que emitiu o access token. Use chaves assimétricas de assinatura RS256 ou ES256. |
| `Supabase__JwksUrl` | Opcional. Por padrão a API usa `/auth/v1/.well-known/jwks.json` no projeto configurado. |
| `Cors__Origin` | Origem exata do frontend, incluindo porta quando aplicável, sem barra final. Sem configuração o navegador não recebe permissão cross-origin. |
| `ASPNETCORE_ENVIRONMENT` | Em `Development`, Swagger UI em `/swagger` e documento em `/swagger/v1/swagger.json`. |

No frontend, `.env.local` contém `VITE_API_URL` (opcional; padrão `http://localhost:5000`), `VITE_SUPABASE_URL` e `VITE_SUPABASE_PUBLISHABLE_KEY` (ou o nome legado `VITE_SUPABASE_ANON_KEY`). Use a Publishable Key do projeto TMS; o exemplo tem somente placeholder. Não coloque chave local real em `.env.example`.

Para o projeto de desenvolvimento TMS (`bdyyutxdrxsjrpztlxow`), abra **Connect → Session pooler** no Dashboard. Na raiz do repositório, execute `.\apps\api\configurar-supabase-dev.ps1 -PoolerHost '<host exibido no painel>'`. O script pede a senha com entrada mascarada e salva a conexão e a URL em .NET User Secrets do usuário local. Se necessário, informe o caminho da CA em `-CertificadoRaiz`. Use `ASPNETCORE_ENVIRONMENT=Development` para carregar essas configurações. Os testes externos continuam lendo as respectivas variáveis de ambiente.

O usuário do Supabase precisa ter um registro ativo em `usuarios` com o UUID do `sub`. Os UUIDs fictícios do seed servem aos ensaios locais. A inicialização normal da API não migra nem popula o banco.

### RF03–RF06 — Operação de fretes

Após iniciar a API e o frontend pelos comandos acima, entre pela conta de desenvolvimento. A navegação apresenta Painel, Fretes, Transportadoras e Tabelas de frete. Todas as listas e indicadores consultam a API; não há dados operacionais de demonstração mantidos no navegador.

Em **Tabelas de frete**, selecione uma transportadora ativa para cadastrar tarifas por rota (UF de origem/destino, mínimo, valor por kg e prazo). Em **Fretes**, cadastre os dados do envio. No detalhe, use **Calcular pela tabela** ou registre uma proposta manual, compare valor e prazo e selecione a proposta. As ações de acompanhamento são oferecidas conforme o status. A aba de documentos registra somente metadados fiscais; arquivo de NF-e não é enviado.

Endpoints protegidos implementados:

| Uso | Rotas |
| --- | --- |
| Tabelas de frete | `GET/POST /api/transportadoras/{id}/tabelas`; `PUT /api/tabelas-frete/{id}` |
| Fretes | `GET/POST /api/fretes`; `GET/PUT /api/fretes/{id}` |
| Cotação | `POST /api/fretes/{id}/cotacao/simular`; `POST /api/fretes/{id}/propostas`; `DELETE /api/fretes/{id}/propostas/{propostaId}`; `POST /api/fretes/{id}/propostas/{propostaId}/selecionar` |
| Acompanhamento | `POST /api/fretes/{id}/status`; histórico incluído em `GET /api/fretes/{id}` |
| Documentos fiscais | `GET/POST /api/fretes/{id}/documentos` |
| Indicadores | `GET /api/dashboard?de=AAAA-MM-DD&ate=AAAA-MM-DD` |

A consulta `GET /api/fretes` aceita filtros `status`, `busca`, `atrasados`, `pagina` e `tamanho`. A API sempre aplica o escopo da empresa associada ao usuário autenticado. A migration existente já contém as tabelas necessárias; esta etapa não criou migration nem alterou o banco Supabase.

#### Roteiro curto para demonstração

1. Inicie API em `Development` e frontend local, entre com a conta de desenvolvimento TMS.
2. Abra **Transportadoras** e mostre a lista real da empresa autenticada; cadastre uma transportadora fictícia caso precise de um segundo parceiro.
3. Em **Tabelas de frete**, cadastre uma tarifa para a rota de demonstração.
4. Em **Fretes**, crie um envio fictício na mesma rota; calcule pela tabela e adicione outra proposta manual de parceiro diferente. Compare valor e prazo e contrate uma opção.
5. Abra o frete contratado, avance os status disponíveis até **Entregue** e registre os metadados da NF-e (sem anexar arquivo fiscal real).
6. Abra **Painel** e mostre os indicadores para o período que inclua a data do frete.

Capturas sugeridas: (1) sessão identificada e lista de transportadoras; (2) tabela de tarifa cadastrada; (3) frete criado e propostas comparadas; (4) frete entregue com histórico; (5) metadados fiscais registrados; (6) painel com indicadores e ranking. Use somente dados fictícios na apresentação. Em 24/11/2026, o usuário relatou que observou login pela interface e o dashboard funcionando. As demais telas e o fluxo completo não foram verificados manualmente nesta execução; consulte a seção de evidências para os resultados realmente observados.

```powershell
dotnet test apps/api/TMS.sln
```

Para conferir o modelo e gerar o SQL sem conexão com banco, execute na raiz com o SDK .NET 10.

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes -p apps/api/src/Tms.Infrastructure -s apps/api/src/Tms.Api
dotnet ef migrations script -p apps/api/src/Tms.Infrastructure -s apps/api/src/Tms.Api
```

Para preparar um banco **local de desenvolvimento**, defina a variável de ambiente `ConnectionStrings__Default` com a conexão desse banco. Execute os comandos abaixo na raiz. O seed pode ser repetido após a migration e usa usuários fictícios sem senha.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update -p apps/api/src/Tms.Infrastructure -s apps/api/src/Tms.Api
dotnet run --project apps/api/src/Tms.Api -- --seed-development
```

Segredos devem ser fornecidos por variáveis de ambiente. Arquivos `.env.example` podem conter somente nomes e exemplos seguros. O layout inicial não exige configuração de ambiente.

## Documentação

Consulte `AGENTS.md`, `CLAUDE.md` e `docs/spec-mvp-v1.md`. Resultados realmente executados ficam em `docs/evidencias/README.md`.

## Testes de integração

O comando `dotnet test apps/api/TMS.sln` inclui os testes HTTP com chaves RSA efêmeras e EF em memória. A suíte valida respostas e regras de autorização do pipeline. A execução das suítes externas depende das variáveis abaixo. O xUnit registra os casos sem configuração como ignorados.

- PostgreSQL real. Defina `TmsTest__Postgres` com a conexão de um banco **local** chamado `tms_test`. O papel precisa criar esquemas e tabelas. Cada execução cria um esquema exclusivo, aplica a migration e exercita o seed, as FKs e as consultas por empresa com o papel proprietário usado no ensaio. O esquema fica disponível para inspeção.
- Supabase real. Em um projeto de desenvolvimento já migrado, prepare dois usuários ativos de empresas distintas. Defina `TmsTestSupabase__Url`, `TmsTestSupabase__Postgres`, `TmsTestSupabase__TokenAlfa`, `TmsTestSupabase__TokenBeta`, `TmsTestSupabase__EmpresaAlfa` e `TmsTestSupabase__EmpresaBeta`. Os tokens devem ser access tokens vigentes do projeto e as empresas são os UUIDs esperados. O teste acessa a API em memória, busca as chaves públicas do projeto e consulta o banco em modo de leitura.
