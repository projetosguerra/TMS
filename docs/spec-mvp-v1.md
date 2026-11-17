# Especificação — MVP v1 (fatia vertical real)

Objetivo: transformar o protótipo atual (front com localStorage) em uma fatia vertical real
React → ASP.NET Core API → PostgreSQL (Supabase), publicada na nuvem, para gerar evidências
reais do Capítulo 7 do relatório (telas, validações, banco antes/depois, hospedagem no ar).

Leia `CLAUDE.md` antes de começar. Trabalhe por etapas e PARE ao fim de cada uma com um resumo.

## Escopo

Dentro: login, transportadoras + tabelas de frete, fretes, cotação (tabela + proposta manual),
comparação e seleção, status com histórico, documentos fiscais (só metadados), dashboard, usuários (admin).

Fora: upload de arquivo, NF-e real (PlugNotas), rastreio por API/GPS, e-mail, pagamentos,
link público de rastreio para cliente externo, Python, dias úteis/feriados.

---

## Etapa 0 — Auditoria (sem alterar código)
1. Liste a estrutura atual (sem node_modules/dist) e as telas existentes.
2. Leia `docs/decisoes.md`, `docs/fluxo-operacional.md`, `docs/proximos-passos.md` e o README.
3. Responda: o que já existe de cotação/propostas? quais estados de frete existem? onde está o localStorage?
4. Liste conflitos entre esses docs e o `CLAUDE.md`. Pare e aguarde.

## Etapa 1 — Reorganização em monorepo
```
/apps/web            ← mover o projeto Vite atual para cá (sem reescrever telas)
/apps/api            ← solução .NET
   TMS.sln
   src/Tms.Domain
   src/Tms.Infrastructure
   src/Tms.Api
   tests/Tms.Tests
/docs
   decisoes.md, regras-de-negocio.md, testes/cenarios.md, evidencias/README.md
/CLAUDE.md  /README.md  /.gitignore
```
- `.gitignore`: node_modules, dist, .npm-cache, bin, obj, .env*, *.user.
- Remover `.npm-cache` e `dist` do controle de versão.
- Renomear qualquer "Atlas TMS" para "TMS Embarcador".
- Critério: `npm run dev` em apps/web continua funcionando igual.

## Etapa 2 — Banco (EF Core migrations → Supabase Postgres)

Tabelas (todas com `id uuid default gen_random_uuid()`, `criado_em timestamptz default now()`):

- **empresas**: nome, cnpj varchar(14) unique.
- **usuarios**: `id` = id do usuário no Supabase Auth (sem FK para auth.users), empresa_id, nome, email unique, perfil (`ADMIN`|`OPERADOR`), ativo bool.
- **transportadoras**: empresa_id, tipo (`TRANSPORTADORA`|`AUTONOMO`), nome, documento varchar(14) (CNPJ 14 ou CPF 11 dígitos), telefone, email, ativo. unique(empresa_id, documento).
- **tabelas_frete**: empresa_id, transportadora_id, uf_origem char(2), uf_destino char(2), valor_minimo numeric(12,2), valor_por_kg numeric(12,4), prazo_dias int, ativa bool.
- **fretes**: empresa_id, codigo varchar(20) unique (`FR-AAAA-NNNNNN`), status, origem_cep, origem_cidade, origem_uf, destino_cep, destino_cidade, destino_uf, destinatario_nome, peso_kg numeric(10,2), volume_m3 numeric(10,3) default 0, valor_mercadoria numeric(12,2) null, observacao, transportadora_id null, proposta_selecionada_id null, valor_contratado null, prazo_previsto date null, entregue_em timestamptz null, criado_por (usuarios), atualizado_em.
- **propostas**: frete_id, transportadora_id, origem (`TABELA`|`MANUAL`), valor numeric(12,2), prazo_dias int, observacao, selecionada bool. unique(frete_id, transportadora_id).
- **historico_status**: frete_id, status_anterior null, status_novo, observacao, usuario_id, ocorrido_em.
- **documentos_fiscais**: frete_id, tipo (`NFE`|`CTE`|`OUTRO`), numero, chave_acesso char(44) null, valor numeric(12,2) null.

