# Auditoria técnica e de produto

Data da auditoria: 3 de agosto de 2026  
Escopo: toda a solução suportada e inspeção dos componentes legados  
Critério: adequação para comercialização em milhares de lan houses

## Resumo executivo

O projeto evoluiu de um protótipo funcional para uma base local mais segura e coerente, mas ainda não deve ser comercializado em massa como versão 1.0. Os maiores riscos imediatos encontrados foram dependências com advisories altos, SQLite nativo vulnerável, credenciais expostas na UI, mensageria sem confirmação, solicitações duplicáveis, arquivos locais sujeitos a corrupção, falta de limites HTTP, comandos remotos perigosos ainda aceitos pelo serviço e uma interface que anunciava recursos inexistentes.

Esses riscos foram corrigidos no fluxo suportado. Permanecem dívidas estruturais relevantes: serviço de aplicação muito grande, code-behind extenso, onboarding inicial ainda baseado em arquivo local, autorização pouco granular, ausência de TLS/identidade forte por estação, migração de banco sem pipeline formal e artefatos gerados antigos ainda rastreados no histórico.

## Achados e tratamento

| Severidade | Achado | Impacto comercial | Tratamento |
|---|---|---|---|
| Crítica | dependências transitivas com advisories altos | DoS, corrupção/memória e risco conhecido de supply chain | versões centralizadas; EF Core 8.0.29; `System.Text.Json` e cache corrigidos; `winsqlite3`; auditoria NuGet limpa |
| Crítica | Server marcava mensagens como entregues antes do ACK | perda silenciosa de comandos e avisos em falha de rede | ACK no heartbeat, redelivery e deduplicação |
| Alta | reenvio de fila criava solicitações duplicadas | aprovações ou operações repetidas | `RequestId` idempotente e teste de replay |
| Alta | JSON local sobrescrito diretamente | estado/fila corrompidos em queda de energia | gravação atômica e preservação de arquivo corrompido |
| Alta | comandos de reinício/logout/limpeza aceitos no serviço | superfície perigosa e comportamento inesperado | whitelist de comandos seguros no caso de uso |
| Alta | nenhuma proteção contra tentativa repetida | força bruta de senha e PIN | rate limiting por IP no login e API |
| Alta | duas sessões podiam iniciar em corrida | cobrança e estado inconsistentes | índice parcial único e resposta de concorrência |
| Alta | credenciais iniciais fixas e expostas | acesso previsível e vazamento local | senha/PIN aleatórios, arquivo local descartável e remoção após troca de senha |
| Média | atualização de tela WPF podia sobrepor chamadas | travamentos e UI instável | gate não bloqueante e recuperação visual |
| Média | heartbeat removia/inseria processos não utilizados | escrita constante e coleta sem propósito | coleta ignorada no fluxo suportado e UI removida |
| Média | manutenção repetia `EnsureCreated` e seeds | consultas periódicas inúteis | inicialização somente no startup |
| Média | banco sem índices compostos essenciais | degradação com histórico crescente | índices de status, máquina, usuário e data |
| Média | interface anunciava controles do Windows inexistentes | quebra de confiança e suporte | textos, tutorial e configurações alinhados ao produto real |
| Média | logs técnicos não separados | diagnóstico difícil e dados misturados | `Admin.log`, `Client.log` e `Server.log` |
| Média | pacote versions espalhadas | upgrades inconsistentes | `Directory.Packages.props` |
| Baixa | navegação sem estado ativo/foco e ações sem confirmação | erros operacionais e acessibilidade | nav ativa, skip link, foco, confirmação e double-submit guard |

## Arquitetura

### Pontos positivos

- projetos suportados têm direção de dependência sem ciclos;
- Domain não depende de UI ou infraestrutura;
- Client não acessa SQLite nem referencia Server;
- Admin e Client têm dados e configurações separados;
- API, painel e servidor embutido compartilham a mesma composição.

### Problemas encontrados

