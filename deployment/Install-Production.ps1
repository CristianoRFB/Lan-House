[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'Adrenalina'),
    [string]$DataRoot = (Join-Path $env:ProgramData 'Adrenalina'),
    [switch]$EnableLan,
    [string]$CertificatePath,
    [SecureString]$CertificatePassword
)

$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este instalador em um PowerShell como Administrador.'
}

$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
if (-not (Test-Path (Join-Path $PackageRoot 'Adrenalina.Admin.exe'))) { throw "Publicação do Admin não encontrada em $PackageRoot." }
if (-not (Test-Path (Join-Path $PackageRoot 'Adrenalina.Launcher.exe'))) { throw "Publicação do Launcher não encontrada em $PackageRoot." }
if (-not (Test-Path (Join-Path $PackageRoot 'Adrenalina.Server.exe'))) { throw "Publicação do servidor não encontrada em $PackageRoot." }

foreach ($name in 'Adrenalina.Admin', 'Adrenalina.Client') {
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw "Feche $name antes de atualizar a instalação." }
}

New-Item -ItemType Directory -Path $InstallRoot, $DataRoot, (Join-Path $DataRoot 'certs'), (Join-Path $DataRoot 'releases') -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$previousRoot = Join-Path $DataRoot "releases\$stamp"
if (Test-Path $InstallRoot) { New-Item -ItemType Directory -Path $previousRoot -Force | Out-Null; Copy-Item "$InstallRoot\*" $previousRoot -Recurse -Force -ErrorAction SilentlyContinue }

if (Test-Path $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item "$PackageRoot\*" $InstallRoot -Recurse -Force

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
New-AdrenalinaShortcut (Join-Path $publicDesktop 'Adrenalina Launcher.lnk') (Join-Path $InstallRoot 'Adrenalina.Launcher.exe') 'Abrir o sistema Adrenalina'
New-AdrenalinaShortcut (Join-Path $commonStartup 'Adrenalina Admin.lnk') (Join-Path $InstallRoot 'Adrenalina.Admin.exe') 'Iniciar o servidor Adrenalina automaticamente'

$thumbprint = $null
$certificateManagedByAdrenalina = $false
if ($CertificatePath) {
    if (-not $CertificatePassword) { $CertificatePassword = Read-Host 'Senha do certificado PFX' -AsSecureString }
    $certificate = Import-PfxCertificate -FilePath (Resolve-Path -LiteralPath $CertificatePath) -CertStoreLocation Cert:\CurrentUser\My -Password $CertificatePassword
    $thumbprint = $certificate.Thumbprint
} elseif ($EnableLan) {
    $dnsName = $env:COMPUTERNAME
    $ipAddresses = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike '169.254.*' } |
        Select-Object -ExpandProperty IPAddress -Unique)
    $sanEntries = @("DNS=$dnsName", 'DNS=localhost', 'IPAddress=127.0.0.1')
    $sanEntries += $ipAddresses | ForEach-Object { "IPAddress=$($_)" }
    $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=$dnsName" -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyUsage DigitalSignature,KeyEncipherment -TextExtension @("2.5.29.17={text}$($sanEntries -join '&')") -CertStoreLocation Cert:\CurrentUser\My -FriendlyName 'Adrenalina LAN HTTPS' -NotAfter (Get-Date).AddYears(3)
    $thumbprint = $certificate.Thumbprint
    $publicCertificatePath = Join-Path $DataRoot "certs\$dnsName.cer"
    Export-Certificate -Cert $certificate -FilePath $publicCertificatePath -Force | Out-Null
    Import-Certificate -FilePath $publicCertificatePath -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    $certificateManagedByAdrenalina = $true
}

$settings = [ordered]@{
    ListenOnLocalNetwork = [bool]$EnableLan
    UseHttps = [bool]$EnableLan
    CertificateThumbprint = $thumbprint
    CertificateManagedByAdrenalina = $certificateManagedByAdrenalina
}
$settingsPath = Join-Path $DataRoot 'server-settings.json'
$settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$ruleName = 'Adrenalina Admin LAN (Private)'
Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if ($EnableLan) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort '5076-5095' -Profile Private -RemoteAddress LocalSubnet | Out-Null
    New-NetFirewallRule -DisplayName 'Adrenalina Discovery LAN (Private)' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 5075 -Profile Private -RemoteAddress LocalSubnet | Out-Null
}

Write-Host "Adrenalina instalado em $InstallRoot"
Write-Host "Configuração: $settingsPath"
if ($thumbprint) { Write-Host "Certificado: $thumbprint" }
if ($EnableLan) { Write-Host "Regra de firewall limitada ao perfil Private e LocalSubnet." }
Write-Host "Rollback: copie a instalação desejada de $DataRoot\releases para $InstallRoot com os aplicativos fechados."
