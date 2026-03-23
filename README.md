# Lan House Manager

Sistema organizado em três áreas principais para atender ao fluxo solicitado:

- `admin/`: servidor e painel web do administrador, iniciado pelo navegador.
- `client/`: agente local e interface do cliente da máquina.
- `shared/`: domínio e serviços reutilizados pelos dois lados.

## Estrutura do projeto

```text
admin/
  server.py          # FastAPI que expõe API + painel HTML/CSS/JS do admin
  web/               # assets visuais do admin rodando no navegador
client/
  agent/             # runtime local e integrações offline do cliente
  web/               # interface HTML/CSS da máquina cliente
shared/
  domain/            # entidades, enums e contratos comuns
  services/          # regras de sessão, fila offline, relatórios e backup
tests/
```

## O que foi ajustado neste passo

### Admin via navegador

O admin agora foi estruturado para iniciar como **aplicação web**, servida por FastAPI, com:

- dashboard visual em HTML/CSS/JS;
- cards de resumo;
- tabela de máquinas;
- histórico de notificações;
- envio de comandos remotos pelo navegador.

### Separação admin / cliente / shared

- O código compartilhado foi movido para `shared/`.
- O servidor do admin foi concentrado em `admin/`.
- O runtime do cliente e a interface da estação foram criados em `client/`.
- Foi adicionada uma camada de compatibilidade em `backend/app/` para reduzir conflitos em PRs e manter imports antigos funcionando enquanto a migração acontece.

### Regras de negócio mantidas

Continuam disponíveis no módulo compartilhado:

- controle de perfis `admin`, `ghost`, `special` e `regular`;
- avaliação de sessão e bloqueio ao fim do tempo;
- fila offline em SQLite;
- agregação de relatórios de PCs e PlayStation;
- política de backup após 20:30 com retenção de 60 dias.

## Como iniciar o admin

Com as dependências instaladas, rode:

```bash
uvicorn admin.server:app --reload
```

Depois abra no navegador:

```text
http://127.0.0.1:8000/
```

## Endpoints principais do admin

- `GET /`
- `GET /health`
- `GET /api/dashboard`
- `POST /api/session/evaluate`
- `POST /api/commands`

## Como usar a base do cliente

O runtime local fica em `client/agent/runtime.py` e usa a fila offline compartilhada para guardar eventos quando o servidor central estiver indisponível.

A interface HTML/CSS inicial do cliente está em `client/web/`.

## Testes

```bash
python -m unittest discover -s tests -v
```
