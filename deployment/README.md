# Implantação do Adrenalina

Este diretório contém o fluxo de publicação e instalação para Windows. Os
scripts devem ser executados em uma máquina de implantação controlada, com
PowerShell elevado quando indicado.

## Publicar

Na raiz do repositório:

```powershell
.\deployment\Publish-Release.ps1
```

O resultado fica em `artifacts\release` com dois pacotes independentes:

- `ADMIN`: Admin, Server, Launcher, dependências e `INSTALAR_ADMIN.bat`;
- `CLIENTE`: Client, Agent, Launcher, dependências e `INSTALAR_CLIENTE.bat`.

Cada pacote mantém seus executáveis na própria pasta, sem misturar arquivos de
funções diferentes.

Os arquivos `.bat` devem ser executados dentro do pacote publicado. A pasta
`deployment` do repositório contém os modelos dos instaladores; ela não contém
os executáveis finais até que `Publish-Release.ps1` seja executado.
Em caso de falha, o motivo fica registrado em
`C:\ProgramData\Adrenalina\logs\Install-ADMIN.latest.log` ou
`Install-CLIENTE.latest.log`.

## Instalar o servidor

Na pasta publicada `artifacts\release\ADMIN`, execute `INSTALAR_ADMIN.bat`.
Esse é o fluxo recomendado e chama o PowerShell elevado automaticamente.
O equivalente manual, executado dentro da própria pasta `ADMIN`, é:

```powershell
.\Install-Production.ps1 -PackageRoot .\ -EnableLan
```

O instalador cria um certificado local para o nome da máquina, grava somente o
thumbprint em `%ProgramData%\Adrenalina\server-settings.json`, cria uma regra
de firewall TCP limitada ao perfil Private e preserva a instalação anterior
para rollback. Para uma autoridade certificadora existente, use
`-CertificatePath` e `-CertificatePassword`.

Distribua o arquivo `.cer` gerado em `C:\ProgramData\Adrenalina\certs` para
cada estação e instale-o em `Trusted Root Certification Authorities`. Em uma
rede corporativa, prefira um certificado emitido pela CA da organização.

Com o Admin ativo e a LAN habilitada, o Client tenta descobrir o servidor
automaticamente via UDP e preenche a URL HTTPS. Para novas estações, use
`INICIAR CONFIGURAÇÃO` no Admin e `PAREAR CONFIGURAÇÃO` no Client; a chave
manual antiga permanece apenas para compatibilidade.

## Validar

Depois de iniciar o Admin, execute:

```powershell
.\deployment\Validate-Production.ps1 -ServerUrl https://NOME-DO-SERVIDOR:5076/
```

O script verifica arquivos, certificado, regra de firewall, `/health`,
`/health/ready` e a possibilidade de validar o último backup sem restaurar o
banco ativo.

Para retornar a uma versão anterior, com os aplicativos fechados:

```powershell
.\deployment\Rollback-Production.ps1 -ReleasePath "$env:ProgramData\Adrenalina\releases\20260915-120000"
```

Para guardar uma cópia fora do computador e exercitar a restauração:

```powershell
.\deployment\Copy-LatestBackup.ps1 -DestinationRoot \\servidor-backup\adrenalina
.\deployment\Restore-Backup.ps1 -BackupPath \\servidor-backup\adrenalina\adrenalina-20260915-203000.db -WhatIf
```

O `-WhatIf` deve ser revisado antes da execução real. A restauração exige os
aplicativos fechados e preserva o banco anterior com timestamp.

Para retornar a uma versão anterior, com os aplicativos fechados:

```powershell
.\deployment\Rollback-Production.ps1 -ReleasePath "$env:ProgramData\Adrenalina\releases\20260915-120000"
```

## Escopo do Client e Agent

O Client não altera o Windows diretamente. O pacote `Agent` fornece um serviço
Windows separado, opt-in, com ações allowlisted e rollback das políticas. Sem
Agent instalado e com segredo provisionado, o bloqueio não pode ser considerado
validado. Assigned Access, Shell Launcher e diferenças entre edições do Windows
devem ser avaliados no teste autorizado documentado em `docs/FIELD_TEST.md`.
## Instalar uma estação CLIENTE

Na pasta publicada `artifacts\release\CLIENTE`, execute `INSTALAR_CLIENTE.bat`.
Ele instala Client, Agent e Launcher em `C:\Program Files\Adrenalina`, cria os
atalhos e deixa o Client preparado para iniciar com o Windows. Depois do
pareamento aprovado pelo Admin, o próprio Client instala e provisiona o Agent.

## Desinstalar

Na pasta ADMIN ou CLIENTE instalada, execute `DESINSTALAR.bat`. O desinstalador
remove os programas, o Agent, os atalhos e as regras de firewall, mas preserva
os dados e backups em `%LOCALAPPDATA%\Adrenalina` e `%PROGRAMDATA%\Adrenalina`.

Para apagar também os dados, backups e o certificado configurado pelo sistema,
execute `DESINSTALAR_COMPLETO.bat` somente depois de fazer uma cópia externa.
Os dois arquivos elevam o PowerShell automaticamente e restauram as políticas
do Windows antes de remover o Agent.

## Client Agent

O pacote CLIENTE inclui `Adrenalina.Agent.exe`. Em uma estação Windows
autorizada, o instalador e o pareamento fazem o provisionamento automaticamente.
Se for necessário reparar manualmente, execute:

```powershell
.\Install-ClientAgent.ps1 -AgentDirectory 'C:\Program Files\Adrenalina'
```

Por padrão, o instalador lê a credencial DPAPI já criada pelo Client pareado,
quando executado pelo mesmo usuário Windows. Como alternativa, use
`-MachineSecret (Read-Host 'Segredo da estação' -AsSecureString)`. O segredo é
protegido por DPAPI no escopo da máquina. O serviço aceita apenas
ações allowlisted assinadas e mantém a estação bloqueada quando não há segredo
válido. Para recuperação ou desinstalação, execute o script elevado:

```powershell
.\Uninstall-ClientAgent.ps1 -AgentExecutable 'C:\Program Files\Adrenalina\Adrenalina.Agent.exe'
```

Esses scripts devem ser usados somente em computadores Windows autorizados e
devem ser seguidos pela validação de `docs/FIELD_TEST.md`.
