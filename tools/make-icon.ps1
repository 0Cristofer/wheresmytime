# Draws the app icon (a white clock on the green of the Start button) and writes
# src/WheresMyTime/Assets/icon.ico with all the sizes Windows asks for.
# Windows only (uses System.Drawing). Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1 [-PreviewDir <dir>]
param([string]$PreviewDir)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$green = [System.Drawing.Color]::FromArgb(0x1F, 0x8A, 0x4C)
$white = [System.Drawing.Color]::White

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded square background.
    $inset = [math]::Max(0.5, $size * 0.03)
    $box = $size - 2 * $inset
    $r = $box * 0.22
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($inset, $inset, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($inset + $box - 2 * $r, $inset, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($inset + $box - 2 * $r, $inset + $box - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($inset, $inset + $box - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $green), $path)

    # Clock face and hands; strokes get relatively thicker at small sizes so they stay crisp.
    $stroke = [math]::Max(1.6, $size * 0.075)
    $pen = New-Object System.Drawing.Pen $white, $stroke
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $c = $size / 2
    $face = $size * 0.29
    $g.DrawEllipse($pen, $c - $face, $c - $face, 2 * $face, 2 * $face)
    $hands = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF $c, ($c - $face * 0.62)),
        (New-Object System.Drawing.PointF $c, $c),
        (New-Object System.Drawing.PointF ($c + $face * 0.45), ($c + $face * 0.32)))
    $g.DrawLines($pen, $hands)
    $g.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewDir) { $bmp.Save((Join-Path $PreviewDir "icon-$size.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , $stream.ToArray()
}

# ICO container with PNG-compressed entries (supported by Windows Vista and later, and by Skia).
$images = foreach ($s in $sizes) { , (New-IconPng $s) }
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()

$target = Join-Path $PSScriptRoot '..\src\WheresMyTime\Assets\icon.ico'
New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
[System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($target), $out.ToArray())
Write-Host "Wrote $([System.IO.Path]::GetFullPath($target)) ($($out.Length) bytes, sizes $($sizes -join ', '))"