Usar CHECK constraints para enums e valores > 0. Índices em (empresa_id, status) e (empresa_id, criado_em).
Seed (somente Development, idempotente): 1 empresa fictícia, 4 transportadoras (1 AUTONOMO),
tabelas SP→SP, SP→RJ, SP→MG, SP→PR, ~12 fretes em status variados com histórico coerente.

## Etapa 3 — Domínio (Tms.Domain) + testes

**Status:** `ABERTO`, `EM_COTACAO`, `CONTRATADO`, `COLETADO`, `EM_TRANSITO`, `ENTREGUE`, `CANCELADO`.
Transições permitidas:
- ABERTO → EM_COTACAO (automática ao registrar a 1ª proposta)
- EM_COTACAO → CONTRATADO (somente via seleção de proposta)
- CONTRATADO → COLETADO; COLETADO → EM_TRANSITO; COLETADO → ENTREGUE; EM_TRANSITO → ENTREGUE
- ABERTO | EM_COTACAO | CONTRATADO → CANCELADO (observação obrigatória)
Qualquer outra → exceção de domínio (API responde 409). Toda transição grava historico_status.

**Regras de negócio**
- RN01 Peso taxado = max(peso_kg, volume_m3 × 300). Fator 300 kg/m³ em constante configurável.
- RN02 Valor pela tabela = max(valor_minimo, peso_taxado × valor_por_kg), arredondado a 2 casas.
- RN03 Simular cotação: para cada transportadora ativa com tabela ativa para (origem_uf, destino_uf), cria/atualiza proposta TABELA. Se nenhuma for elegível → 422 "Nenhuma transportadora atende esta rota".
- RN04 Proposta MANUAL: valor > 0 e prazo_dias ≥ 1; substitui proposta existente da mesma transportadora para o frete.
- RN05 Propostas só podem ser criadas/removidas com frete em ABERTO ou EM_COTACAO.
- RN06 Selecionar proposta: frete passa a CONTRATADO; grava transportadora_id, valor_contratado, proposta_selecionada_id; prazo_previsto = data da seleção + prazo_dias (dias corridos). Trocar a proposta é permitido enquanto CONTRATADO.
- RN07 Edição de dados do frete só em ABERTO ou EM_COTACAO.
- RN08 ENTREGUE grava entregue_em. Atrasado é DERIVADO: prazo_previsto < hoje e status fora de ENTREGUE/CANCELADO. Entregue no prazo: data(entregue_em) ≤ prazo_previsto.
- RN09 Transportadora inativa não aparece em simulação nem aceita nova proposta; histórico preservado (nunca excluir fisicamente).
- RN10 Documento fiscal NFE exige chave_acesso com 44 dígitos.

**Validações**: CNPJ e CPF com dígito verificador, CEP 8 dígitos, UF válida, peso > 0.

**Testes xUnit obrigatórios**: todas as transições válidas e inválidas; RN01/RN02 com casos de peso cubado maior e menor; RN08 atraso; validação de CNPJ.

## Etapa 4 — API (Tms.Api)

Auth: validar JWT do Supabase (JWKS do projeto ou JWT secret legado, conforme o projeto). Resolver usuário por `sub` em `usuarios`; se não existir ou inativo → 403. Empresa sempre vem desse registro.

