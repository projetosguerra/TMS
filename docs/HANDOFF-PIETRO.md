# Handoff — TMS Embarcador

Atualizado em 24/11/2026. Worktree `C:\Users\msant\TMS-Continuacao`, branch `feat/continuacao-inicio`. Este documento descreve o estado para continuar sem depender do histórico de chat.

## Produto e fluxo implementado

O projeto é um TMS **embarcador** para micro e pequenas empresas. Stack fechada: React + TypeScript + Vite + Tailwind; ASP.NET Core/.NET 10 + EF Core/Npgsql; PostgreSQL/Supabase; Supabase Auth. Não trocar stack nem introduzir serviços fora do MVP.

RF01/login e sessão mínima, RF02 transportadoras e motoristas autônomos, tarifas por rota, RF03 fretes/cotações e seleção, RF04 histórico e status, RF05 metadados de documentos fiscais e RF06 indicadores estão conectados à API. A navegação web tem Painel, Fretes, Transportadoras e Tabelas de frete. A migration existente `20261119165808_Inicial` já contém o modelo necessário; esta implementação não criou migration. O dashboard consulta dados da API, sem mocks.

A empresa dos dados é derivada do `sub` do JWT e do cadastro local ativo; o frontend não define o escopo por `empresa_id`. O token de acesso do frontend fica em memória React. A chave Supabase publicável é configuração local do navegador e não substitui validação JWT da API.

## Estado observado

- Build backend Release: **0 erros, 1 aviso**. O aviso é conflito de dependência `Microsoft.EntityFrameworkCore.Relational` 10.0.4 e 10.0.8 no projeto de integração; investigar alinhamento de versões.
- Testes `dotnet test apps/api/TMS.sln -c Release --no-build --no-restore`: **126 aprovados, 0 falhas, 6 ignorados**. Os seis dependem de banco PostgreSQL de teste, Supabase/tokens externos ou configuração do teste local isolado.
- O teste persistente da API passou anteriormente com **1 aprovado** contra PostgreSQL 17 em contêiner local descartável, usando schema temporário. Não era o Supabase compartilhado.
- Build frontend `npm run build`: sucesso com TypeScript e Vite 6.4.6.
- CORS validado em API Development local: OPTIONS para `http://localhost:5173` retornou 204 e permitiu `authorization`; outra origem não obteve `Access-Control-Allow-Origin`. `/api/health` 200 e `/api/me` sem token 401. `UseCors` vem antes de autenticação/autorização.
- O usuário relatou em 24/11/2026 que observou login pela interface e dashboard funcionando. É relato de validação manual do usuário. As demais telas e o fluxo inteiro não foram verificados manualmente nesta retomada; capturas acadêmicas ainda pendem.
- Validação anterior no Supabase TMS confirmou login Auth e leitura autenticada das transportadoras da Empresa Alfa. Os novos fluxos RF03–RF06 não foram testados com escrita no banco compartilhado.

Os seis testes ignorados na última suíte requerem PostgreSQL de teste, Supabase/tokens externos ou variáveis de teste. O teste local PostgreSQL do fluxo completo havia passado em execução própria anterior; a última suíte foi executada sem esses ambientes e ignorou esse caso também.

## Configuração local

1. API: manter `ASPNETCORE_ENVIRONMENT=Development`; connection string Npgsql e `Supabase:Url` ficam em .NET User Secrets/ambiente local. Para Supabase, usar somente o projeto TMS autorizado (`bdyyutxdrxsjrpztlxow`), Session Pooler TLS VerifyFull. Nunca colocar segredos no repositório ou chat.
2. Frontend: copiar `apps/web/.env.example` para `apps/web/.env.local` e preencher localmente a Publishable Key do projeto TMS. O exemplo contém placeholder. `.env.local` é ignorado pelo Git; não o adicionar ao stage. `VITE_API_URL` pode apontar para API local; o padrão do cliente é `http://localhost:5000`.
3. O frontend exige `VITE_SUPABASE_URL` e `VITE_SUPABASE_PUBLISHABLE_KEY` (aceita também `VITE_SUPABASE_ANON_KEY` legado). Não usar Secret/Service Role Key.

## Iniciar

Terminal 1, a partir da raiz da worktree:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project apps/api/src/Tms.Api -- --urls http://localhost:5000
```

Terminal 2:

```powershell
cd apps/web
npm ci
npm run dev -- --host localhost --port 5173 --strictPort
```

Abrir `http://localhost:5173` e entrar com a conta de desenvolvimento já vinculada à Empresa Alfa no Supabase Auth. Em Development, CORS está fixado exclusivamente para `http://localhost:5173`; `Cors__Origin` é lido nos outros ambientes. Reinicie uma API antiga que já esteja rodando para que carregue a política nova.

## Como demonstrar

Entrar → Transportadoras → criar/selecionar transportadora fictícia → Tabelas de frete → configurar rota → Fretes → criar frete nessa rota → simular tarifa e adicionar proposta manual de outro parceiro → comparar/selecionar → avançar status até Entregue → registrar metadados fictícios NF-e → Painel para ver indicadores. Não use dados reais de destinatários ou documentos na apresentação.

Capturas sugeridas: sessão e parceiros; tabela; frete e propostas; detalhe com histórico entregue; metadados fiscais; dashboard. Não declarar capturas ou testes visuais que não foram produzidos.

## Próximas ações do Pietro

1. Fazer pull da branch publicada e conferir `git status` antes de começar.
2. Reiniciar qualquer API já executando em 5000, para carregar CORS novo; usar os comandos acima.
3. Completar validação manual pela UI nas telas Transportadoras, Tarifas, Fretes/Propostas, Histórico/Status e Documentos. Confirmar Dashboard com dados do período e capturar telas reais.
4. Reexecutar builds e testes na máquina de desenvolvimento. Os seis testes externos requerem infraestrutura/configuração própria; não redirecioná-los ao banco compartilhado sem autorização.
5. Avaliar o aviso de conflito EF Relational 10.0.4/10.0.8 e registrar a correção se alinhada.
6. Qualquer gravação de demonstração no Supabase compartilhado exige autorização explícita separada. Não executar seed ou migration nesta continuidade sem aprovação.

## Segurança e escopo

Não houve migration, seed, escrita no PostgreSQL compartilhado, criação de usuários ou deploy nesta etapa. Não versionar `.env.local`, User Secrets, senhas, tokens, connection strings, certificados privados ou artefatos de build. Não fazer merge para `main` nem abrir PR automaticamente.
