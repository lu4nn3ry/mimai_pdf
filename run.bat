@echo off
setlocal
cd /d "%~dp0"

rem Fecha uma instÃ¢ncia anterior para liberar o .exe antes da recompilaÃ§Ã£o.
taskkill /f /im mimai_pdf.exe >nul 2>&1

call "%~dp0build.bat"
if errorlevel 1 (
    echo.
    echo NÃ£o foi possÃ­vel compilar o aplicativo.
    pause
    exit /b 1
)

start "" "%~dp0mimai_pdf.exe"
endlocal
