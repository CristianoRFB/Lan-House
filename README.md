# Adrenalina

Sistema de gerenciamento de lan house com dois aplicativos independentes:

- `Adrenalina.Admin`: aplicativo WinForms exclusivo da administração;
- `Adrenalina.Client`: aplicativo WPF exclusivo da estação cliente.

## Segurança do fluxo atual

O cliente principal não controla o Windows. O estado “bloqueado” é somente visual e a janela continua fechável. O executável principal não:

- bloqueia teclado ou mouse;
- esconde a barra de tarefas;
- encerra processos;
- altera o Registro;
- reinicia, desliga ou faz logoff;
- instala serviço ou tarefa agendada;
- modifica firewall ou reserva URL com `netsh`.

Os antigos componentes de quiosque, serviço, watchdog, hooks, scripts de instalação e o target de encerramento de processos foram removidos do repositório durante a auditoria de segurança.

## Arquitetura

| Projeto | Responsabilidade |
|---|---|
| `Adrenalina.Admin` | Inicializa e encerra o servidor embutido, mostra status, URL, painel, backup e configurações do Admin |
| `Adrenalina.Client` | Configuração da estação, login por PIN, estado da sessão, saldo, avisos e solicitações |
| `Adrenalina.Server` | API do cliente, health check, autenticação por cookie e painel administrativo MVC |
| `Adrenalina.Application` | Contratos, DTOs, validações simples e caminhos compartilhados |
| `Adrenalina.Domain` | Entidades e enumerações de negócio |
| `Adrenalina.Infrastructure` | EF Core, SQLite, regras de negócio, seed e persistência |

O Client não referencia `Adrenalina.Server`, não abre SQLite e se comunica somente por HTTP. Admin e Client não compartilham telas nem arquivos de configuração.

## Dados e configurações

Admin:

```text
%LocalAppData%\Adrenalina\Admin\
  adrenalina.db
  admin-app.json
  backups\
  keys\
  logs\Admin.log
  logs\Server.log
```

Client:

```text
%LocalAppData%\Adrenalina\Client\clientsettings.json
%LocalAppData%\Adrenalina\Client\logs\Client.log
%LocalAppData%\Adrenalina\Runtime\<NOME-DA-MAQUINA>\client-state.json
%LocalAppData%\Adrenalina\Runtime\<NOME-DA-MAQUINA>\client-requests.json
```

O servidor aceita `Adrenalina:RootDirectory` para testes ou ambientes controlados. A suíte automatizada usa somente diretórios temporários e bancos descartáveis.

## Funcionalidades do Admin

- instância única sem privilégios administrativos;
- servidor ASP.NET Core embutido com encerramento por ciclo de vida/cancellation token;
- porta padrão `5076`, com fallback para uma porta local livre;
- escuta somente em `127.0.0.1` por padrão;
- rede local opt-in nas configurações, sem alterar o firewall;
- criação idempotente do SQLite e seed sem duplicação;
- upgrade aditivo do esquema, WAL, índices e sessão ativa única por máquina;
- fallback para o navegador padrão quando WebView2 não estiver disponível;
- autenticação por cookie nas páginas administrativas;
- cadastro, edição, consulta e bloqueio de usuários;
- cadastro, edição e consulta de máquinas e suas chaves;
- início, ajuste de tempo e encerramento de sessões;
- saldo e lançamentos financeiros;
- aprovação ou recusa de pedidos de cadastro e tempo adicional;
- histórico, relatórios e auditoria;
- backup somente por ação explícita do administrador.

O endpoint anônimo de disponibilidade é:

```text
GET /health
```

Todas as páginas administrativas exigem autenticação.

## Funcionalidades do Client

- tela de preparação no primeiro uso;
- URL do servidor, nome, chave e tipo da máquina;
- validação da URL e botão `Testar conexão` com timeout;
- sincronização assíncrona e reconexão sem travar a interface;
- solicitações idempotentes e mensagens reenviadas até confirmação;
- indicação clara de online/offline;
- login por usuário e PIN;
- usuário, saldo, anotações, tempo restante e avisos;
- pedido de cadastro;
- pedido de minutos adicionais;
- edição posterior do endereço do servidor;
- configurações armazenadas separadamente do Admin;
- estado “bloqueado” somente visual e seguro.

