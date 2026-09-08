@echo off
setlocal
cd /d "%~dp0"
title Compilador Nativo Windows 11 - Tradutor PDF ^& Ollama

echo ========================================================
echo   Compilando Tradutor PDF ^& OCR Ollama (C# Nativo)
echo ========================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERRO] Compilador csc.exe nao encontrado no .NET Framework!
    pause
    exit /b 1
)

echo Usando compilador: %CSC%
set ICON_FLAG=
if exist "icon.ico" set ICON_FLAG=/win32icon:icon.ico

"%CSC%" /nologo /target:winexe /optimize+ /out:mimai_pdf.exe %ICON_FLAG% /reference:System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll src\*.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo   [SUCESSO] mimai_pdf.exe compilado com sucesso!
    echo ========================================================
    echo.
) else (
    echo.
    echo [ERRO] Falha na compilacao. Codigo de saida: %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

endlocal
