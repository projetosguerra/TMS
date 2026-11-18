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

- Testes automatizados das regras de domínio, previstos na Etapa 3.
- Cenários funcionais RF01–RF06, incluindo os registros do Apêndice F para RF02, RF05 e RF06.
- Consultas SQL antes/depois, dependentes da implementação da persistência.
- Comprovação de homologação e produção publicadas.
- Capturas para o relatório acadêmico e identificação das figuras.
- Aceite de avaliadores do protótipo e avaliação de usabilidade com participantes.

## Risco de dependências

A auditoria identificou ReDoS em picomatch, propagado pela cadeia de ferramentas de desenvolvimento Vite/Tailwind. A decisão e o compromisso de reavaliação estão registrados em `../decisoes.md`. O servidor frontend utilizado na validação foi iniciado em loopback.
