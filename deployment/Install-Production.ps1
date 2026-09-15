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
$adminSource = Join-Path $PackageRoot 'Admin'
$clientSource = Join-Path $PackageRoot 'Client'
$launcherSource = Join-Path $PackageRoot 'Launcher'
if (-not (Test-Path (Join-Path $adminSource 'Adrenalina.Admin.exe'))) { throw "Publicação do Admin não encontrada em $adminSource." }
if (-not (Test-Path (Join-Path $clientSource 'Adrenalina.Client.exe'))) { throw "Publicação do Client não encontrada em $clientSource." }
if (-not (Test-Path (Join-Path $launcherSource 'Adrenalina.Launcher.exe'))) { throw "Publicação do Launcher não encontrada em $launcherSource." }

foreach ($name in 'Adrenalina.Admin', 'Adrenalina.Client') {
    if (Get-Process -Name $name -ErrorAction SilentlyContinue) { throw "Feche $name antes de atualizar a instalação." }
}

New-Item -ItemType Directory -Path $InstallRoot, $DataRoot, (Join-Path $DataRoot 'certs'), (Join-Path $DataRoot 'releases') -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$previousRoot = Join-Path $DataRoot "releases\$stamp"
if (Test-Path $InstallRoot) { New-Item -ItemType Directory -Path $previousRoot -Force | Out-Null; Copy-Item "$InstallRoot\*" $previousRoot -Recurse -Force -ErrorAction SilentlyContinue }

if (Test-Path $InstallRoot) { Remove-Item -LiteralPath $InstallRoot -Recurse -Force }
New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
Copy-Item "$adminSource\*" $InstallRoot -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $InstallRoot 'Client') -Force | Out-Null
Copy-Item "$clientSource\*" (Join-Path $InstallRoot 'Client') -Recurse -Force
Copy-Item "$launcherSource\*" $InstallRoot -Recurse -Force

$thumbprint = $null
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
}

$settings = [ordered]@{
    ListenOnLocalNetwork = [bool]$EnableLan
    UseHttps = [bool]$EnableLan
    CertificateThumbprint = $thumbprint
}
$settingsPath = Join-Path $DataRoot 'server-settings.json'
$settings | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding UTF8

$ruleName = 'Adrenalina Admin LAN (Private)'
Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if ($EnableLan) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5076 -Profile Private -RemoteAddress LocalSubnet | Out-Null
    New-NetFirewallRule -DisplayName 'Adrenalina Discovery LAN (Private)' -Direction Inbound -Action Allow -Protocol UDP -LocalPort 5075 -Profile Private -RemoteAddress LocalSubnet | Out-Null
}

Write-Host "Adrenalina instalado em $InstallRoot"
Write-Host "Configuração: $settingsPath"
if ($thumbprint) { Write-Host "Certificado: $thumbprint" }
if ($EnableLan) { Write-Host "Regra de firewall limitada ao perfil Private e LocalSubnet." }
Write-Host "Rollback: copie a instalação desejada de $DataRoot\releases para $InstallRoot com os aplicativos fechados."
