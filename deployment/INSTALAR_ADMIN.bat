@echo off
setlocal
title Instalacao automatica - Adrenalina ADMIN
echo Instalacao do ADMIN Adrenalina
echo O Windows pode solicitar permissao de Administrador.
set "ADRENALINA_PACKAGE_ROOT="
if exist "%~dp0Adrenalina.Admin.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0"
if not defined ADRENALINA_PACKAGE_ROOT if exist "%~dp0..\artifacts\release\ADMIN\Adrenalina.Admin.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\ADMIN\"
if not defined ADRENALINA_PACKAGE_ROOT if exist "%~dp0..\Adrenalina.slnx" if exist "%~dp0Publish-Release.ps1" (
    where dotnet.exe >nul 2>&1
    if not errorlevel 1 (
        echo Pacote ADMIN nao encontrado. Preparando o pacote automaticamente...
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-Release.ps1" -OutputRoot "%~dp0..\artifacts\release"
        if not errorlevel 1 if exist "%~dp0..\artifacts\release\ADMIN\Adrenalina.Admin.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\ADMIN\"
    )
)
if not defined ADRENALINA_PACKAGE_ROOT (
    echo.
    echo ERRO: pacote ADMIN nao encontrado.
    echo Execute este arquivo dentro de artifacts\release\ADMIN.
    echo Se voce baixou o codigo do GitHub, use o pacote publicado ADMIN ou instale o .NET 8 SDK e execute novamente.
    pause
    exit /b 2
)
if not exist "%ADRENALINA_PACKAGE_ROOT%Run-Installer.ps1" (
    echo ERRO: Run-Installer.ps1 nao foi encontrado no pacote ADMIN.
    pause
    exit /b 2
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$package=$env:ADRENALINA_PACKAGE_ROOT.TrimEnd([char]92); $runner=Join-Path $package 'Run-Installer.ps1'; $q=[char]34; $arguments='-NoProfile -ExecutionPolicy Bypass -File '+$q+$runner+$q+' -Role ADMIN -PackageRoot '+$q+$package+$q; $p=Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList $arguments; exit $p.ExitCode"
if errorlevel 1 (
    echo Falha na instalacao do ADMIN. Codigo: %errorlevel%
    echo Execute novamente usando o pacote publicado ADMIN.
    if exist "%ProgramData%\Adrenalina\logs\Install-ADMIN.latest.log" type "%ProgramData%\Adrenalina\logs\Install-ADMIN.latest.log"
    pause
    exit /b 1
)
echo ADMIN instalado. Use o atalho criado ou Adrenalina.Launcher.exe.
pause
