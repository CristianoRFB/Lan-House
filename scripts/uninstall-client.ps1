param(
    [string]$TaskName = "Adrenalina Client UI",
    [string]$ServiceName = "AdrenalinaClientService",
    [switch]$RestoreShell
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

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
}

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

if ($RestoreShell) {
    $winlogonKey = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
    $backupShell = (Get-ItemProperty -Path $winlogonKey -Name AdrenalinaShellBackup -ErrorAction SilentlyContinue).AdrenalinaShellBackup

    if (-not [string]::IsNullOrWhiteSpace($backupShell)) {
        Set-ItemProperty -Path $winlogonKey -Name Shell -Value $backupShell
        Remove-ItemProperty -Path $winlogonKey -Name AdrenalinaShellBackup -ErrorAction SilentlyContinue
    }
}

Write-Host "Cliente removido."
