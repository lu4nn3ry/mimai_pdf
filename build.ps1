<#
.SYNOPSIS
    Script de compilacao PowerShell nativo para Tradutor PDF Ollama
#>
$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Compilador Nativo Windows 11 - Tradutor PDF Ollama   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "Compilador csc.exe não encontrado no sistema."
    exit 1
}

Write-Host "Compilador: $cscPath" -ForegroundColor Gray
Write-Host "Compilando arquivos C# em src\ ..." -ForegroundColor Yellow

$sources = (Get-ChildItem -Path "src\*.cs").FullName
$refs = "System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll"
$iconParam = if (Test-Path "icon.ico") { "/win32icon:icon.ico" } else { "" }

if ($iconParam) {
    & $cscPath /nologo /target:winexe /optimize+ /out:mimai_pdf.exe $iconParam /reference:$refs $sources
} else {
    & $cscPath /nologo /target:winexe /optimize+ /out:mimai_pdf.exe /reference:$refs $sources
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCESSO] Executável 'mimai_pdf.exe' gerado com sucesso!" -ForegroundColor Green
} else {
    Write-Host "`n[ERRO] Falha na compilação." -ForegroundColor Red
}
