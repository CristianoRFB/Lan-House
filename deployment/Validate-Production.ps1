[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ServerUrl,
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'Adrenalina'),
    [string]$DataRoot = (Join-Path $env:ProgramData 'Adrenalina')
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { $failures.Add($Message) } }

Check (Test-Path (Join-Path $InstallRoot 'Adrenalina.Admin.exe')) 'Executável do Admin ausente.'
Check (Test-Path (Join-Path $InstallRoot 'Client\Adrenalina.Client.exe')) 'Executável do Client ausente.'
Check (Test-Path (Join-Path $DataRoot 'server-settings.json')) 'Configuração de implantação ausente.'
Check ($ServerUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) 'A URL de produção precisa usar HTTPS.'

try {
    $settings = Get-Content (Join-Path $DataRoot 'server-settings.json') -Raw | ConvertFrom-Json
    if ($settings.CertificateThumbprint) {
        $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My | Where-Object Thumbprint -eq ($settings.CertificateThumbprint -replace ' ', '') | Select-Object -First 1
        Check ($null -ne $cert) 'Certificado configurado não está instalado neste computador.'
    }
} catch { $failures.Add("Falha ao ler configuração/certificado: $($_.Exception.Message)") }

try {
    $uri = [Uri]$ServerUrl
    $rule = Get-NetFirewallRule -DisplayName 'Adrenalina Admin LAN (Private)' -ErrorAction SilentlyContinue
    Check (($null -ne $rule) -or $uri.Host -in @('127.0.0.1', 'localhost')) 'Regra de firewall da LAN não encontrada.'
    $health = Invoke-WebRequest -Uri ([Uri]::new($uri, 'health')) -UseBasicParsing -TimeoutSec 10
    Check ($health.StatusCode -eq 200) 'Endpoint /health não retornou 200.'
    $ready = Invoke-WebRequest -Uri ([Uri]::new($uri, 'health/ready')) -UseBasicParsing -TimeoutSec 10
    Check ($ready.StatusCode -eq 200) 'Endpoint /health/ready não retornou 200.'
} catch { $failures.Add("Falha de conectividade/saúde: $($_.Exception.Message)") }

$backupRoot = Join-Path (Join-Path $env:LOCALAPPDATA 'Adrenalina\Admin') 'backups'
$latestBackup = Get-ChildItem $backupRoot -Filter '*.db' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($latestBackup) {
    $header = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($latestBackup.FullName)[0..15])
    Check ($header.StartsWith('SQLite format 3')) 'O backup mais recente não parece ser um banco SQLite válido.'
} else { Write-Warning 'Nenhum backup foi encontrado; execute e valide o primeiro backup antes da entrega.' }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host 'Validação de produção concluída sem falhas bloqueantes.'