A chave informada no Client precisa existir no cadastro de máquinas do Admin. O heartbeat não cadastra máquinas desconhecidas automaticamente.

## Primeiro uso

### 1. Iniciar o Admin

```powershell
dotnet run --project src/Adrenalina.Admin
```

Clique em `Iniciar servidor e abrir painel`.

Por padrão, o servidor aceita apenas conexões deste computador. Para clientes em outros computadores, abra `Configurações`, habilite a rede local e reinicie o Admin. Essa opção apenas muda o endereço de escuta; o aplicativo não cria regras de firewall.

### 2. Entrar no painel

Na primeira inicialização, o sistema cria somente a conta `admin`, com senha forte e PIN aleatórios. Consulte:

```text
%LocalAppData%\Adrenalina\Admin\initial-admin-access.txt
```

Entre com os dados do arquivo e altere a senha do `admin` em `Usuários`. Quando a nova senha é salva, o arquivo inicial é removido. Senhas e PINs ficam no banco como PBKDF2 com salt; contas de demonstração não são criadas em produção.

### 3. Cadastrar a máquina

No painel, abra `Máquinas` e cadastre:

- nome da estação;
- chave da máquina;
- tipo (`Pc` ou `Console`);
- grupo e observações opcionais.

Guarde a chave exatamente como cadastrada.

### 4. Preparar o Client

```powershell
dotnet run --project src/Adrenalina.Client
```

Na tela `Preparar Client`:

1. informe a URL mostrada pelo Admin;
2. informe o mesmo nome e a mesma chave cadastrados no painel;
3. use `Testar conexão`;
4. clique em `Salvar e iniciar cliente`.

Se o servidor estiver fora do ar, a configuração ainda pode ser salva e o Client mostrará o erro, permanecendo responsivo e tentando sincronizar novamente.

## Restore, build e testes

Use PowerShell na raiz do repositório:

```powershell
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'

dotnet restore Adrenalina.slnx -m:1
dotnet build Adrenalina.slnx --no-restore -m:1 /p:UseSharedCompilation=false
dotnet test tests/Adrenalina.Tests/Adrenalina.Tests.csproj --no-restore -m:1 /p:UseSharedCompilation=false
```

O `global.json` pede a linha do SDK .NET 10 com roll-forward para feature mais recente, enquanto os projetos têm target `net8.0`/`net8.0-windows`.

A suíte cobre:

- criação inicial e seed repetido do banco;
- autenticação administrativa e conta bloqueada;
- autorização das rotas administrativas;
- health check;
- cadastro, consulta e sincronização de máquina;
- login por PIN;
- início, ajuste e fim de sessão;
- envio, aprovação e recusa de pedidos;
- servidor indisponível;
- leitura/gravação da configuração e runtime do Client;
- recuperação de JSON corrompido, idempotência e confirmação de mensagens;
- rejeição de comandos remotos inseguros;
- separação de caminhos Admin/Client.

## Publicação

Em um computador pessoal ou de implantação controlado:

```powershell
dotnet publish src/Adrenalina.Admin -c Release -r win-x64 --self-contained true -m:1
dotnet publish src/Adrenalina.Client -c Release -r win-x64 --self-contained true -m:1
```

Não é necessário instalar WebView2 para usar o Admin: o navegador padrão é o fallback suportado.

## Limitações conhecidas

- a rede local depende da rede e das políticas já existentes; o sistema não altera firewall;
- o bloqueio da estação é uma representação visual, não um recurso de segurança do Windows;
- não há recuperação automática de senha; proteja o arquivo de acesso inicial, o banco e os backups;
- o protocolo de LAN usa HTTP e a chave da máquina ainda não é uma credencial criptográfica;
- não existe pipeline formal de migrations/rollback nem restauração guiada;
- arquivos gerados antigos (`bin`, `obj`, logs e banco de demonstração) ainda podem existir em históricos anteriores do repositório, embora `.gitignore` impeça novas inclusões comuns;
Não execute instaladores ou ferramentas externas de controle da estação em computadores institucionais.

Consulte também [ARCHITECTURE.md](ARCHITECTURE.md), [AUDIT_REPORT.md](AUDIT_REPORT.md), [ROADMAP.md](ROADMAP.md) e [CHANGELOG.md](CHANGELOG.md).