- `CafeManagementService` ainda é um *god service* e mistura muitos contextos;
- `MainForm` e `MainWindow` misturam apresentação, orquestração e tratamento de erros;
- `Contracts.cs` e `Core.cs` concentram tipos demais;
- Client referencia Infrastructure para detalhes compartilhados;
- os antigos `ClientShell` e `ClientAgent` duplicavam conceitos e mantinham caminhos perigosos; foram removidos.

### Alteração estrutural implementada

`AdrenalinaDatabaseInitializer` passou a cuidar de criação, upgrade, índices e seed. `AdrenalinaReportExporter` concentra TXT/Excel/PDF. Isso retira persistência de bootstrap e formatação documental do serviço de negócio e permite testar esses ciclos isoladamente.

Não foram criados seis projetos `Shared.*`. A decisão detalhada está em `ARCHITECTURE.md`: Application e Domain já são os limites corretos; novos assemblies agora aumentariam cerimônia sem reduzir acoplamento. Recomenda-se dividir arquivos internamente e considerar apenas `Adrenalina.Contracts` quando houver versionamento independente do protocolo.

## Segurança

### Verificações

- SQL Injection: consultas EF parametrizadas; `VACUUM INTO` usa interpolação EF;
- XSS: Razor mantém encoding padrão; entradas normalizadas e limitadas;
- CSRF: formulários mutáveis usam antiforgery;
- Open Redirect: login redireciona somente para rota fixa;
- Directory Traversal: paths operacionais são construídos em raízes internas;
- enumeração: login retorna mensagem genérica;
- cookies: HttpOnly, SameSite Strict e política Secure compatível com HTTPS;
- hashes: PBKDF2-SHA512, salt aleatório e comparação constante;
- segredos em logs: logger técnico não recebe senha/PIN nos fluxos revisados;
- autorização: usuários, configurações e relatórios exigem `Admin`; máquinas e sessões aceitam Admin ou Especial.

### Riscos restantes

- o acesso inicial aleatório ainda depende de o operador trocar a senha; falta bloqueio obrigatório de primeiro acesso;
- HTTP na LAN não garante confidencialidade ou integridade contra atacante local;
- a chave da máquina identifica, mas não autentica criptograficamente;
- PIN de quatro dígitos depende fortemente de rate limiting e segurança da rede;
- logs em arquivo não possuem proteção criptográfica contra adulteração.

## Banco e integridade

Implementado:

- WAL, `synchronous=NORMAL`, foreign keys no modelo e timeout;
- índices para consultas recorrentes;
- unicidade de login, nome/chave de máquina e sessão ativa;
- upgrade aditivo que preserva banco existente;
- backup consistente via `VACUUM INTO` e retenção somente durante ação manual.

Pendente:

- migrations formais e ensaio de upgrade entre todas as versões;
- `PRAGMA integrity_check` agendado/acionável e restauração guiada;
- teste de concorrência sustentada;
- política documentada de retenção fiscal e LGPD;
- limpeza opcional da tabela antiga de snapshots de processos em bases já existentes.

## Performance

Melhorias realizadas:

- removida escrita de snapshots sem consumidor;
- removida inicialização do banco a cada tick;
- eliminada consulta de usuário por item do lote;
- índices compostos adicionados;
- bloqueio de refresh WPF evita sobreposição;
- arquivos locais gravados sem buffers compartilhados ou `Task.Run` desnecessário;
- async permanece em I/O, sem `await` artificial no processamento de comandos.

Limites atuais:

- dashboard ainda materializa coleções maiores do que o necessário;
- alguns históricos usam limites fixos, mas não paginação;
- relatórios são construídos integralmente em memória;
- não existe benchmark nem teste de carga para definir capacidade real.

## UX e UI

Implementado:

- Client com tipografia, controles, contraste e textos consistentes;
- configuração reduzida a endereço, identidade da máquina e teste de conexão;
- status offline compreensível e retry automático;
- erros locais não deixam o refresh travado;
- painel com navegação ativa, skip link, foco visível, inputs escuros consistentes, estados vazios, confirmação de encerramento e prevenção de submissão dupla;
- PIN/senhas não são exibidos como texto;
- processos, reinício, logout, limpeza, lista de bloqueio, banda e backup automático removidos da comunicação visual;
- credenciais não são mais copiadas para clipboard pelo Admin.

