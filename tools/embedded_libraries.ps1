function Get-EmbeddedLibraryArguments([string]$RepositoryPath) {
    foreach ($folder in @('WpfMath', 'PdfPig')) {
        $libraryPath = Join-Path $RepositoryPath ('vendor\' + $folder)
        foreach ($file in Get-ChildItem -LiteralPath $libraryPath -File) {
            if ($file.Extension -eq '.dll') {
                '/reference:' + $file.FullName
                '/resource:' + $file.FullName + ',Mimai.' + $file.Name
            } elseif ($file.Name -match 'LICENSE|NOTICE') {
                '/resource:' + $file.FullName + ',Mimai.' + $folder + '.' + $file.Name
            }
        }
    }
}
