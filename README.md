# Adrenalina

Sistema de gerenciamento de lan house com dois aplicativos desktop principais:

- `Adrenalina.Admin`: ambiente do administrador
- `Adrenalina.Client`: ambiente do cliente

O foco deste projeto e deixar o uso simples para pessoas comuns:

- o ADMIN e o app principal que deve ser iniciado primeiro
- o ADMIN concentra o bootstrap e sobe o servidor local com um unico botao principal
- o CLIENTE pode ser preparado por uma tela inicial guiada
- os dois apps possuem tutorial inicial
- o tutorial aparece uma vez no primeiro uso
- o tutorial pode ser aberto novamente nas configuracoes
- o sistema evita depender de plugins, extensoes ou ajustes manuais no computador do usuario final

## O que cada app faz

### ADMIN

O app do administrador:

- abre rapidamente e deixa claro o proximo passo
- inicia o servidor local quando voce manda
- prepara o banco local SQLite no primeiro start
- abre o painel do sistema sem depender do WebView2
- mostra status rapido da operacao
- mostra o IP/endereco que os clientes devem usar na rede
- evita abrir duas instancias ao mesmo tempo
- tenta usar a porta padrao e cai para uma porta livre quando ela ja estiver ocupada
- permite backup manual
- fecha com confirmacao e encerramento limpo do servidor local
- permite reabrir tutorial e ajustar preferencias do app

Se o `WebView2` nao estiver disponivel na maquina, o ADMIN continua funcionando:

- o app sobe o ambiente normalmente
- o painel e aberto no navegador padrao
- nao e preciso instalar plugin ou extensao para continuar usando o sistema

### CLIENTE

O app do cliente:

- mostra o estado atual da maquina
- pede o IP do ADMIN logo no primeiro uso
- permite login com usuario e PIN
- mostra tempo, saldo, anotacoes e avisos
- permite pedir cadastro ou mais tempo
- tem uma tela inicial de preparacao para o primeiro uso
- permite reabrir tutorial e editar configuracoes da maquina

## Estrutura principal

- `src/Adrenalina.Admin`
- `src/Adrenalina.Client`
- `src/Adrenalina.Server`
- `src/Adrenalina.Application`
- `src/Adrenalina.Domain`
- `src/Adrenalina.Infrastructure`

Projetos legados que continuam no repositorio:

- `src/Adrenalina.ClientShell`
- `src/Adrenalina.ClientAgent`

O fluxo principal hoje e:

- `Adrenalina.Admin`
- `Adrenalina.Client`

Esses sao os dois apps independentes do fluxo principal. Os projetos legados ficaram fora da solucao principal para reduzir confusao de entrada.

## Requisitos para desenvolvimento

Para compilar o projeto:

- Windows
- .NET SDK `10.0.202`

O repositório ja foi ajustado para usar essa versao pelo `global.json`.

Para o usuario final usar os executaveis publicados, o ideal e:

- abrir o `Adrenalina.Admin.exe` no computador do administrador
- abrir o `Adrenalina.Client.exe` em cada maquina cliente

Nao ha exigencia de extensoes, plugins ou runtime .NET separado quando os apps forem publicados em modo self-contained.

## Dependencias

### Obrigatorias para desenvolver/compilar

- .NET SDK `10.0.202`
- pacotes NuGet do projeto

### Para o usuario final

- nenhuma extensao ou plugin extra e obrigatorio para usar o fluxo principal

### Observacao sobre WebView2 no ADMIN

O ADMIN tenta abrir o painel dentro do proprio app. Se o `WebView2` nao estiver instalado:

- o app nao trava
- o servidor continua iniciando normalmente
- o painel abre no navegador padrao

Em outras palavras: o `WebView2` virou opcional para o uso do ADMIN.

## Como compilar

Use os comandos abaixo na raiz do projeto.

### Restaurar pacotes

```powershell
dotnet restore Adrenalina.slnx -m:1
```

### Compilar tudo

```powershell
dotnet build Adrenalina.slnx --no-restore -m:1
```

O parametro `-m:1` foi mantido para deixar a compilacao mais estavel neste ambiente.

## Como executar em desenvolvimento

### ADMIN

```powershell
dotnet run --project src/Adrenalina.Admin
```

### CLIENTE

```powershell
dotnet run --project src/Adrenalina.Client
```

## Como publicar

### Publicar ADMIN

```powershell
dotnet publish src/Adrenalina.Admin -c Release -r win-x64 --self-contained true -m:1
```

Saida principal:

- `src\Adrenalina.Admin\bin\Release\net8.0-windows\win-x64\publish\Adrenalina.Admin.exe`

### Publicar CLIENTE

```powershell
dotnet publish src/Adrenalina.Client -c Release -r win-x64 --self-contained true -m:1
```

Saida principal:

- `src\Adrenalina.Client\bin\Release\net8.0-windows\win-x64\publish\Adrenalina.Client.exe`

## Como usar o ADMIN

### Fluxo normal

