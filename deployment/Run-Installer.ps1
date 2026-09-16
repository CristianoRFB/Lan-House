[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('ADMIN', 'CLIENTE')][string]$Role,
    [Parameter(Mandatory = $true)][string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageRoot).Path
$logRoot = Join-Path $env:ProgramData 'Adrenalina\logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$logPath = Join-Path $logRoot "Install-$Role.latest.log"

try {
    Start-Transcript -LiteralPath $logPath -Force | Out-Null
    Write-Host "Iniciando instalação do pacote $Role em $package"

    if ($Role -eq 'ADMIN') {
        $installer = Join-Path $package 'Install-Production.ps1'
        & $installer -PackageRoot $package -EnableLan
    } else {
        $installer = Join-Path $package 'Install-ClientStation.ps1'
        & $installer -PackageRoot $package
    }

    if (-not $?) { throw "O instalador do pacote $Role retornou falha." }
    Write-Host "Instalação do pacote $Role concluída."
    Stop-Transcript | Out-Null
    exit 0
} catch {
    Write-Error ("Falha na instalação do pacote {0}: {1}" -f $Role, $_.Exception.Message)
    Write-Host "O log completo foi salvo em $logPath"
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
