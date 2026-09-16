[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)][string]$ReleasePath,
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'Adrenalina')
)

$ErrorActionPreference = 'Stop'
$ReleasePath = (Resolve-Path -LiteralPath $ReleasePath).Path
if (-not (Test-Path (Join-Path $ReleasePath 'Adrenalina.Admin.exe'))) { throw "Release inválida: $ReleasePath" }
if (-not (Test-Path (Join-Path $ReleasePath 'Adrenalina.Launcher.exe'))) { throw "Release inválida: $ReleasePath" }

foreach ($name in 'Adrenalina.Admin', 'Adrenalina.Client') {
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw "Feche $name antes de executar o rollback." }
}

if ($PSCmdlet.ShouldProcess($InstallRoot, "Restaurar release $ReleasePath")) {
    if (Test-Path $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    Copy-Item "$ReleasePath\*" $InstallRoot -Recurse -Force
    Write-Host "Rollback concluído em $InstallRoot"
}
