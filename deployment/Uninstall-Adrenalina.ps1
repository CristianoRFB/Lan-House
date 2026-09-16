[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'Adrenalina'),
    [string]$ProgramDataRoot = (Join-Path $env:ProgramData 'Adrenalina'),
    [string]$UserDataRoot = (Join-Path $env:LOCALAPPDATA 'Adrenalina'),
    [switch]$RemoveData,
    [switch]$RemoveCertificates,
    [switch]$KeepInstallers
)

$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute o desinstalador como Administrador.'
}

function Remove-PathIfExists([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    }
}

function Remove-ShortcutIfExists([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    }
}

function Remove-CertificateByThumbprint([string]$Thumbprint) {
    if ([string]::IsNullOrWhiteSpace($Thumbprint)) { return }

    $normalized = $Thumbprint -replace '\s', ''
    foreach ($store in 'Cert:\CurrentUser\My', 'Cert:\CurrentUser\Root', 'Cert:\LocalMachine\My', 'Cert:\LocalMachine\Root') {
        try {
            Get-ChildItem -Path $store -ErrorAction SilentlyContinue |
                Where-Object Thumbprint -eq $normalized |
                ForEach-Object { Remove-Item -LiteralPath $_.PSPath -Force -ErrorAction SilentlyContinue }
        } catch {
            Write-Warning "Não foi possível verificar o certificado em ${store}: $($_.Exception.Message)"
        }
    }
}

function Resolve-SafeAdrenalinaPath([string]$Path, [string]$Description) {
    $fullPath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ([string]::IsNullOrWhiteSpace($fullPath) -or $fullPath -match '^[A-Za-z]:$' -or
        (Split-Path -Leaf $fullPath) -ne 'Adrenalina') {
        throw "Por segurança, a pasta de $Description deve terminar em Adrenalina: $fullPath"
    }
    return $fullPath
}

$installRootFull = Resolve-SafeAdrenalinaPath $InstallRoot 'instalação'
$programDataRootFull = Resolve-SafeAdrenalinaPath $ProgramDataRoot 'dados do sistema'
$userDataRootFull = Resolve-SafeAdrenalinaPath $UserDataRoot 'dados do usuário'

$settingsPath = Join-Path $programDataRootFull 'server-settings.json'
$thumbprint = $null
$certificateManagedByAdrenalina = $false
if (Test-Path -LiteralPath $settingsPath) {
    try {
        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        $thumbprint = $settings.CertificateThumbprint
        $certificateManagedByAdrenalina = [bool]$settings.CertificateManagedByAdrenalina
    } catch { }
}

$serviceName = 'AdrenalinaClientAgent'
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host 'Parando o Agent e restaurando as políticas do Windows...'
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    $agentExecutable = Join-Path $installRootFull 'Adrenalina.Agent.exe'
    if (-not (Test-Path -LiteralPath $agentExecutable)) {
        throw "O serviço do Agent existe, mas o executável não foi encontrado em $agentExecutable. O serviço não foi removido para evitar deixar a estação em estado inseguro."
    }

    & $agentExecutable --recover
    if ($LASTEXITCODE -ne 0) {
        throw 'A restauração das políticas do Windows falhou. O serviço não foi removido.'
    }

    sc.exe delete $serviceName | Out-Null
    $deadline = (Get-Date).AddSeconds(10)
    do {
        Start-Sleep -Milliseconds 250
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    } while ($service -and (Get-Date) -lt $deadline)
    if ($service) { throw "O serviço $serviceName não foi removido." }
}

foreach ($processName in 'Adrenalina.Admin', 'Adrenalina.Client', 'Adrenalina.Launcher') {
    Get-Process -Name $processName -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

foreach ($ruleName in 'Adrenalina Admin LAN (Private)', 'Adrenalina Discovery LAN (Private)') {
    Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue |
        Remove-NetFirewallRule -ErrorAction SilentlyContinue
}

$publicDesktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
$commonPrograms = [Environment]::GetFolderPath('CommonPrograms')
Remove-ShortcutIfExists (Join-Path $publicDesktop 'Adrenalina Launcher.lnk')
Remove-ShortcutIfExists (Join-Path $publicDesktop 'Adrenalina Cliente.lnk')
Remove-ShortcutIfExists (Join-Path (Join-Path $commonPrograms 'Startup') 'Adrenalina Admin.lnk')
Remove-ShortcutIfExists (Join-Path (Join-Path $commonPrograms 'Startup') 'Adrenalina Cliente.lnk')

if ($RemoveCertificates -and $certificateManagedByAdrenalina) {
    Remove-CertificateByThumbprint $thumbprint
} elseif ($RemoveCertificates -and $thumbprint) {
    Write-Host 'Certificado externo preservado; ele não foi criado pelo Adrenalina.'
}

if ($RemoveData) {
    Remove-PathIfExists $programDataRootFull
    Remove-PathIfExists $userDataRootFull
    Write-Host 'Dados locais, configurações e backups removidos.'
} else {
    Write-Host "Dados preservados em $userDataRootFull e $programDataRootFull."
}

if (-not $KeepInstallers -and (Test-Path -LiteralPath $installRootFull)) {
    $cleanupCommand = "/c ping 127.0.0.1 -n 3 > nul & rmdir /s /q `"$installRootFull`""
    Start-Process -FilePath $env:ComSpec -ArgumentList $cleanupCommand -WindowStyle Hidden
    Write-Host "A pasta $installRootFull será removida ao finalizar o desinstalador."
}

Write-Host 'Desinstalação concluída. Reinicie o Windows antes de reutilizar a máquina.'