```
GET    /api/health
GET    /api/me
GET    /api/transportadoras?ativo=
POST   /api/transportadoras
PUT    /api/transportadoras/{id}
PATCH  /api/transportadoras/{id}/ativo
GET    /api/transportadoras/{id}/tabelas
POST   /api/transportadoras/{id}/tabelas
PUT    /api/tabelas-frete/{id}
GET    /api/fretes?status=&busca=&atrasados=&pagina=&tamanho=
GET    /api/fretes/{id}                       (inclui propostas, histórico, documentos)
POST   /api/fretes
PUT    /api/fretes/{id}
POST   /api/fretes/{id}/cotacao/simular
POST   /api/fretes/{id}/propostas             (MANUAL)
DELETE /api/fretes/{id}/propostas/{propostaId}
POST   /api/fretes/{id}/propostas/{propostaId}/selecionar
POST   /api/fretes/{id}/status                { novoStatus, observacao }
GET    /api/fretes/{id}/documentos
POST   /api/fretes/{id}/documentos
GET    /api/dashboard?de=&ate=
GET    /api/usuarios                          (ADMIN)
POST   /api/usuarios                          (ADMIN; cria no Supabase Auth via Admin API + registro local; e-mail duplicado → 409 "E-mail já está em uso")
```
Dashboard retorna: fretes ativos, entregues no período, % entregues no prazo, custo total, custo médio por frete, tempo médio de entrega (dias), fretes atrasados, ranking por transportadora (qtd, custo médio, % no prazo).
Swagger habilitado em Development. CORS só para a origem do front (variável de ambiente).
Coleção Postman exportada em `docs/testes/tms.postman_collection.json`.

## Etapa 5 — Front (apps/web)
- Criar `src/services/api.ts` (fetch tipado, injeta token, trata ProblemDetails) e remover localStorage como fonte de dados de negócio.
- Login com supabase-js; rota protegida; logout; mensagem "E-mail ou senha incorretos".
- Telas: Dashboard, Fretes (lista com filtros, badge de status e de atraso), Novo/Editar Frete, Detalhe do Frete (abas: Cotação, Histórico, Documentos), Transportadoras (lista, form, tabelas de frete), Usuários (só ADMIN).
- Cotação no detalhe: botão "Calcular pela tabela", "Registrar proposta recebida" (manual), lista ordenável por menor preço / menor prazo, destaque do mais barato e do mais rápido, botão "Escolher".
- Ações de status mostram somente as transições permitidas para o status atual.
- Estados de carregando, vazio e erro em todas as listas. Erros de validação aparecem no campo.
- Preservar layout e identidade visual atuais; remover telas sem função no MVP (Produtos, Tarifação) do menu.
- Opcional, se sobrar tempo: autopreencher cidade/UF pelo CEP (ViaCEP), sem bloquear o formulário se falhar.

## Etapa 6 — Deploy
- API: Dockerfile em apps/api; Render Web Service. Variáveis: `ConnectionStrings__Default` (usar a connection string do **pooler** do Supabase, compatível com IPv4), `Supabase__Url`, `Supabase__JwksUrl` ou `Supabase__JwtSecret`, `Supabase__ServiceRoleKey`, `Cors__Origin`.
- Web: Vercel com root `apps/web`. Variáveis: `VITE_API_URL`, `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`. Rewrite de SPA.
- Documentar no README: URLs, variáveis, como rodar local, e o aviso de que o plano gratuito do Render "dorme" (abrir a API antes de demonstrar).

## Etapa 7 — Evidências
Criar `docs/evidencias/README.md` com checklist de prints a capturar e `docs/evidencias/consultas.sql` com consultas "antes/depois" para: criar frete, simular cotação, selecionar proposta, mudar status (histórico), cadastrar transportadora.
Atualizar `docs/testes/cenarios.md` com cenários Dado/Quando/Então para RF01–RF06, cada um com campo "Resultado obtido" em branco (a equipe preenche ao executar).

## Critérios de aceite do MVP v1
1. Fluxo completo funciona na URL pública: login → criar frete → calcular cotação → registrar proposta manual → escolher → coletado → entregue → aparece no dashboard.
2. Recarregar a página não perde dados (vêm do banco).
3. Usuário de uma empresa nunca vê dados de outra (testar com 2 empresas no seed).
4. Transição inválida retorna 409 e a interface não oferece a ação.
5. `dotnet test` verde.
6. Nenhum segredo no repositório.
