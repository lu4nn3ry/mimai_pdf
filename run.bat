@echo off
setlocal
set "ROOT=%~dp0"
pushd "%ROOT%" >nul || exit /b 1

rem Fecha uma instÃ¢ncia anterior para liberar o .exe antes da recompilaÃ§Ã£o.
taskkill /f /im mimai_pdf.exe >nul 2>&1

call "%ROOT%build.bat"
if errorlevel 1 (
    echo.
    echo NÃ£o foi possÃ­vel compilar o aplicativo.
    pause
    popd
    exit /b 1
)

start "" "%ROOT%mimai_pdf.exe"
popd
endlocal
exit /b 0
