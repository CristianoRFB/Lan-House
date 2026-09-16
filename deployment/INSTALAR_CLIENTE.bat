@echo off
setlocal
title Instalacao automatica - Adrenalina CLIENTE
echo Instalacao do CLIENTE Adrenalina
echo O Windows pode solicitar permissao de Administrador.
set "ADRENALINA_PACKAGE_ROOT=%~dp0"
if not exist "%ADRENALINA_PACKAGE_ROOT%Adrenalina.Client.exe" if exist "%~dp0..\artifacts\release\CLIENTE\Adrenalina.Client.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\CLIENTE\"
if not exist "%ADRENALINA_PACKAGE_ROOT%Adrenalina.Client.exe" (
    echo.
    echo ERRO: Adrenalina.Client.exe nao foi encontrado.
    echo Este arquivo deve ser executado dentro de artifacts\release\CLIENTE.
    echo Se voce esta no repositorio, execute deployment\Publish-Release.ps1 primeiro.
    pause
    exit /b 2
)
if not exist "%ADRENALINA_PACKAGE_ROOT%Run-Installer.ps1" (
    echo ERRO: Run-Installer.ps1 nao foi encontrado no pacote CLIENTE.
    pause
    exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$package=$env:ADRENALINA_PACKAGE_ROOT.TrimEnd([char]92); $runner=Join-Path $package 'Run-Installer.ps1'; $q=[char]34; $arguments='-NoProfile -ExecutionPolicy Bypass -File '+$q+$runner+$q+' -Role CLIENTE -PackageRoot '+$q+$package+$q; $p=Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList $arguments; exit $p.ExitCode"
if errorlevel 1 (
    echo Falha na instalacao do CLIENTE. Codigo: %errorlevel%
    echo Execute novamente usando o pacote publicado CLIENTE.
    if exist "%ProgramData%\Adrenalina\logs\Install-CLIENTE.latest.log" type "%ProgramData%\Adrenalina\logs\Install-CLIENTE.latest.log"
    pause
    exit /b 1
)
echo CLIENTE instalado. O atalho sera executado na inicializacao do Windows.
echo Abra o Client agora pelo atalho para fazer o pareamento.
pause
