[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CertificatePath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Certificado não encontrado: $CertificatePath" }
Import-Certificate -FilePath (Resolve-Path -LiteralPath $CertificatePath) -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
Write-Host 'Certificado instalado na raiz confiável do usuário atual.'
