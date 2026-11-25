# Preparação de deploy — TMS Embarcador

Esta configuração prepara o repositório para Vercel + Render + o projeto Supabase TMS já existente. Nenhum serviço foi criado ou publicado nesta etapa. Não usar migrations, seed ou Service Role.

Validação local nesta etapa: `apps/web/vercel.json` parseou como JSON válido. Não foi possível fazer `docker build` porque o Docker Engine local não estava em execução; o YAML do Render ainda precisa ser validado pelo Dashboard/Blueprint antes de qualquer criação de serviço.

## Render — API

Foi preparado `render.yaml` na raiz e `apps/api/Dockerfile`. Configure o serviço com a raiz do repositório, `apps/api/Dockerfile` e contexto `apps/api` (os mesmos caminhos estão no Blueprint). O Dockerfile faz publish Release em estágios .NET 10 e inicia Kestrel em `0.0.0.0:$PORT`. O health check HTTP do Blueprint consulta `/api/health`.

No Dashboard do Render, inserir os valores secretos diretamente no serviço:

| Configuração | Valor/fonte |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` (já previsto no Blueprint) |
| `Supabase__Url` | URL HTTPS do projeto existente TMS (já prevista no Blueprint) |
| `ConnectionStrings__Default` | Session Pooler do projeto TMS, porta 5432, banco `postgres`, usuário `postgres.<project-ref>`, senha atual do banco, `SSL Mode=VerifyFull` e `Root Certificate=/etc/secrets/prod-ca-2021.crt`. Nunca reutilizar a connection string local com caminho Windows. |
| Secret File `prod-ca-2021.crt` | CA de servidor baixada em **Supabase Dashboard → Database Settings → SSL Configuration**. O Dockerfile dá ao usuário da API leitura do grupo usado pelos Secret Files do Render. Não commitar a CA nem a senha. |
| `Cors__Origin` | Origem HTTPS exata da aplicação Vercel, sem barra final, por exemplo `https://<dominio-vercel>`. Definir depois de conhecer o domínio. Não usar `*`. |

O `render.yaml` marca a connection string e a origem CORS com `sync: false`, para inserir manualmente. A saúde `/api/health` é liveness do processo e não testa o PostgreSQL; após publicar, validar login, `GET /api/me` e as consultas autenticadas como readiness funcional.

## Vercel — frontend

Criar o projeto apontando para este repositório, com **Root Directory** `apps/web`. Build: `npm run build`; Output Directory: `dist`; Install Command: `npm ci`. O `apps/web/vercel.json` redireciona rotas da SPA para `index.html`.

Definir no ambiente **Production** do projeto Vercel antes do deploy final:

| Variável | Valor |
| --- | --- |
| `VITE_API_URL` | URL HTTPS do serviço Render, sem barra final. |
| `VITE_SUPABASE_URL` | URL HTTPS do projeto Supabase TMS existente. |
| `VITE_SUPABASE_PUBLISHABLE_KEY` | Publishable Key do projeto existente. É a chave pública de cliente; não usar Secret/Service Role Key. |

`VITE_*` é incorporado ao bundle no build. Alterar essas variáveis exige novo build/deploy da web. Configure o domínio de produção na Vercel, replique sua origem exata em `Cors__Origin` no Render e redeploy/reinicie a API. URLs de Preview Vercel não estão automaticamente liberadas pelo CORS exato.

## Ordem operacional após autorização de publicação

1. Criar o serviço Render a partir do Blueprint; fornecer connection string e CA na configuração segura; preencher `Cors__Origin` quando o domínio Vercel estiver definido.
2. Confirmar deploy saudável e `GET https://<api-render>/api/health` com HTTP 200.
3. Criar/conectar o projeto Vercel em `apps/web`, definir as três variáveis `VITE_*` e publicar.
4. Atualizar `Cors__Origin` para a origem pública exata e reiniciar/redeployar a API; redeployar a web se `VITE_API_URL` foi atualizada.
5. Pela URL pública, realizar login Supabase real, confirmar `/api/me`, dashboard e listagem autenticada de transportadoras. Guardar URLs, status HTTP e capturas sem tokens/segredos.

Não sincronizar nem aplicar migrations, seed, alterações de schema ou dados ao Supabase. O banco de desenvolvimento existente já está populado. Rever separadamente o aviso de conflito EF Core 10.0.4/10.0.8 antes de abrir publicação para uso mais amplo.

## Cliques nos painéis

### Render

