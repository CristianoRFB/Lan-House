[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Get-Location) 'artifacts\release')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Adrenalina.slnx'

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments -join ' ') falhou com código $LASTEXITCODE." }
}

if (Test-Path $OutputRoot) { Remove-Item -LiteralPath $OutputRoot -Recurse -Force }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

Invoke-Dotnet @('restore', $solution, '--runtime', 'win-x64', '-p:Configuration=Release', '-m:1')
Invoke-Dotnet @('build', $solution, '--no-restore', '--configuration', 'Release', '-m:1', '/p:UseSharedCompilation=false')
Invoke-Dotnet @('test', (Join-Path $repoRoot 'tests\Adrenalina.Tests\Adrenalina.Tests.csproj'), '--no-restore', '--configuration', 'Release', '-m:1', '/p:UseSharedCompilation=false')
$audit = & dotnet list $solution package --vulnerable --include-transitive 2>&1
$audit | Write-Host
if ($LASTEXITCODE -ne 0 -or $audit -match 'has the following vulnerable packages') {
    throw 'A auditoria de vulnerabilidades NuGet falhou.'
}

Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Admin\Adrenalina.Admin.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $OutputRoot 'Admin'), '-m:1')
Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Client\Adrenalina.Client.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $OutputRoot 'Client'), '-m:1')
Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Launcher\Adrenalina.Launcher.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $OutputRoot 'Launcher'), '-m:1')

Copy-Item (Join-Path $PSScriptRoot 'Install-Production.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'Validate-Production.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'Install-ClientCertificate.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'Rollback-Production.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'Copy-LatestBackup.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'Restore-Backup.ps1') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'README.md') $OutputRoot

$manifest = [ordered]@{
    CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
    Commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    Runtime = 'win-x64'
    Configuration = 'Release'
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'release-manifest.json') -Encoding UTF8
Write-Host "Release publicada em $OutputRoot"
