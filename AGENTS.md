# TMS Embarcador — instruções para agentes (Claude Code / Codex)

## Produto
TMS **embarcador** (não transportador) para micro e pequenas empresas.
O cliente é a empresa que envia mercadoria. Fluxo de valor:
cadastrar frete → cotar (tabela ou proposta manual) → comparar → selecionar parceiro → acompanhar status → concluir → indicadores.
Projeto acadêmico (USCS — ADS, Projeto de Extensão/PIM). Equipe: Pietro (tech lead), Gabriel (UX/front), Harley (back/banco).
Nome do produto: **TMS Embarcador**. Não usar "Atlas" em nenhum lugar.

## Stack (fechada — não trocar sem decisão registrada em docs/decisoes.md)
- Web: React + TypeScript + Vite + Tailwind CSS (`apps/web`)
- API: C# / ASP.NET Core Web API, .NET 10 LTS, EF Core + Npgsql (`apps/api`)
- Banco: PostgreSQL gerenciado pelo Supabase
- Auth: Supabase Auth (e-mail/senha); a API valida o JWT
- Deploy: Vercel (web), Render via Docker (API), Supabase (banco/auth)
- Python: NÃO usar nesta fase.
- Proibido: Node.js como backend, Flask, SQL Server, Angular, Next.js, AWS, Azure, microserviços.

## Arquitetura
Monólito modular. Módulos: Transportadoras, Fretes (inclui propostas e status), Documentos, Dashboard, Usuarios.
- `Tms.Domain`: entidades, enums, regras (máquina de status, cálculo de frete). Sem dependência de EF/ASP.NET.
- `Tms.Infrastructure`: DbContext, mapeamentos, migrations, cliente Supabase Admin.
- `Tms.Api`: endpoints organizados por módulo (pasta `Modules/<Modulo>`), DTOs, validação, auth.
- `Tms.Tests`: xUnit para regras de domínio.
Sem Repository genérico, sem MediatR, sem AutoMapper. Mapeamento manual de DTO.

## Regras invioláveis
- Multiempresa: toda tabela de negócio tem `empresa_id`. A empresa vem do usuário autenticado, NUNCA do body/query.
- Regras de negócio ficam no Domain, não em controller nem no React.
- Erros da API em ProblemDetails (400 validação com erros por campo, 404, 409 regra violada, 403).
- Segredos só em variáveis de ambiente. Nunca commitar `.env`, connection string ou service role key.
- Dados de seed são fictícios e devem parecer fictícios (nada de empresas reais).
- Textos da interface em português do Brasil, linguagem simples (público com baixo/médio letramento digital).
- Interface de sistema corporativo: sidebar, header, tabelas, filtros, formulários, status. Sem gradientes, sem efeitos, sem cards decorativos.

## Convenções
- Banco: tabelas e colunas em português, snake_case, sem acento.
- C#: classes de domínio em português (Frete, Transportadora, Proposta); infraestrutura em inglês idiomático é aceitável.
- Valores monetários: `numeric(12,2)` / `decimal`. Datas: `timestamptz` (UTC) e `date`.
- CNPJ/CPF/CEP armazenados só com dígitos.

## Forma de trabalhar
- Antes de mudanças grandes, apresente um plano curto e espere aprovação.
- Trabalhe por etapas; ao fim de cada etapa: resumo do que mudou, como testar, pendências.
- Não instale dependência nova sem justificar em uma linha.
- Toda decisão técnica nova vai para `docs/decisoes.md` (data, decisão, motivo).
- Não leia `node_modules`, `dist`, `bin`, `obj`.

## Comandos
- Web: `cd apps/web && npm install && npm run dev`
- API: `cd apps/api && dotnet run --project src/Tms.Api`
- Testes: `cd apps/api && dotnet test`
- Migrations: `dotnet ef migrations add <Nome> -p src/Tms.Infrastructure -s src/Tms.Api`
