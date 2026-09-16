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

if (Test-Path $OutputRoot) {
    $runningAdrenalina = @(Get-Process -Name 'Adrenalina.Admin', 'Adrenalina.Client', 'Adrenalina.Server', 'Adrenalina.Launcher' -ErrorAction SilentlyContinue)
    if ($runningAdrenalina.Count -gt 0) {
        $names = ($runningAdrenalina | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
        throw "Feche os aplicativos Adrenalina antes de substituir o pacote publicado: $names."
    }

    try {
        Remove-Item -LiteralPath $OutputRoot -Recurse -Force
    }
    catch {
        throw "Não foi possível limpar o pacote publicado em $OutputRoot. Feche qualquer aplicativo que esteja usando os arquivos e tente novamente. Detalhe: $($_.Exception.Message)"
    }
}
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$publishRoot = Join-Path $OutputRoot '.publish'
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

Invoke-Dotnet @('restore', $solution, '--runtime', 'win-x64', '-p:Configuration=Release', '-m:1')
Invoke-Dotnet @('build', $solution, '--no-restore', '--configuration', 'Release', '-m:1', '/p:UseSharedCompilation=false')
Invoke-Dotnet @('test', (Join-Path $repoRoot 'tests\Adrenalina.Tests\Adrenalina.Tests.csproj'), '--no-restore', '--configuration', 'Release', '-m:1', '/p:UseSharedCompilation=false')
$audit = & dotnet list $solution package --vulnerable --include-transitive 2>&1
$audit | Write-Host
if ($LASTEXITCODE -ne 0 -or $audit -match 'has the following vulnerable packages') {
    throw 'A auditoria de vulnerabilidades NuGet falhou.'
}

Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Admin\Adrenalina.Admin.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $publishRoot 'Admin'), '-m:1')
Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Client\Adrenalina.Client.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $publishRoot 'Client'), '-m:1')
Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Agent\Adrenalina.Agent.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $publishRoot 'Agent'), '-m:1')
Invoke-Dotnet @('publish', (Join-Path $repoRoot 'src\Adrenalina.Launcher\Adrenalina.Launcher.csproj'), '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', (Join-Path $publishRoot 'Launcher'), '-m:1')

foreach ($component in 'Admin', 'Client', 'Agent', 'Launcher') {
    if (-not (Test-Path (Join-Path $publishRoot $component))) { throw "Publicação incompleta: $component" }
}

$adminPackage = Join-Path $OutputRoot 'ADMIN'
$clientPackage = Join-Path $OutputRoot 'CLIENTE'
New-Item -ItemType Directory -Path $adminPackage, $clientPackage -Force | Out-Null
Copy-Item (Join-Path $publishRoot 'Admin\*') $adminPackage -Recurse -Force
Copy-Item (Join-Path $publishRoot 'Launcher\*') $adminPackage -Recurse -Force
Copy-Item (Join-Path $publishRoot 'Client\*') $clientPackage -Recurse -Force
Copy-Item (Join-Path $publishRoot 'Agent\*') $clientPackage -Recurse -Force
Copy-Item (Join-Path $publishRoot 'Launcher\*') $clientPackage -Recurse -Force
Remove-Item -LiteralPath $publishRoot -Recurse -Force

foreach ($file in 'Install-Production.ps1', 'Validate-Production.ps1', 'Rollback-Production.ps1', 'Copy-LatestBackup.ps1', 'Restore-Backup.ps1', 'Run-Installer.ps1', 'Uninstall-Adrenalina.ps1', 'INSTALAR_ADMIN.bat', 'DESINSTALAR.bat', 'DESINSTALAR_COMPLETO.bat', 'SE VAI INSTALAR EM ADMIN CLICA AQUI.txt', 'SE VAI DESINSTALAR CLICA AQUI.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $adminPackage
}
foreach ($file in 'Install-ClientAgent.ps1', 'Uninstall-ClientAgent.ps1', 'Run-Installer.ps1', 'Uninstall-Adrenalina.ps1', 'Install-ClientStation.ps1', 'Install-ClientCertificate.ps1', 'INSTALAR_CLIENTE.bat', 'DESINSTALAR.bat', 'DESINSTALAR_COMPLETO.bat', 'SE VAI INSTALAR EM CLIENTE CLICA AQUI.txt', 'SE VAI DESINSTALAR CLICA AQUI.txt') {
    Copy-Item (Join-Path $PSScriptRoot $file) $clientPackage
}
Copy-Item (Join-Path $repoRoot 'README.txt') $OutputRoot
Copy-Item (Join-Path $PSScriptRoot 'README.md') $OutputRoot

$manifest = [ordered]@{
    CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
    Commit = (& git -C $repoRoot rev-parse HEAD).Trim()
    Runtime = 'win-x64'
    Configuration = 'Release'
}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputRoot 'release-manifest.json') -Encoding UTF8
Write-Host "Release publicada em $OutputRoot"