1. Abra `Adrenalina.Admin.exe`.
2. Clique em `Iniciar servidor e abrir painel`.
3. Veja no topo do app o endereco que os clientes devem usar na rede.
4. Use o botao principal para abrir ou atualizar o painel.
5. Entre com suas credenciais no painel.

### Primeiro acesso

Credenciais iniciais padrao:

- login: `admin`
- senha: `adrenalina123`
- PIN inicial do admin seed: `1234`

### Onde ficam as opcoes principais

No app do ADMIN voce encontra com facilidade:

- botao principal para iniciar servidor e abrir painel
- endereco local do painel
- endereco/IP para os clientes na rede
- backup manual
- abertura do painel no navegador
- configuracoes do app
- tutorial
- confirmacao ao sair com opcao de encerrar tambem o ambiente do dia

### Onde ficam os dados do ADMIN

Quando o ADMIN desktop e usado:

- banco SQLite: `%LocalAppData%\Adrenalina\Admin\adrenalina.db`
- backups: `%LocalAppData%\Adrenalina\Admin\backups`
- preferencias locais do app: `%LocalAppData%\Adrenalina\Admin\admin-app.json`

Se existir base antiga em `%ProgramData%\Adrenalina\admin-data`, o ADMIN tenta migrar os dados automaticamente para o caminho novo no primeiro start.

## Como usar o CLIENTE

### Primeiro uso

No primeiro uso, o CLIENTE abre a tela `Preparar cliente`.

Nela basta informar:

- URL/IP do servidor do ADMIN
- nome da maquina
- chave da maquina
- tipo da maquina

Depois clique em:

- `Salvar e iniciar cliente`

Com isso:

- a configuracao fica salva localmente
- o cliente comeca a sincronizar sozinho
- o tutorial inicial pode ser exibido logo em seguida

### Fluxo normal do cliente

1. Abra `Adrenalina.Client.exe`.
2. Se for o primeiro uso, informe o IP mostrado no app do administrador e conclua a preparacao inicial.
3. Para liberar a maquina, informe usuario e PIN.
4. Se precisar, use `Outras opcoes` para:
   - solicitar cadastro
   - pedir mais tempo

### Onde ficam as configuracoes do CLIENTE

Arquivo principal:

- `%LocalAppData%\Adrenalina\Client\clientsettings.json`

Campos mais importantes:

- `ServerBaseUrl`
- `MachineKey`
- `MachineName`
- `MachineKind`
- `SyncIntervalSeconds`
- `SetupCompleted`
- `ShowTutorialOnNextLaunch`

### Runtime do CLIENTE

- `%LocalAppData%\Adrenalina\Runtime\<NOME-DA-MAQUINA>\client-state.json`
- `%LocalAppData%\Adrenalina\Runtime\<NOME-DA-MAQUINA>\client-requests.json`

Se existirem arquivos antigos em `%ProgramData%\Adrenalina`, o CLIENTE tenta reaproveitar esses dados automaticamente.

## Como funciona o tutorial inicial

### ADMIN

O tutorial do ADMIN:

- aparece uma vez no primeiro uso
- explica o fluxo de abrir o app, iniciar o servidor e entrar no painel
- cobre as funcoes do app desktop e o uso do painel web
- explica o fallback para navegador quando o modo embutido nao estiver disponivel
- agora tambem existe no painel web pelo menu `Tutorial`

### CLIENTE

O tutorial do CLIENTE:

- aparece uma vez no primeiro uso
- explica como entrar na maquina
- explica onde pedir cadastro ou mais tempo
- explica onde encontrar as configuracoes e como reabrir o proprio tutorial

## Como reexibir o tutorial

### No ADMIN

Abra:

- `Configuracoes`

Depois:

- marque a opcao para mostrar o tutorial novamente na proxima abertura

Ou:

- clique em `Abrir tutorial agora`

### No CLIENTE

Abra:

- `Configuracoes`

Depois:

- marque a opcao para mostrar o tutorial novamente na proxima abertura

Ou:

- clique em `Abrir tutorial agora`

## Instalacao avancada do CLIENTE (opcional)

Os scripts abaixo continuam existindo para cenarios mais travados de quiosque, servico e tarefa agendada:

- `scripts/install-client.ps1`
- `scripts/uninstall-client.ps1`

Esses scripts sao opcionais.

O fluxo principal do projeto nao depende deles para:

- abrir o app
- preparar o cliente
- fazer login
- usar o tutorial

## Validacao feita

Validacoes executadas neste ajuste:

- `dotnet build Adrenalina.slnx -m:1 /p:UseSharedCompilation=false`
- `dotnet publish src/Adrenalina.Admin -c Release -r win-x64 --self-contained true -m:1 /p:UseSharedCompilation=false`
- `dotnet publish src/Adrenalina.Client -c Release -r win-x64 --self-contained true -m:1 /p:UseSharedCompilation=false`

Observacao:

- os executaveis `Release` de ADMIN e CLIENTE foram publicados com sucesso em modo self-contained
- o app agora possui fallback para navegador, entao o uso do ADMIN nao fica preso ao `WebView2`
- o repositorio passou a ignorar `bin`, `obj`, `artifacts` e cache do Visual Studio para reduzir ruido estrutural
