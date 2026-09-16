@echo off
setlocal
title Desinstalacao completa - Adrenalina
echo.
echo ATENCAO: esta opcao remove programas, dados, configuracoes, backups
echo e o certificado configurado pelo Adrenalina.
echo Esta operacao nao pode ser desfeita sem um backup externo.
echo.
choice /M "Deseja apagar tudo"
if errorlevel 2 exit /b 0
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Uninstall-Adrenalina.ps1\" -RemoveData -RemoveCertificates'; exit $p.ExitCode"
if errorlevel 1 (
    echo Falha na desinstalacao completa. Verifique a mensagem acima.
    pause
    exit /b 1
)
echo Desinstalacao completa concluida.
pause
