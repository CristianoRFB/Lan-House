@echo off
setlocal
title Desinstalar - Adrenalina
echo.
echo Este desinstalador remove o Adrenalina, o Agent, atalhos e regras de firewall.
echo Os dados e backups serao preservados para permitir recuperacao.
echo.
choice /M "Deseja continuar"
if errorlevel 2 exit /b 0
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Uninstall-Adrenalina.ps1\"'; exit $p.ExitCode"
if errorlevel 1 (
    echo Falha na desinstalacao. Verifique a mensagem acima.
    pause
    exit /b 1
)
echo Desinstalacao concluida. Os dados foram preservados.
pause
