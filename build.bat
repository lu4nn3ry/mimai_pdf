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

for %%I in ("%CSC%") do set "WPF=%%~dpIWPF"
"%CSC%" /nologo /target:winexe /optimize+ /out:"%ROOT%mimai_pdf.exe" %ICON_FLAG% /reference:System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xaml.dll /reference:"%WPF%\WindowsBase.dll" /reference:"%WPF%\PresentationCore.dll" /reference:"%WPF%\PresentationFramework.dll" /reference:"%ROOT%vendor\WpfMath\WpfMath.dll" /resource:"%ROOT%vendor\WpfMath\WpfMath.dll",Mimai.WpfMath.dll /resource:"%ROOT%vendor\WpfMath\LICENSE.md",Mimai.WpfMath.LICENSE.md "%ROOT%src\*.cs"

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
