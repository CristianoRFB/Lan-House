# Changelog

Todas as alterações relevantes deste projeto serão documentadas neste arquivo. O formato segue *Keep a Changelog*; o projeto ainda não possui release comercial estável.

## [Unreleased]

### Implantação de produção

- adicionada rejeição de HTTP aberto quando o servidor tenta expor a LAN em produção;
- adicionado carregamento de certificado por thumbprint do Windows e configuração externa do Admin;
- adicionados scripts de publicação self-contained, instalação com firewall restrito, rollback, cópia off-machine e restauração guiada;
- adicionada checklist de entrega para registrar evidências do ambiente do cliente;
- corrigido o restore do runtime `win-x64` no pacote de release e removida a combinação inválida de self-contained/RID do build do Admin.

### Segurança

- removidos do índice artefatos de build, logs e banco de runtime; adicionada proteção no `.gitignore`;
- adicionada versão explícita ao contrato Client/Server e rejeição de versões incompatíveis;
- adicionado carregamento de certificado Kestrel por configuração externa e bloqueio de binding HTTPS sem certificado;
- criada a fronteira `IStationEnforcementService` com provider seguro no-op para desenvolvimento e validação futura autorizada;
- restringido `AllowedHosts` por padrão, com wildcard somente quando o binding LAN é explicitamente habilitado;
- atualizadas e centralizadas as dependências .NET;
- substituído o SQLite nativo embutido vulnerável pelo `winsqlite3` atualizado pelo Windows;
- adicionados rate limiting por IP, limites de corpo/cabeçalho e timeouts HTTP;
- endurecidos cookie, Data Protection e validação de hash;
- substituídas as credenciais fixas por acesso inicial aleatório e descartável;
- removidas as contas de demonstração do seed de produção;
- restritos usuários, configurações e relatórios de auditoria ao papel `Admin`;
- ocultadas credenciais da tela de login e da área de transferência;
- rejeitados comandos remotos incompatíveis com o Client seguro;
- desativado por padrão o target de build que encerrava processos;
- removidos os componentes de serviço, watchdog, quiosque, APIs nativas, scripts de instalação e projetos Client duplicados.
- removido definitivamente o target MSBuild que encerrava processos.

### Confiabilidade

- solicitações do Client agora têm identificador idempotente;
- comandos e avisos são reenviados até confirmação explícita;
- fila e estado JSON usam substituição atômica;
- arquivos JSON corrompidos são preservados e recriados com estado seguro;
- heartbeat desconhecido não cadastra máquinas automaticamente;
- login offline não armazena PIN nem cria autenticação local;
- backup é executado somente por solicitação explícita.

### Banco de dados

- extraída a inicialização para `AdrenalinaDatabaseInitializer`;
- extraída a geração de relatórios para `AdrenalinaReportExporter`;
- habilitados WAL, chaves estrangeiras em novas bases e timeout de escrita;
- adicionados índices de operação e integridade;
- impedidas múltiplas sessões ativas na mesma máquina;
- seed tornado idempotente por login;
- incluído upgrade aditivo para bloqueio de usuário.

### Interface

- refinados estilos, foco, navegação ativa, estados vazios e confirmação de ações;
- protegidos PIN e senha nos formulários;
- removidas promessas visuais de recursos não implementados;
- simplificada a configuração do Client e corrigidas mensagens em português;
- evitada sobreposição do timer de atualização da UI;
- mantida a janela do Client responsiva e fechável.

### Observabilidade

- separados `Admin.log`, `Client.log` e `Server.log`;
- adicionada rotação simples a 10 MiB;
- mantida a auditoria de negócio no banco, separada de falhas técnicas.

### Testes e documentação

- adicionada suíte xUnit segura com fluxos de banco, autenticação, máquinas, sessões, solicitações, rede e armazenamento;
- adicionados testes de idempotência, redelivery, rejeição de comando e recuperação de corrupção;
- criados `ARCHITECTURE.md`, `ROADMAP.md`, `CHANGELOG.md` e `AUDIT_REPORT.md`;
- atualizado o `README.md` com implantação e limitações reais.
