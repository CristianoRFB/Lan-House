[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AgentExecutable
)

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute a recuperação em um PowerShell elevado.'
}

$serviceName = 'AdrenalinaClientAgent'
Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $AgentExecutable) {
    & $AgentExecutable --recover
    if ($LASTEXITCODE -ne 0) {
        throw 'A restauração das políticas do Windows falhou; o serviço não será removido.'
    }
}

sc.exe delete $serviceName | Out-Null
Write-Host 'Agent removido e políticas do Windows restauradas. Verifique a checklist de uninstall antes de reutilizar a estação.'
