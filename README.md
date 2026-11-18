# TMS Embarcador

Projeto acadêmico de ADS da USCS para apoiar a gestão de transporte de micro e pequenas empresas embarcadoras.

## Estado atual

Etapa 1 implementada e validada, aguardando revisão. Frontend inicial com página Início, sidebar, header e menu responsivo. Solução .NET estruturada com host HTTP e projetos de domínio, infraestrutura e testes. As funcionalidades de negócio serão implementadas nas etapas seguintes.

## Stack oficial

React, TypeScript, Vite e Tailwind CSS no frontend. C# e ASP.NET Core .NET 10 no backend, em monólito modular. EF Core e Npgsql, PostgreSQL e Supabase Auth estão previstos para as próximas etapas. Hospedagem prevista em Vercel, Render via Docker e Supabase.

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

O host inicial responde 404 para rotas, pois os endpoints serão implementados na Etapa 4.

```powershell
dotnet test apps/api/TMS.sln
```

O projeto xUnit está preparado, com zero casos nesta etapa. As regras e seus testes pertencem à Etapa 3.

Segredos devem ser fornecidos por variáveis de ambiente. Arquivos `.env.example` podem conter somente nomes e exemplos seguros. O layout inicial não exige configuração de ambiente.

## Documentação

Consulte `AGENTS.md`, `CLAUDE.md` e `docs/spec-mvp-v1.md`. Resultados realmente executados ficam em `docs/evidencias/README.md`.
