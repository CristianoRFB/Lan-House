# Lan House Manager

Sistema base para gerenciamento de lan house com foco em operação **cliente/servidor no Windows 10**, suporte offline e painel administrativo centralizado.

## Visão geral

Esta base implementa o **núcleo inicial** do projeto solicitado:

- API local em **FastAPI** para o servidor central do admin.
- Modelos de domínio para usuários, máquinas, sessões, anotações, comandos, notificações e PlayStation.
- Regras de negócio para perfis **Admin**, **Ghost**, **Usuário Especial** e **Usuário Comum**.
- Motor de sessão com controle de tempo, alertas de 10/5/1 minuto, bloqueio por fim de saldo e exceções para perfis privilegiados.
- Fila offline em **SQLite** para o agente cliente continuar operando sem rede e sincronizar depois.
- Política de backup diário após **20:30** com retenção de **60 dias**.
- Agregação de relatórios diários/semanais/mensais para PCs e consoles separadamente.
- Estrutura preparada para empacotamento futuro com WebView2, PyInstaller e serviço Windows.

## Estrutura

```text
backend/
  app/
    api/
    domain/
    services/
tests/
```

## Perfis suportados

- **Admin**: acesso total.
- **Ghost**: máquina livre, sem contagem de tempo.
- **Especial**: uso liberado, saldo negativo permitido, sem limite de anotações.
- **Comum**: usa saldo pré-pago, pode ser bloqueado ao fim do tempo e respeita limites de anotação.

## Fluxo arquitetural previsto

### Servidor central (admin)

Responsável por:

- cadastro de usuários e máquinas;
- gestão de saldo, sessões e anotações;
- comandos remotos e notificações;
- relatórios, calendário financeiro e estatísticas;
- backups e trilha de auditoria.

### Agente cliente (cada PC)

Responsável por:

- login local do usuário;
- cronômetro e detecção de ociosidade;
- fila offline em SQLite;
- sincronização posterior com o servidor;
- exibição de mensagens e bloqueios.

## Endpoints iniciais

Ao instalar dependências e iniciar a API, os endpoints iniciais ficam disponíveis em:

- `GET /health`
- `GET /api/summary`
- `POST /api/session/evaluate`
- `POST /api/offline/events`
- `GET /api/offline/events`
- `GET /api/reports/usage`

## Execução local

```bash
python -m unittest discover -s tests -v
```

> Observação: a API FastAPI foi estruturada, mas a execução do servidor depende da instalação das dependências listadas em `pyproject.toml`.

## Próximos passos sugeridos

1. Persistir entidades no PostgreSQL do admin com SQLAlchemy/Alembic.
2. Criar serviço Windows do cliente com watchdog.
3. Implementar comunicação em WebSocket para comandos em tempo real.
4. Criar painel Vue 3 para admin e tela bloqueada do cliente via WebView2.
5. Adicionar exportação PDF/Excel/TXT e captura remota de tela.
