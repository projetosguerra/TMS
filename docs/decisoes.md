# Decisões técnicas

## 2026-11-17 — Estrutura inicial autorizada

O responsável autorizou criar o frontend do zero na Etapa 1 porque o protótipo não está disponível. O aceite desta etapa passa a ser o funcionamento da página inicial, sidebar, header e layout responsivo.

A arquitetura aprovada permanece o monólito modular descrito em AGENTS.md. Domain não referencia outros projetos. Infrastructure referencia Domain. Api referencia Domain e Infrastructure. Tests referencia Domain.

## 2026-11-17 — Ferramentas do frontend

Vite 6 e plugin React 4 foram escolhidos por compatibilidade com Node.js 20.16.0. React 19 e TypeScript 5.9 compõem a aplicação. Tailwind CSS 3 usa PostCSS e Autoprefixer para produzir o CSS. O package-lock.json fixa as versões resolvidas.

Essas linhas de ferramentas devem ser reavaliadas quando a equipe atualizar o Node.js. O npm informa vulnerabilidades conhecidas durante a instalação e elas devem ser registradas conforme o resultado real.

## 2026-11-17 — Preparação do backend

SDK .NET 10.0.404 instalado com autorização. Solução no formato TMS.sln exigido pela especificação. Os pacotes Microsoft.NET.Test.Sdk, xunit e xunit.runner.visualstudio preparam a descoberta e execução dos futuros testes de domínio.

## 2026-11-17 — Risco residual de dependências no encerramento da Etapa 1

A revisão executou npm audit, que identificou 11 alertas altos, e npm audit --omit=dev, que identificou zero vulnerabilidades. Os alertas estão relacionados à cadeia de dependências de desenvolvimento, incluindo Vite, Tailwind CSS e seus pacotes transitivos.

A vulnerabilidade de picomatch envolve negação de serviço por expressão regular (ReDoS), com consumo excessivo de CPU ao processar padrões maliciosos. A revisão encontrou picomatch 2.3.2 e 4.0.7 na árvore de dependências. Referência pública do aviso de segurança [GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf).

Não há correção compatível confirmada para toda a cadeia neste momento. A atualização principal do Tailwind não está autorizada. O encerramento desta etapa mantém o risco registrado e os alertas visíveis, conforme orientação do responsável.

O risco será reavaliado antes da publicação e sempre que houver correções disponíveis. Qualquer alteração de dependências exige avaliação de compatibilidade, autorização aplicável e nova execução das auditorias e dos builds.

## 2026-11-19 — Persistência e domínio das Etapas 2 e 3

- Propostas, histórico e documentos têm empresa_id obrigatório. As FKs compostas incluem a empresa e a seleção referencia uma proposta do próprio frete. Os filtros de leitura do contexto usam a empresa do usuário autenticado. Sem empresa definida, as consultas de negócio retornam vazias.
- As tabelas usam RLS sem políticas públicas para impedir acesso direto pelos papéis de cliente do Supabase. A API acessará o banco por uma conexão de servidor com acesso às tabelas (por exemplo, o proprietário). O filtro de empresa continua necessário na API.
- O seed explícito de Development usa uma transação e um lock transacional do PostgreSQL. A empresa principal recebe quatro parceiros e doze fretes. Uma segunda empresa tem usuário, parceiro, tabela e frete próprios para exercitar o isolamento. Os UUIDs dos usuários do seed são fictícios. O cadastro real usará o UUID do Supabase Auth.
- Peso, tarifas, valores informados e prazos são positivos. Volume aceita zero, com precisão de três casas. Valores opcionais ausentes são NULL.
- Uma transportadora pode ter uma tabela ativa por rota. Isso define qual tarifa usar na simulação. O fator padrão de 300 kg/m³ é substituível por simulação. O arredondamento monetário usa duas casas com empates afastados de zero.
- Dias corridos a partir da data UTC de seleção e prazos de exemplo do seed são hipóteses do MVP. A troca enquanto contratado recalcula o prazo. A última proposta removida mantém o frete em cotação, conforme a máquina de estados.
- O contexto usa xmin do PostgreSQL para detectar gravações concorrentes do frete. A API deverá carregar as propostas do frete antes de cotar, remover ou selecionar e tratar conflitos de concorrência.

## 2026-11-19 — Acesso HTTP e ensaio de persistência

- A API verifica assinatura assimétrica, emissor do projeto, audiência authenticated, validade, role de usuário e sub UUID. As chaves públicas vêm do JWKS via HTTPS. O cadastro ativo no banco fornece a empresa e o perfil em cada requisição protegida.
- RLS está habilitada sem políticas de acesso direto. Os papéis comuns sujeitos a RLS ficam sem acesso às linhas. O proprietário e papéis BYPASSRLS podem lê-las. Com a conexão de servidor planejada, o isolamento de leitura depende do filtro do contexto por empresa. As FKs compostas protegem os vínculos entre registros. A API não propaga o JWT para a sessão SQL.
- O novo projeto de testes mantém as dependências HTTP e de persistência fora de Tms.Tests. O provedor InMemory e chaves efêmeras exercitam o contrato HTTP offline. Os testes de migration e integridade usam Npgsql e um esquema exclusivo em banco local de teste. A autenticação com tokens emitidos pelo projeto tem um ensaio separado de leitura.
