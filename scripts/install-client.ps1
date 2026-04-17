param(
    [Parameter(Mandatory = $true)]
    [string]$ClientExePath,

    [Parameter(Mandatory = $true)]
    [string]$ServerBaseUrl,

    [ValidateSet("Pc", "Console")]
    [string]$MachineKind = "Pc",

    [string]$MachineKey = $env:COMPUTERNAME.ToLowerInvariant(),

    [string]$TaskName = "Adrenalina Client UI",

    [string]$ServiceName = "AdrenalinaClientService",

    [string]$InteractiveUser = "$env:USERDOMAIN\$env:USERNAME",

    [switch]$UseShellReplacement
)

$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Execute este script como Administrador."
    }
}

Assert-Administrator

$resolvedClientExePath = (Resolve-Path $ClientExePath).Path
$programDataRoot = Join-Path $env:ProgramData "Adrenalina\client"
$settingsPath = Join-Path $programDataRoot "clientsettings.json"

New-Item -ItemType Directory -Force -Path $programDataRoot | Out-Null

$settings = @{
    ServerBaseUrl = $ServerBaseUrl
    MachineKey = $MachineKey
    MachineName = $env:COMPUTERNAME
    MachineKind = $MachineKind
    SyncIntervalSeconds = 10
    EnableDestructiveCommands = $true
    LaunchLocalWatchdog = $true
    UiScheduledTaskName = $TaskName
}

$settings | ConvertTo-Json -Depth 4 | Set-Content -Path $settingsPath -Encoding UTF8

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

$taskAction = New-ScheduledTaskAction -Execute $resolvedClientExePath
$taskTrigger = New-ScheduledTaskTrigger -AtLogOn
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $InteractiveUser -LogonType InteractiveToken -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $taskAction `
    -Trigger $taskTrigger `
    -Principal $taskPrincipal `
    -Description "Inicia a interface interativa do Adrenalina Client no logon." `
    -Force | Out-Null

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

$binaryPath = "`"$resolvedClientExePath`" --service"
New-Service `
    -Name $ServiceName `
    -BinaryPathName $binaryPath `
    -DisplayName "Adrenalina Client Service" `
    -Description "Watchdog e endurecimento do cliente Adrenalina." `
    -StartupType Automatic | Out-Null

sc.exe failure $ServiceName reset= 0 actions= restart/5000/restart/5000/restart/5000 | Out-Null
Start-Service -Name $ServiceName

if ($UseShellReplacement) {
    $winlogonKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $currentShell = (Get-ItemProperty -Path $winlogonKey -Name Shell -ErrorAction SilentlyContinue).Shell

    if (-not [string]::IsNullOrWhiteSpace($currentShell)) {
        Set-ItemProperty -Path $winlogonKey -Name AdrenalinaShellBackup -Value $currentShell
    }

    Set-ItemProperty -Path $winlogonKey -Name Shell -Value $resolvedClientExePath
}

Write-Host "Cliente instalado com sucesso."
Write-Host "Configuração: $settingsPath"
Write-Host "Serviço: $ServiceName"
Write-Host "Tarefa agendada: $TaskName"
