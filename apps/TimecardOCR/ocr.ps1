param([Parameter(Mandatory=$true)][string]$ImagePath)
$ErrorActionPreference = 'Stop'

# Built-in Windows 10/11 WinRT OCR. No network, Python, Tesseract or API key.
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult, Windows.Foundation, ContentType = WindowsRuntime]

function Await-WinRT {
    param([object]$Operation, [Type]$ResultType)
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
        $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and
        $_.GetParameters().Count -eq 1 -and
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
    } | Select-Object -First 1
    if ($null -eq $method) { throw 'Windows OCR async helper unavailable' }
    $task = $method.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.Wait()
    return $task.Result
}

$path = (Resolve-Path -LiteralPath $ImagePath).Path
$file = Await-WinRT ([Windows.Storage.StorageFile]::GetFileFromPathAsync($path)) ([Windows.Storage.StorageFile])
$stream = Await-WinRT ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
try {
    $decoder = Await-WinRT ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $bitmap = Await-WinRT ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
    try {
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
        if ($null -eq $engine) {
            $languages = @([Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages)
            if ($languages.Count -gt 0) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($languages[0]) }
        }
        if ($null -eq $engine) {
            throw 'Windows 沒有可用的 OCR 語言套件。可先用手動輸入，不影響 Excel 匯出。'
        }
        $result = Await-WinRT ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
        $words = New-Object 'System.Collections.Generic.List[object]'
        foreach ($line in $result.Lines) {
            foreach ($word in $line.Words) {
                $r = $word.BoundingRect
                $words.Add([pscustomobject]@{text=$word.Text; x=[math]::Round($r.X,2); y=[math]::Round($r.Y,2); w=[math]::Round($r.Width,2); h=[math]::Round($r.Height,2)})
            }
        }
        return [pscustomobject]@{ok=$true; words=@($words.ToArray()); language=$engine.RecognizerLanguage.LanguageTag}
    }
    finally { if ($null -ne $bitmap) { $bitmap.Dispose() } }
}
finally { if ($null -ne $stream) { $stream.Dispose() } }
