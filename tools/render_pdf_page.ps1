param(
    [Parameter(Mandatory=$true)][string]$PdfPath,
    [Parameter(Mandatory=$true)][int]$PageIndex,
    [Parameter(Mandatory=$true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.InMemoryRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

$allAsTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' }

function Invoke-WinRtOp($asyncOp, [Type]$resultType) {
    $m = $allAsTask | Where-Object { $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
    $task = $m.MakeGenericMethod($resultType).Invoke($null, @($asyncOp))
    return $task.GetAwaiter().GetResult()
}

function Invoke-WinRtAction($asyncAction) {
    foreach ($m in $allAsTask) {
        try {
            if ($m.IsGenericMethodDefinition) {
                $genArgs = $m.GetGenericArguments()
                if ($genArgs.Count -eq 1) {
                    $task = $m.MakeGenericMethod([uint64]).Invoke($null, @($asyncAction))
                    $null = $task.GetAwaiter().GetResult()
                    return
                }
            } else {
                $task = $m.Invoke($null, @($asyncAction))
                $null = $task.GetAwaiter().GetResult()
                return
            }
        } catch { }
    }
}

$fullPath = [System.IO.Path]::GetFullPath($PdfPath)
if (-not (Test-Path $fullPath)) {
    throw "Arquivo PDF não encontrado: $fullPath"
}

$fileOp = [Windows.Storage.StorageFile]::GetFileFromPathAsync($fullPath)
$file = Invoke-WinRtOp $fileOp ([Windows.Storage.StorageFile])

$docOp = [Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($file)
$doc = Invoke-WinRtOp $docOp ([Windows.Data.Pdf.PdfDocument])

if ($PageIndex -lt 0 -or $PageIndex -ge $doc.PageCount) {
    throw "Página $PageIndex fora do intervalo (total: $($doc.PageCount))."
}

$page = $doc.GetPage([uint32]$PageIndex)
$stream = New-Object Windows.Storage.Streams.InMemoryRandomAccessStream

$renderOp = $page.RenderToStreamAsync($stream)
Invoke-WinRtAction $renderOp

$stream.Seek(0)
$reader = New-Object Windows.Storage.Streams.DataReader($stream)
$loadOp = $reader.LoadAsync([uint32]$stream.Size)
$null = Invoke-WinRtOp $loadOp ([uint32])

$bytes = New-Object byte[] ([int]$stream.Size)
$reader.ReadBytes($bytes)
$reader.Dispose()
$stream.Dispose()

$outDir = [System.IO.Path]::GetDirectoryName($OutputPath)
if ($outDir -and -not (Test-Path $outDir)) {
    [System.IO.Directory]::CreateDirectory($outDir) | Out-Null
}

[System.IO.File]::WriteAllBytes($OutputPath, $bytes)
