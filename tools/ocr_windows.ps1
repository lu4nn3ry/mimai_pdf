<#
.SYNOPSIS
    OCR nativo do Windows 10/11 usando Windows.Media.Ocr (WinRT) sem instalar nada.
.PARAMETER ImagePath
    Caminho para a imagem (PNG, JPG, BMP)
.PARAMETER LanguageTag
    Código do idioma OCR (ex: 'pt-BR', 'en-US', 'es-ES')
#>
param (
    [Parameter(Mandatory=$true)]
    [string]$ImagePath,
    [string]$LanguageTag = "pt-BR"
)

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]

$asyncOp = [Windows.Storage.StorageFile]::GetFileFromPathAsync((Resolve-Path $ImagePath).Path)
$file = [WindowsRuntimeSystemExtensions]::AsTask($asyncOp).GetAwaiter().GetResult()

$streamOp = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read)
$stream = [WindowsRuntimeSystemExtensions]::AsTask($streamOp).GetAwaiter().GetResult()

$decoderOp = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)
$decoder = [WindowsRuntimeSystemExtensions]::AsTask($decoderOp).GetAwaiter().GetResult()

$bitmapOp = $decoder.GetSoftwareBitmapAsync()
$bitmap = [WindowsRuntimeSystemExtensions]::AsTask($bitmapOp).GetAwaiter().GetResult()

$lang = New-Object Windows.Globalization.Language($LanguageTag)
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang)
if (-not $engine) {
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
}

$ocrOp = $engine.RecognizeAsync($bitmap)
$result = [WindowsRuntimeSystemExtensions]::AsTask($ocrOp).GetAwaiter().GetResult()

$result.Text
