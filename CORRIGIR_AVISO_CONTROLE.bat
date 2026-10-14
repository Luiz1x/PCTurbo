@echo off
:: Corrige o aviso "procurar um app na Microsoft Store" ao usar o controle.
:: Duplo clique. Pede permissao de administrador sozinho.
net session >nul 2>&1
if %errorLevel% == 0 goto :executar
echo Solicitando permissao de administrador...
powershell -Command "Start-Process '%~f0' -Verb RunAs"
exit /b

:executar
reg add "HKLM\SOFTWARE\Classes\ms-gamingoverlay" /ve /d "URL:ms-gamingoverlay" /f >nul
reg add "HKLM\SOFTWARE\Classes\ms-gamingoverlay" /v "URL Protocol" /t REG_SZ /d "" /f >nul
reg add "HKLM\SOFTWARE\Classes\ms-gamebar" /ve /d "URL:ms-gamebar" /f >nul
reg add "HKLM\SOFTWARE\Classes\ms-gamebar" /v "URL Protocol" /t REG_SZ /d "" /f >nul
echo.
echo Pronto! Reinicie o PC e teste o controle de novo.
echo Para desfazer: apague as chaves ms-gamingoverlay e ms-gamebar em HKLM\SOFTWARE\Classes.
echo.
pause
