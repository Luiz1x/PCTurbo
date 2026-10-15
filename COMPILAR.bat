@echo off
setlocal
cd /d "%~dp0"
title Compilando o PC Turbo
echo ==========================================
echo   PC Turbo - gerando o programa (.exe)
echo ==========================================
echo.

:: Procura o compilador que ja vem no Windows (nao precisa instalar nada)
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Nao encontrei o compilador do .NET Framework neste PC.
    echo Me avise que eu te passo outro caminho.
    echo.
    pause
    exit /b 1
)

if not exist "src\Util.cs" (
    echo Nao encontrei a pasta "src". Extraia TODO o zip antes de compilar.
    echo.
    pause
    exit /b 1
)

taskkill /f /im PCTurbo.exe >nul 2>&1
if exist "PCTurbo.exe" copy /y "PCTurbo.exe" "PCTurbo.exe.bak" >nul 2>&1
if exist "PCTurbo.exe" del /f /q "PCTurbo.exe" >nul 2>&1
if exist "PCTurbo.exe" (
    echo Nao consegui apagar o PCTurbo.exe antigo. Feche o PC Turbo e tente de novo.
    echo.
    if not "%~1"=="auto" pause
    exit /b 1
)

set "ICONOPT="
if exist "icone.ico" set "ICONOPT=/win32icon:icone.ico"

echo Compilando (com o modulo NVIDIA)...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /define:NVAPI /out:PCTurbo.exe /win32manifest:app.manifest %ICONOPT% /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\*.cs
if not errorlevel 1 goto :compilou

echo.
echo O modulo NVIDIA deu erro. Tentando compilar sem ele...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /out:PCTurbo.exe /win32manifest:app.manifest %ICONOPT% /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\*.cs
:compilou

if not exist "PCTurbo.exe" (
    if exist "PCTurbo.exe.bak" copy /y "PCTurbo.exe.bak" "PCTurbo.exe" >nul 2>&1
    echo.
    echo ==========================================
    echo   DEU ERRO AO COMPILAR
    if exist "PCTurbo.exe" echo   A versao anterior do PC Turbo foi mantida e continua funcionando.
    echo   Tire um print desta janela e mande para o Claude.
    echo ==========================================
    echo.
    pause
    exit /b 1
)

echo.
echo ==========================================
echo   PRONTO! Foi criado o arquivo PCTurbo.exe nesta pasta.
echo   Ele pede permissao de administrador ao abrir.
echo   Dica: botao direito no PCTurbo.exe ^> Mostrar mais opcoes
echo         ^> Enviar para ^> Area de trabalho (criar atalho).
echo ==========================================
echo.
if "%~1"=="auto" goto :fim
choice /c SN /m "Abrir o PC Turbo agora"
if errorlevel 2 goto :fim
start "" "%~dp0PCTurbo.exe"
:fim
endlocal
