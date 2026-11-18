# Decisões técnicas

## 2026-11-17 — Estrutura inicial autorizada

O responsável autorizou criar o frontend do zero na Etapa 1 porque o protótipo não está disponível. O aceite desta etapa passa a ser o funcionamento da página inicial, sidebar, header e layout responsivo.

A arquitetura aprovada permanece o monólito modular descrito em AGENTS.md. Domain não referencia outros projetos. Infrastructure referencia Domain. Api referencia Domain e Infrastructure. Tests referencia Domain.

## 2026-11-17 — Ferramentas do frontend

Vite 6 e plugin React 4 foram escolhidos por compatibilidade com Node.js 20.16.0. React 19 e TypeScript 5.9 compõem a aplicação. Tailwind CSS 3 usa PostCSS e Autoprefixer para produzir o CSS. O package-lock.json fixa as versões resolvidas.

Essas linhas de ferramentas devem ser reavaliadas quando a equipe atualizar o Node.js. O npm informa vulnerabilidades conhecidas durante a instalação e elas devem ser registradas conforme o resultado real.

## 2026-11-17 — Preparação do backend

SDK .NET 10.0.404 instalado com autorização. Solução no formato TMS.sln exigido pela especificação. Os pacotes Microsoft.NET.Test.Sdk, xunit e xunit.runner.visualstudio preparam a descoberta e execução dos futuros testes de domínio.

## Pendências para as próximas etapas

- Esclarecer empresa_id em propostas, historico_status e documentos_fiscais, considerando a obrigatoriedade de isolamento em AGENTS.md.
- Compatibilizar seed de uma empresa com o aceite de isolamento usando duas empresas.
- Definir constraints por campo, respeitando volume_m3 igual a zero.

## 2026-11-17 — Risco residual de dependências no encerramento da Etapa 1

A revisão executou npm audit, que identificou 11 alertas altos, e npm audit --omit=dev, que identificou zero vulnerabilidades. Os alertas estão relacionados à cadeia de dependências de desenvolvimento, incluindo Vite, Tailwind CSS e seus pacotes transitivos.

A vulnerabilidade de picomatch envolve negação de serviço por expressão regular (ReDoS), com consumo excessivo de CPU ao processar padrões maliciosos. A revisão encontrou picomatch 2.3.2 e 4.0.7 na árvore de dependências. Referência pública do aviso de segurança [GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf).

Não há correção compatível confirmada para toda a cadeia neste momento. A atualização principal do Tailwind não está autorizada. O encerramento desta etapa mantém o risco registrado e os alertas visíveis, conforme orientação do responsável.

O risco será reavaliado antes da publicação e sempre que houver correções disponíveis. Qualquer alteração de dependências exige avaliação de compatibilidade, autorização aplicável e nova execução das auditorias e dos builds.
