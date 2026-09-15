[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DestinationRoot,
    [string]$BackupRoot = (Join-Path $env:LOCALAPPDATA 'Adrenalina\Admin\backups')
)

$ErrorActionPreference = 'Stop'
$latest = Get-ChildItem -LiteralPath $BackupRoot -Filter '*.db' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if (-not $latest) { throw "Nenhum backup encontrado em $BackupRoot." }
$header = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($latest.FullName)[0..15])
if (-not $header.StartsWith('SQLite format 3')) { throw "O backup mais recente não é um arquivo SQLite válido." }

New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
$destination = Join-Path $DestinationRoot $latest.Name
$temporary = "$destination.partial"
Copy-Item -LiteralPath $latest.FullName -Destination $temporary -Force
Move-Item -LiteralPath $temporary -Destination $destination -Force
Write-Host "Backup copiado para $destination"
