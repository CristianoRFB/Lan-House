# Adrenalina

Sistema de gerenciamento de lan house em C#/.NET, evoluído a partir da arquitetura já existente e seguindo como referência principal a documentação do HandyCafe.

## Apps principais

- `src/Adrenalina.Admin`
  - executável Windows com painel nativo
  - sobe o backend local automaticamente
  - embute o painel admin em `WebView2`
  - roda offline com SQLite local
  - minimiza para a bandeja
  - oferece start/stop do servidor, indicador de clientes online e backup manual

- `src/Adrenalina.Client`
  - executável Windows para modo quiosque
  - trava a máquina até login válido
  - usa fullscreen, hook global de teclado, ocultação da barra de tarefas, política do Gerenciador de Tarefas e bloqueio do Explorer
  - inclui modo `--service` para watchdog via Windows Service
  - inclui watchdog local `--watchdog` para reiniciar a UI se ela for encerrada

## Camadas reaproveitadas

- `src/Adrenalina.Domain`
- `src/Adrenalina.Application`
- `src/Adrenalina.Infrastructure`
- `src/Adrenalina.Server`

Os projetos antigos `Adrenalina.ClientAgent` e `Adrenalina.ClientShell` continuam no repositório como referência/legado, mas o fluxo principal agora é `Adrenalina.Admin` + `Adrenalina.Client`.

## Build

### Compilar solução

```powershell
dotnet build Adrenalina.slnx
```

### Publicar ADMIN

```powershell
dotnet publish src/Adrenalina.Admin -c Release -r win-x64 --self-contained false
```

Saída principal:

- `src\Adrenalina.Admin\bin\Release\net10.0-windows\win-x64\publish\Adrenalina.Admin.exe`

### Publicar CLIENTE

```powershell
dotnet publish src/Adrenalina.Client -c Release -r win-x64 --self-contained false
```

Saída principal:

- `src\Adrenalina.Client\bin\Release\net10.0-windows\win-x64\publish\Adrenalina.Client.exe`

## Uso do ADMIN

### Execução simples

Abra:

- `Adrenalina.Admin.exe`

Ao abrir, ele:

- sobe a API local
- garante a inicialização do banco
- inicia o ciclo de manutenção/sincronização
- carrega o painel MVC dentro do próprio app

### URL interna

- `http://127.0.0.1:5076/auth/login`

### Credenciais iniciais

- login: `admin`
- senha: `adrenalina123`
- PIN do admin seed: `1234`

### Dados do ADMIN

Quando o app nativo é usado:

- banco SQLite: `%ProgramData%\Adrenalina\admin-data\adrenalina.db`
- backups: `%ProgramData%\Adrenalina\admin-data\backups`

Quando o projeto web `Adrenalina.Server` é executado isoladamente:

- banco SQLite: `src\Adrenalina.Server\data\adrenalina.db`

## Uso do CLIENTE

### Configuração

O cliente lê e cria sua configuração em:

- `%ProgramData%\Adrenalina\client\clientsettings.json`

Campos principais:

- `ServerBaseUrl`
- `MachineKey`
- `MachineName`
- `MachineKind`
- `SyncIntervalSeconds`
- `EnableDestructiveCommands`
- `LaunchLocalWatchdog`
- `UiScheduledTaskName`

### Modos de execução

- modo normal: `Adrenalina.Client.exe`
- modo serviço: `Adrenalina.Client.exe --service`
- modo watchdog local: `Adrenalina.Client.exe --watchdog --pid <PID>`

### Runtime local

- `%ProgramData%\Adrenalina\runtime\<NOME-DA-MAQUINA>\client-state.json`
- `%ProgramData%\Adrenalina\runtime\<NOME-DA-MAQUINA>\client-requests.json`

## Instalação do CLIENTE

### Instalação recomendada

1. Publique ou copie `Adrenalina.Client.exe` para a máquina cliente.
2. Execute PowerShell como Administrador.
3. Rode:

```powershell
.\scripts\install-client.ps1 `
  -ClientExePath "C:\LanHouse\Adrenalina.Client.exe" `
  -ServerBaseUrl "http://IP-DO-ADMIN:5076/" `
  -MachineKind Pc `
  -InteractiveUser "NOMEPC\UsuarioDaLan"
```

Esse script:

- grava `clientsettings.json` em `%ProgramData%`
- registra a tarefa agendada `Adrenalina Client UI` no logon
- registra o serviço `AdrenalinaClientService`
- configura reinício automático do serviço

### Endurecimento máximo de quiosque

Para shell replacement no Windows:

```powershell
.\scripts\install-client.ps1 `
  -ClientExePath "C:\LanHouse\Adrenalina.Client.exe" `
  -ServerBaseUrl "http://IP-DO-ADMIN:5076/" `
  -InteractiveUser "NOMEPC\UsuarioDaLan" `
  -UseShellReplacement
```

Isso troca o `Shell` do Winlogon para o executável do cliente e exige privilégios de administrador. O script salva o shell anterior em `AdrenalinaShellBackup`.

### Remoção

```powershell
.\scripts\uninstall-client.ps1 -RestoreShell
```

## O que foi implementado nesta evolução

- separação clara entre `Adrenalina.Admin` e `Adrenalina.Client`
- host reutilizável do `Adrenalina.Server` para execução embutida
- `WebView2` no admin
- start/stop do backend dentro do app admin
- indicador de clientes online no admin
- botão de backup manual no admin
- login do cliente via API própria
- sessão aberta pelo backend central sem duplicar regra no cliente
- cliente com fullscreen, topmost, hook global de teclado e watchdog
- bloqueio de `ALT+TAB`, `ALT+ESC`, `CTRL+ESC`, `CTRL+SHIFT+ESC`, tecla Windows e `ALT+F4` enquanto travado
- ocultação da barra de tarefas
- política de desativação do Gerenciador de Tarefas no usuário atual
- interrupção e retomada do Explorer conforme estado travado/liberado
- Windows Service de watchdog do cliente
- tarefa agendada para auto-start interativo no logon
- backup manual e automático
- persistência de sessão/estado offline

## Limitações conhecidas

- `CTRL+ALT+DEL` e a Secure Attention Sequence não podem ser bloqueados por app de usuário; isso é uma limitação do próprio Windows.
- O bloqueio do Gerenciador de Tarefas foi implementado em `HKCU`; para endurecimento em toda a máquina, a instalação deve aplicar política equivalente com privilégios administrativos.
- O watchdog do serviço depende da tarefa agendada interativa para reabrir a UI na sessão do usuário.
- O warning de build do `WebView2` sobre `WindowsBase` não impede a compilação nem o funcionamento observado no smoke test.

## Verificação feita

- `dotnet build Adrenalina.slnx`
- smoke test do `Adrenalina.Admin.exe` com resposta `200` em `http://127.0.0.1:5076/auth/login`
