# Estrutura adicional para evolução do projeto

Além da base preservada em `backend/app/` para facilitar o merge com `main`, esta branch mantém uma evolução separada em novas pastas:

- `admin/`: painel do administrador servido por FastAPI e aberto no navegador;
- `client/`: runtime local do agente e interface inicial da estação do cliente;
- `shared/`: entidades e serviços compartilhados entre admin e cliente.

## Objetivo desta organização

Essa separação permite evoluir o sistema em camadas sem alterar imediatamente os caminhos já existentes no `main`, reduzindo o risco de conflitos enquanto a migração é feita gradualmente.
