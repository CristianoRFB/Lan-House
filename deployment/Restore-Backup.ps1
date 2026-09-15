[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [string]$DatabasePath = (Join-Path $env:LOCALAPPDATA 'Adrenalina\Admin\adrenalina.db')
)

$ErrorActionPreference = 'Stop'
$BackupPath = (Resolve-Path -LiteralPath $BackupPath).Path
if (-not $BackupPath.EndsWith('.db', [StringComparison]::OrdinalIgnoreCase)) { throw 'O backup precisa ter extensão .db.' }
$header = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($BackupPath)[0..15])
if (-not $header.StartsWith('SQLite format 3')) { throw 'O arquivo informado não é um banco SQLite válido.' }

foreach ($name in 'Adrenalina.Admin', 'Adrenalina.Client') {
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw "Feche $name antes de restaurar o banco." }
}

if ($PSCmdlet.ShouldProcess($DatabasePath, "Restaurar backup $BackupPath")) {
    $databaseDirectory = Split-Path -Parent $DatabasePath
    New-Item -ItemType Directory -Path $databaseDirectory -Force | Out-Null
    if (Test-Path $DatabasePath) {
        $rollbackPath = "$DatabasePath.before-restore-$(Get-Date -Format yyyyMMdd-HHmmss).db"
        Copy-Item -LiteralPath $DatabasePath -Destination $rollbackPath
        Write-Host "Banco atual preservado em $rollbackPath"
    }
    $temporary = "$DatabasePath.restore"
    Copy-Item -LiteralPath $BackupPath -Destination $temporary -Force
    Move-Item -LiteralPath $temporary -Destination $DatabasePath -Force
    Write-Host "Restauração concluída em $DatabasePath"
}
