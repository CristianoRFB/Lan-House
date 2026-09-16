[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'Adrenalina')
)

$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este instalador em um PowerShell como Administrador.'
}

$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
foreach ($file in 'Adrenalina.Launcher.exe', 'Adrenalina.Client.exe', 'Adrenalina.Agent.exe', 'Install-ClientAgent.ps1') {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot $file))) {
        throw "Arquivo obrigatório não encontrado: $file"
    }
}

foreach ($name in 'Adrenalina.Client', 'Adrenalina.Agent') {
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) {
        throw "Feche $name antes de instalar a estação."
    }
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
if ((Resolve-Path -LiteralPath $PackageRoot).Path.TrimEnd('\') -ne (Resolve-Path -LiteralPath $InstallRoot).Path.TrimEnd('\')) {
    Copy-Item "$PackageRoot\*" $InstallRoot -Recurse -Force
}

function New-AdrenalinaShortcut([string]$ShortcutPath, [string]$TargetPath, [string]$Description) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = Split-Path -Parent $TargetPath
    $shortcut.Description = $Description
    $shortcut.Save()
}

$publicDesktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$commonPrograms = [Environment]::GetFolderPath('CommonPrograms')
$commonStartup = Join-Path $commonPrograms 'Startup'
New-Item -ItemType Directory -Path $publicDesktop, $commonPrograms, $commonStartup -Force | Out-Null
New-AdrenalinaShortcut (Join-Path $publicDesktop 'Adrenalina Cliente.lnk') (Join-Path $InstallRoot 'Adrenalina.Launcher.exe') 'Abrir o Client Adrenalina'
New-AdrenalinaShortcut (Join-Path $commonStartup 'Adrenalina Cliente.lnk') (Join-Path $InstallRoot 'Adrenalina.Client.exe') 'Iniciar o Client Adrenalina automaticamente'

Write-Host "Client instalado em $InstallRoot. O Client será iniciado pelo atalho de inicialização do Windows."
