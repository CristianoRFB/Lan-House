[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AgentDirectory,
    [SecureString]$MachineSecret,
    [string]$ClientCredentialId,
    [string]$ClientCredentialPath = (Join-Path $env:LOCALAPPDATA 'Adrenalina\Client\machine-credential.dpapi')
)

$currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este instalador em um PowerShell elevado.'
}

$agentExecutable = Join-Path $AgentDirectory 'Adrenalina.Agent.exe'
if (-not (Test-Path -LiteralPath $agentExecutable)) {
    throw "Agent não encontrado em $agentExecutable. Publique o pacote antes da instalação."
}

$serviceName = 'AdrenalinaClientAgent'
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
}

if ($MachineSecret) {
    $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($MachineSecret)
    try {
        $secret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
        $secret | & $agentExecutable --provision-secret
        if ($LASTEXITCODE -ne 0) {
            throw 'Não foi possível provisionar o segredo protegido do Agent.'
        }
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    }
}
else {
    if ([string]::IsNullOrWhiteSpace($ClientCredentialId)) {
        $optionsPath = Join-Path $env:LOCALAPPDATA 'Adrenalina\Client\clientsettings.json'
        if (Test-Path -LiteralPath $optionsPath) {
            $ClientCredentialId = ((Get-Content -LiteralPath $optionsPath -Raw | ConvertFrom-Json).MachineCredentialId)
        }
    }

    if ([string]::IsNullOrWhiteSpace($ClientCredentialId)) {
        throw 'Informe -MachineSecret ou execute com o mesmo usuário do Client pareado para detectar a credencial DPAPI.'
    }

    & $agentExecutable --provision-from-client --client-credential-file $ClientCredentialPath --credential-id $ClientCredentialId
    if ($LASTEXITCODE -ne 0) {
        throw 'Não foi possível importar a credencial DPAPI do Client para o Agent.'
    }
}

$secretFile = Join-Path $env:ProgramData 'Adrenalina\Agent\machine-secret.dpapi'
if (-not (Test-Path -LiteralPath $secretFile)) {
    throw 'O arquivo protegido do Agent não foi criado.'
}
& icacls.exe $secretFile /inheritance:r /grant:r '*S-1-5-18:F' '*S-1-5-32-544:F' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Não foi possível restringir a ACL do segredo do Agent.'
}

if (-not $service) {
    New-Service -Name $serviceName -BinaryPathName "`"$agentExecutable`"" -DisplayName 'Adrenalina Client Agent' -Description 'Agente autorizado de proteção reversível da estação Adrenalina.' -StartupType Automatic | Out-Null
}

sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/''/0 | Out-Null
Start-Service -Name $serviceName
Write-Host 'Agent instalado e iniciado. A política só será aplicada após o Client validar o pareamento.'
