$ErrorActionPreference = "Stop"
$repoPath = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$testExecutable = Join-Path ([System.IO.Path]::GetTempPath()) ('mimai-viewer-tests-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    $sourceFiles = @((Get-ChildItem -Path (Join-Path $repoPath 'src\*.cs')).FullName)
    $sourceFiles += @((Get-ChildItem -Path (Join-Path $PSScriptRoot '*.cs')).FullName)
    $wpfPath = Join-Path (Split-Path $compilerPath) 'WPF'
    $mathDll = Join-Path $repoPath 'vendor\WpfMath\WpfMath.dll'
    $refs = "System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Xaml.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll,$wpfPath\WindowsBase.dll,$wpfPath\PresentationCore.dll,$wpfPath\PresentationFramework.dll,$mathDll"
    . (Join-Path $repoPath 'tools\embedded_libraries.ps1')
    $libraries = @(Get-EmbeddedLibraryArguments $repoPath)
    & $compilerPath /nologo /target:exe /main:TranslationViewerTests "/out:$testExecutable" "/reference:$refs" $libraries $sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Translation viewer tests failed.' }
}
finally {
    if (Test-Path -LiteralPath $testExecutable) { Remove-Item -LiteralPath $testExecutable }
}
