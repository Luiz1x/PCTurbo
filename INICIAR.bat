@echo off
setlocal
cd /d "%~dp0"
title PC Turbo
:: Abre o PC Turbo. Se o programa nao existe ou os arquivos da pasta src
:: ficaram mais novos que o PCTurbo.exe (ex.: o Claude atualizou), compila de novo antes.
set NEED=0
if not exist "PCTurbo.exe" set NEED=1
if "%NEED%"=="0" (
    powershell -NoProfile -Command "$e=(Get-Item 'PCTurbo.exe').LastWriteTime; $n=(Get-ChildItem 'src\*.cs','COMPILAR.bat' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime; if($n -gt $e){exit 1}else{exit 0}"
    if errorlevel 1 set NEED=1
)
if "%NEED%"=="1" (
    echo Atualizando o PC Turbo, so um instante...
    call "%~dp0COMPILAR.bat" auto
    schtasks /Query /TN "PC Turbo Segundo Plano" >nul 2>&1
    if not errorlevel 1 if exist "PCTurbo.exe" start "" "%~dp0PCTurbo.exe" /fundo
)
if exist "PCTurbo.exe" start "" "%~dp0PCTurbo.exe"
endlocal
