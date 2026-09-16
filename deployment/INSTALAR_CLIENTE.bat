@echo off
setlocal
title Instalacao automatica - Adrenalina CLIENTE
echo Instalacao do CLIENTE Adrenalina
echo O Windows pode solicitar permissao de Administrador.
set "ADRENALINA_PACKAGE_ROOT="
if exist "%~dp0Adrenalina.Client.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0"
if not defined ADRENALINA_PACKAGE_ROOT if exist "%~dp0..\artifacts\release\CLIENTE\Adrenalina.Client.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\CLIENTE\"
if not defined ADRENALINA_PACKAGE_ROOT if exist "%~dp0..\Adrenalina.slnx" if exist "%~dp0Download-ReleasePackage.ps1" (
    echo Pacote CLIENTE nao encontrado. Baixando a versao publicada...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Download-ReleasePackage.ps1" -Role CLIENTE -DestinationRoot "%~dp0..\artifacts\release"
    if not errorlevel 1 if exist "%~dp0..\artifacts\release\CLIENTE\Adrenalina.Client.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\CLIENTE\"
)
if not defined ADRENALINA_PACKAGE_ROOT if exist "%~dp0..\Adrenalina.slnx" if exist "%~dp0Publish-Release.ps1" (
    where dotnet.exe >nul 2>&1
    if not errorlevel 1 (
        echo Pacote CLIENTE nao encontrado. Preparando o pacote automaticamente...
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-Release.ps1" -OutputRoot "%~dp0..\artifacts\release"
        if not errorlevel 1 if exist "%~dp0..\artifacts\release\CLIENTE\Adrenalina.Client.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\CLIENTE\"
    )
)
if not defined ADRENALINA_PACKAGE_ROOT (
    echo.
    echo ERRO: pacote CLIENTE nao encontrado.
    echo Execute este arquivo dentro de artifacts\release\CLIENTE.
    echo Se voce baixou somente o codigo do GitHub, verifique a internet ou instale o .NET 8 SDK e execute novamente.
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
