# Implantação do Adrenalina

Este diretório contém o fluxo de publicação e instalação para Windows. Os
scripts devem ser executados em uma máquina de implantação controlada, com
PowerShell elevado quando indicado.

## Publicar

Na raiz do repositório:

```powershell
.\deployment\Publish-Release.ps1
```

O resultado fica em `artifacts\release` e contém os executáveis
self-contained do Admin e do Client.
O pacote também contém o `Adrenalina.Launcher.exe`, que apresenta as opções
`ADMIN` e `CLIENTE`.

## Instalar o servidor

Execute como administrador na máquina que ficará no caixa/servidor:

```powershell
.\deployment\Install-Production.ps1 -PackageRoot .\artifacts\release -EnableLan
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
automaticamente via UDP e preenche a URL HTTPS. O cadastro da chave da estação
continua sendo uma etapa de segurança do primeiro uso.

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

## Escopo do Client

O Client não é um bloqueador de Windows. O estado bloqueado é visual e a
janela continua fechável. O uso de Assigned Access, Shell Launcher ou outra
política institucional deve ser decidido e aplicado pelo responsável de TI do
cliente, fora do aplicativo, com teste de recuperação de acesso.
