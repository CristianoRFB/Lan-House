[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('ADMIN', 'CLIENTE')][string]$Role,
    [Parameter(Mandatory = $true)][string]$DestinationRoot,
    [string]$Repository = 'CristianoRFB/Lan-House'
)

$ErrorActionPreference = 'Stop'
$assets = @{
    ADMIN = 'Adrenalina-ADMIN-win-x64-v0.1.1.zip'
    CLIENTE = 'Adrenalina-CLIENTE-win-x64-v0.1.1.zip'
}
$assetName = $assets[$Role]
$downloadUri = "https://github.com/$Repository/releases/latest/download/$assetName"
$temporaryRoot = Join-Path $env:TEMP ("Adrenalina-Download-{0}" -f [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $temporaryRoot $assetName
$null = New-Item -ItemType Directory -Path $DestinationRoot -Force
$packagePath = Join-Path (Resolve-Path -LiteralPath $DestinationRoot).Path $Role
$extractionPath = Join-Path $temporaryRoot $Role

try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    Write-Host "Baixando o pacote publicado $Role..."
    Invoke-WebRequest -Uri $downloadUri -OutFile $zipPath -UseBasicParsing

    if ((Get-Item -LiteralPath $zipPath).Length -lt 1MB) {
        throw "O download do pacote $Role parece incompleto."
    }

    New-Item -ItemType Directory -Path $extractionPath -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractionPath -Force

    $requiredFiles = if ($Role -eq 'CLIENTE') {
        'Adrenalina.Client.exe', 'Adrenalina.Agent.exe', 'Adrenalina.Launcher.exe', 'Run-Installer.ps1', 'Install-ClientStation.ps1'
    } else {
        'Adrenalina.Admin.exe', 'Adrenalina.Server.exe', 'Adrenalina.Launcher.exe', 'Run-Installer.ps1', 'Install-Production.ps1'
    }
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $extractionPath $file))) {
            throw "O pacote baixado não contém o arquivo obrigatório $file."
        }
    }

    if (Test-Path -LiteralPath $packagePath) {
        Remove-Item -LiteralPath $packagePath -Recurse -Force
    }
    Move-Item -LiteralPath $extractionPath -Destination $packagePath

    Write-Host "Pacote $Role preparado em $packagePath"
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