Pendente:

- validação visual em múltiplos DPIs e tamanhos de tela;
- testes com usuários reais de balcão;
- localização centralizada em resources;
- automação de testes de UI;
- redução de modais e padronização completa de estados busy em todos os botões WinForms/WPF.

## Testes

A suíte segura possui 16 testes após esta auditoria e usa apenas `%TEMP%` com bancos descartáveis. Ela não instala componentes, não altera firewall/Registro, não encerra processos e não solicita elevação.

Cobertura funcional atual:

- inicialização e seed idempotente;
- autenticação, bloqueio e autorização de rotas;
- health check;
- cadastro/sync de máquina;
- login, início, ajuste e fim de sessão;
- aprovação/rejeição de solicitações;
- idempotência e ACK/redelivery;
- rejeição de comando inseguro;
- indisponibilidade de servidor;
- isolamento e corrupção do armazenamento local.
- exportação TXT/Excel/PDF e backup SQLite em diretório temporário.

Não foi gerado percentual de cobertura porque não há coletor de coverage configurado. Relatórios e criação de backup possuem testes básicos; permanecem sem cobertura suficiente os caminhos de erro e restauração, rate limiting sob carga, billing concorrente, UI e upgrades de base antiga.

## Configuração e operação

- versões NuGet centralizadas;
- `launchSettings.json` usa somente loopback HTTP e não abre navegador automaticamente;
- LAN permanece opt-in e requer reinício;
- Admin/Client usam `%LocalAppData%` e não exigem privilégios administrativos;
- scripts de instalação, projetos duplicados e componentes de controle do Windows foram removidos.

## Inventário das alterações e justificativas

