<#
.SYNOPSIS
    Script de compilacao PowerShell nativo para Tradutor PDF Ollama
#>
param([string]$OutputPath = 'mimai_pdf.exe')
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
$refs = "System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xaml.dll"
$wpfPath = Join-Path (Split-Path $cscPath) 'WPF'
$refs += ",$wpfPath\WindowsBase.dll,$wpfPath\PresentationCore.dll,$wpfPath\PresentationFramework.dll,vendor\WpfMath\WpfMath.dll"
$resources = @('/resource:vendor\WpfMath\WpfMath.dll,Mimai.WpfMath.dll', '/resource:vendor\WpfMath\LICENSE.md,Mimai.WpfMath.LICENSE.md')
$iconParam = if (Test-Path "icon.ico") { "/win32icon:icon.ico" } else { "" }

if ($iconParam) {
    & $cscPath /nologo /target:winexe /optimize+ "/out:$OutputPath" $iconParam /reference:$refs $resources $sources
} else {
    & $cscPath /nologo /target:winexe /optimize+ "/out:$OutputPath" /reference:$refs $resources $sources
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "`n[SUCESSO] Executavel '$OutputPath' gerado com sucesso!" -ForegroundColor Green
