# TMS Embarcador

Projeto acadêmico de ADS da USCS para apoiar a gestão de transporte de micro e pequenas empresas embarcadoras.

## Estado atual

Frontend inicial com página Início, sidebar, header e menu responsivo. O backend inclui domínio, testes xUnit e migration inicial para PostgreSQL. A API disponibiliza health e identificação do usuário.

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

Para o projeto de desenvolvimento TMS (`bdyyutxdrxsjrpztlxow`), abra **Connect → Session pooler** no Dashboard. Na raiz do repositório, execute `.\apps\api\configurar-supabase-dev.ps1 -PoolerHost '<host exibido no painel>'`. O script pede a senha com entrada mascarada e salva a conexão e a URL em .NET User Secrets do usuário local. Se necessário, informe o caminho da CA em `-CertificadoRaiz`. Use `ASPNETCORE_ENVIRONMENT=Development` para carregar essas configurações. Os testes externos continuam lendo as respectivas variáveis de ambiente.

O usuário do Supabase precisa ter um registro ativo em `usuarios` com o UUID do `sub`. Os UUIDs fictícios do seed servem aos ensaios locais. A inicialização normal da API não migra nem popula o banco.

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