| Área | Arquivos | Justificativa técnica |
|---|---|---|
| Solução e pacotes | `Adrenalina.slnx`, `Directory.Packages.props`, `src/Adrenalina.Admin/Adrenalina.Admin.csproj`, `src/Adrenalina.Client/Adrenalina.Client.csproj`, `src/Adrenalina.Infrastructure/Adrenalina.Infrastructure.csproj` | remover projetos legados da solução, centralizar versões, atualizar dependências vulneráveis e usar o SQLite nativo mantido pelo Windows |
| Arquitetura e domínio | `src/Adrenalina.Domain/Core.cs`, `src/Adrenalina.Application/Contracts.cs` | remover modelos mortos, restringir comandos remotos, adicionar IDs idempotentes e confirmações explícitas no protocolo |
| Persistência e serviços | `src/Adrenalina.Infrastructure/AdrenalinaDbContext.cs`, `AdrenalinaDatabaseInitializer.cs`, `AdrenalinaReportExporter.cs`, `CafeManagementService.cs`, `JsonClientRuntimeStore.cs`, `RollingFileLoggerProvider.cs`, `ServiceCollectionExtensions.cs` | separar inicialização e exportação, reforçar integridade/índices, validar domínio, eliminar N+1 e duplicidade, tornar arquivos atômicos e separar logs |
| Admin desktop | `AdminDesktopOptions.cs`, `AdminSettingsDialog.cs`, `AdminTutorialForm.cs`, `EmbeddedAdminServer.cs`, `MainForm.cs`, `Program.cs` | LAN opt-in, primeiro acesso seguro, mensagens honestas, logs próprios e remoção de atalhos para funções inexistentes/perigosas |
| Client desktop | `App.xaml`, `App.xaml.cs`, `ClientConnectionOptions.cs`, `ClientHostFactory.cs`, `ClientOptionsStore.cs`, `ClientServerGateway.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` | simplificar preparação, melhorar consistência visual, evitar refresh concorrente, recuperar configuração corrompida e implementar retry/ACK/deduplicação |
| Server e API | `AdrenalinaServerBootstrap.cs`, `Controllers/Api/ClientSyncController.cs`, `Controllers/AuthController.cs`, `Controllers/MachinesController.cs`, `Controllers/ReportsController.cs`, `Controllers/SettingsController.cs`, `Controllers/UsersController.cs`, `Properties/launchSettings.json`, `ViewModels/LoginViewModel.cs`, `ViewModels/MachinesPageViewModel.cs` | limites HTTP e rate limiting, IP observado pelo servidor, autenticação/autorização por perfil, validação e defaults locais seguros |
| Painel web | `Views/Auth/Login.cshtml`, `Views/Auth/AccessDenied.cshtml`, `Views/Machines/Index.cshtml`, `Views/Reports/Index.cshtml`, `Views/Sessions/Index.cshtml`, `Views/Settings/Index.cshtml`, `Views/Shared/_Layout.cshtml`, `Views/Tutorial/Index.cshtml`, `Views/Users/Index.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/site.js` | acessibilidade, estados vazios, feedback, confirmação, proteção contra duplo envio e remoção visual de opções sem implementação |
| Testes | `tests/Adrenalina.Tests/Adrenalina.Tests.csproj`, `Usings.cs`, `TestEnvironment.cs`, `SecurityTests.cs`, `ManagementFlowTests.cs`, `ClientIsolationTests.cs`, `ServerApiTests.cs` | criar uma suíte isolada em `%TEMP%`, sem ações administrativas, cobrindo os fluxos críticos e regressões corrigidas |
| Documentação | `README.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `CHANGELOG.md`, `AUDIT_REPORT.md` | documentar operação, limites, decisões, evolução e critérios reais para comercialização |

Removidos: `scripts/install-client.ps1`, `scripts/uninstall-client.ps1`, `ClientServiceWorker.cs`, `ClientWatchdogRunner.cs`, `NativeMethods.cs`, `WindowsKioskManager.cs` e todos os fontes/configurações de `Adrenalina.ClientAgent` e `Adrenalina.ClientShell`. Eram caminhos duplicados ou capazes de alterar sessão, processos, Registro, inicialização e firewall; não faziam parte do fluxo seguro suportado. Como estão versionados no Git, a remoção permanece recuperável enquanto não houver commit destrutivo de histórico.

## Avaliação objetiva após as correções

| Dimensão | Nota | Justificativa resumida |
|---|---:|---|
| Arquitetura | 68/100 | bons limites de projeto, porém serviço e code-behind ainda grandes |
| Código | 72/100 | compilação limpa, fluxo mais defensivo; organização interna ainda concentrada |
| Segurança | 72/100 | dependências e credenciais endurecidas; HTTP e identidade fraca da estação ainda impedem produção ampla |
| Performance | 74/100 | gargalos óbvios removidos e índices adicionados; sem teste de carga |
| UX | 76/100 | fluxos mais honestos e claros; falta validação com usuários reais |
| UI | 73/100 | consistência, foco e responsividade melhores; falta QA visual/DPI completo |
| Escalabilidade | 55/100 | adequada a instalação local pequena/média; SQLite e consultas não foram validados em grande escala |
| Confiabilidade | 76/100 | idempotência, ACK, atomicidade, WAL e recovery; falta restore testado e HA |
| Testabilidade | 67/100 | 16 testes seguros de fluxo; ausência de coverage/UI/carga |
| Manutenibilidade | 64/100 | docs e inicializador melhoram; god service e arquivos grandes ainda pesam |

Nota global aritmética: **70/100**.

## Veredito

O Adrenalina está mais próximo de um beta técnico controlado do que de uma versão comercial massiva. Pode ser demonstrado e validado em ambiente piloto isolado, com credenciais trocadas e rede confiável. Para venda a milhares de lan houses, os bloqueadores mínimos são onboarding sem senha padrão, TLS/identidade por estação, autorização por políticas, migrations/restore comprovados, divisão dos serviços centrais e testes de carga/upgrade/UI.
