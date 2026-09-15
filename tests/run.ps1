$ErrorActionPreference = "Stop"
$repoPath = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$testExecutable = Join-Path ([System.IO.Path]::GetTempPath()) ('mimai-viewer-tests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $sourceFiles = @((Get-ChildItem -Path (Join-Path $repoPath 'src\*.cs')).FullName)
    $sourceFiles += Join-Path $PSScriptRoot 'TranslationViewerTests.cs'
    $wpfPath = Join-Path (Split-Path $compilerPath) 'WPF'
    $mathDll = Join-Path $repoPath 'vendor\WpfMath\WpfMath.dll'
    $refs = "System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xaml.dll,$wpfPath\WindowsBase.dll,$wpfPath\PresentationCore.dll,$wpfPath\PresentationFramework.dll,$mathDll"
    & $compilerPath /nologo /target:exe /main:TranslationViewerTests "/out:$testExecutable" "/reference:$refs" "/resource:$mathDll,Mimai.WpfMath.dll" $sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Translation viewer tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $testExecutable) { Remove-Item -LiteralPath $testExecutable }
}
