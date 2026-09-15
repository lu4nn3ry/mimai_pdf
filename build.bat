@echo off
setlocal
set "ROOT=%~dp0"
pushd "%ROOT%" >nul || (
    echo [ERRO] Nao foi possivel acessar a pasta do projeto.
    exit /b 1
)
title Compilador Nativo Windows 11 - Tradutor PDF ^& Ollama

echo ========================================================
echo   Compilando Tradutor PDF ^& OCR Ollama (C# Nativo)
echo ========================================================
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC%" (
    echo [ERRO] Compilador csc.exe nao encontrado no .NET Framework!
    pause
    exit /b 1
)

echo Usando compilador: %CSC%
set "ICON_FLAG="
if exist "%ROOT%icon.ico" set ICON_FLAG=/win32icon:"%ROOT%icon.ico"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%build.ps1"

if errorlevel 1 (
    echo.
    echo [ERRO] Falha na compilacao. Codigo de saida: %ERRORLEVEL%
    popd
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ========================================================
echo   [SUCESSO] mimai_pdf.exe compilado com sucesso!
echo ========================================================
echo.
popd
endlocal
exit /b 0