1. No Dashboard, clique **New + → Blueprint** e conecte `projetosguerra/TMS` na branch `feat/continuacao-inicio`.
2. Confirme o Blueprint `render.yaml`. Ele cria um **Web Service** Docker no plano **Free**.
3. Revise os caminhos: raiz do repositório `.`, Dockerfile `apps/api/Dockerfile`, contexto Docker `apps/api`, health check `/api/health`.
4. Preencha `ConnectionStrings__Default` e `Cors__Origin` nos campos solicitados para variáveis `sync: false`. A origem CORS pode ser atualizada após obter a URL Vercel. Não cole credenciais no chat, Git ou logs.
5. Abra o serviço → **Environment** e confirme `ASPNETCORE_ENVIRONMENT` e `Supabase__Url`. Em **Secret Files**, clique **Add Secret File**, nomeie `prod-ca-2021.crt`, cole o certificado CA baixado no Supabase e salve. A connection string deve apontar para `/etc/secrets/prod-ca-2021.crt`.
6. Confirme **Free** em **Settings**. Quando estiver pronto para publicar, use **Manual Deploy → Deploy latest commit**.

O plano Free atende à demonstração acadêmica e suspende o serviço após 15 minutos sem tráfego; a primeira requisição pode levar cerca de um minuto para acordar. Se a apresentação exigir resposta imediata, selecione o plano pago básico e confira o preço no painel antes de confirmar.

### Vercel

1. No Dashboard, clique **Add New… → Project**, localize `projetosguerra/TMS` e clique **Import**.
2. Em **Configure Project**, defina **Root Directory** `apps/web`, **Framework Preset** `Vite`, **Build Command** `npm run build`, **Output Directory** `dist` e **Install Command** `npm ci`.
3. Em **Environment Variables**, adicione `VITE_API_URL`, `VITE_SUPABASE_URL` e `VITE_SUPABASE_PUBLISHABLE_KEY` para **Production**. Use apenas a chave publishable, nunca Secret/Service Role.
4. Em **Git**, escolha `feat/continuacao-inicio` como **Production Branch**. Clique **Deploy** quando variáveis e domínio estiverem corretos.
5. Copie a origem HTTPS exata publicada (sem caminho nem barra final) para `Cors__Origin` em Render → serviço → **Environment**; salve e redeploye a API. `VITE_API_URL` deve ser a URL HTTPS do serviço Render; redeploye a Vercel se alterar essa variável.

O plano Vercel Hobby é gratuito para uso pessoal/não comercial; confirme a elegibilidade da conta. Para conta vinculada a organização ou uso fora desses termos, escolha o plano indicado pela Vercel.

## Checklist de evidências acadêmicas ainda faltantes

O inventário foi conferido em `docs/evidencias/README.md`. Este checklist lista somente itens pendentes; não substitui a matriz `docs/testes/rastreabilidade.md`.

### Seção 7.1 — implementação e testes

- [ ] Captura real da tela de login (sem credenciais preenchidas, tokens ou dados pessoais desnecessários).
- [ ] Captura do relatório final de testes/build; os arquivos TXT/JSON atuais são evidência textual, não screenshot.
- [ ] Captura segura do banco/consultas em cliente visual ou painel SQL; os arquivos atuais são SQL/JSON, sem captura de ferramenta.
- [ ] Opcional para fechamento visual: tela dedicada do autônomo QA inativo e validação da chave fiscal inválida.

### Seção 7.2 — desenvolvimento, homologação e produção

- [ ] Registrar o ambiente local e suas versões/configurações sem segredos (parte local está descrita nos documentos existentes).
- [ ] Criar e comprovar homologação, se a equipe realmente a configurar; não há URL ou captura desse ambiente.
- [ ] Criar e comprovar produção somente após autorização; ainda não há URLs públicas nem resultados de deploy.
- [ ] Capturar `/api/health` da API publicada e registrar versão/URL observada.

### Apêndice F — RF02, RF05 e RF06

- [ ] RF02: anexar a evidência dedicada da edição/estado inativo do parceiro QA, caso o apêndice exija sequência visual completa; SQL atual comprova `ativo=false` do autônomo.
- [ ] RF05: manter explícito que a captura e o registro são metadados com chave fictícia; obter captura específica do erro de validação se solicitada pelo roteiro acadêmico. XML/PDF não é upload implementado.
- [ ] RF06: anexar a tela antes/depois junto com o SQL/JSON e explicar que os dados de demonstração alteraram indicadores; estado sem dados e intervalo alternativo seguem sem evidência.
- [ ] Identificar figuras e relacionar cada uma aos cenários e resultados observados, sem atribuir uso real ou feedback não coletado.

### Matriz e aplicação

- [ ] Completar a evidência visual de logout/reload e login inválido controlado; manter os testes de mensagem como evidência automatizada separada.
- [ ] Fazer capturas de tablet/mobile e console/Network; nenhuma conclusão sobre essas verificações deve ser inferida do build.
- [ ] Após autorização e publicação: capturas reais do login, `/api/health`, `/api/me`, dashboard e transportadoras nas URLs públicas; ocultar cabeçalhos de autorização.
- [ ] Revisar anonimização de UUIDs/identificadores nos JSONs antes de anexá-los fora da equipe.
