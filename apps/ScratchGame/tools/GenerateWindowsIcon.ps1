param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$source = [System.IO.Path]::GetFullPath($SourcePath)
$output = [System.IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
    throw "Icon source PNG not found: $source"
}

$outputDirectory = [System.IO.Path]::GetDirectoryName($output)
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null

# Native layers cover Windows shell/taskbar/DPI scenarios instead of relying on
# one oversized bitmap that Windows has to scale at display time.
$sizes = @(16, 20, 24, 28, 32, 40, 48, 64, 72, 80, 96, 128, 256)
$frames = New-Object 'System.Collections.Generic.List[byte[]]'
$image = [System.Drawing.Image]::FromFile($source)
try {
    if ($image.Width -ne 256 -or $image.Height -ne 256) {
        throw "Icon source must be exactly 256x256. Actual: $($image.Width)x$($image.Height)"
    }

    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($image, 0, 0, $size, $size)
            }
            finally {
                $graphics.Dispose()
            }

            $stream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $frames.Add($stream.ToArray())
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $image.Dispose()
}

$file = [System.IO.File]::Open(
    $output,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)
$writer = New-Object System.IO.BinaryWriter($file)
try {
    # ICONDIR
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $payloadOffset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $payload = $frames[$i]
        $dimensionByte = if ($size -eq 256) { [byte]0 } else { [byte]$size }

        # ICONDIRENTRY
        $writer.Write($dimensionByte)
        $writer.Write($dimensionByte)
        $writer.Write([byte]0)       # color count (true-color PNG)
        $writer.Write([byte]0)       # reserved
        $writer.Write([uint16]1)     # planes
        $writer.Write([uint16]32)    # bit depth
        $writer.Write([uint32]$payload.Length)
        $writer.Write([uint32]$payloadOffset)
        $payloadOffset += $payload.Length
    }

    foreach ($payload in $frames) {
        $writer.Write($payload)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Host "Generated Windows ICO: $output"
Write-Host "Native sizes: $($sizes -join ', ')"
