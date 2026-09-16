@echo off
setlocal
title Instalacao automatica - Adrenalina ADMIN
echo Instalacao do ADMIN Adrenalina
echo O Windows pode solicitar permissao de Administrador.
set "ADRENALINA_PACKAGE_ROOT=%~dp0"
if not exist "%ADRENALINA_PACKAGE_ROOT%Adrenalina.Admin.exe" if exist "%~dp0..\artifacts\release\ADMIN\Adrenalina.Admin.exe" set "ADRENALINA_PACKAGE_ROOT=%~dp0..\artifacts\release\ADMIN\"
if not exist "%ADRENALINA_PACKAGE_ROOT%Adrenalina.Admin.exe" (
    echo.
    echo ERRO: Adrenalina.Admin.exe nao foi encontrado.
    echo Este arquivo deve ser executado dentro de artifacts\release\ADMIN.
    echo Se voce esta no repositorio, execute deployment\Publish-Release.ps1 primeiro.
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
