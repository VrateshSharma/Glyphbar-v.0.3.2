# Generates Glyphbar's logo assets from the dot-matrix "G". Requires Windows PowerShell (System.Drawing).
# All artwork is original and derived from src/Glyphbar.Core/Services/DotMatrixFont.cs.
param([string]$Assets = (Join-Path $PSScriptRoot '..\src\Glyphbar.App\Assets'))

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Assets | Out-Null

$rows = 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F   # the "G" glyph, 5 columns by 7 rows

function New-Logo([int]$w, [int]$h, [double]$fill, [bool]$solid) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    if ($solid) {
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 8, 8, 8))
        $g.FillRectangle($brush, 0, 0, $w, $h)
    }
    $pitch = ([Math]::Min($w, $h) * $fill) / 7.0
    $dot = $pitch * 0.78
    $ox = ($w - 5 * $pitch) / 2.0
    $oy = ($h - 7 * $pitch) / 2.0
    $white = [System.Drawing.Brushes]::White
    for ($y = 0; $y -lt 7; $y++) {
        for ($x = 0; $x -lt 5; $x++) {
            if (($rows[$y] -shr (4 - $x)) -band 1) {
                $g.FillEllipse($white, [single]($ox + $x * $pitch), [single]($oy + $y * $pitch), [single]$dot, [single]$dot)
            }
        }
    }
    $g.Dispose()
    return $bmp
}

function Save-Png($bmp, $name) {
    $bmp.Save((Join-Path $Assets $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

Save-Png (New-Logo 44 44 0.72 $false) 'Square44x44Logo.png'
Save-Png (New-Logo 150 150 0.60 $true) 'Square150x150Logo.png'
Save-Png (New-Logo 50 50 0.66 $true) 'StoreLogo.png'
Save-Png (New-Logo 310 150 0.60 $true) 'Wide310x150Logo.png'
Save-Png (New-Logo 620 300 0.55 $true) 'SplashScreen.png'

# A multi-size .ico holding PNG frames, for the executable and the tray.
$sizes = 16, 24, 32, 48, 64, 256
$frames = foreach ($s in $sizes) {
    $bmp = New-Logo $s $s 0.92 $false
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$frames[$i].Length); $w.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $w.Write($f) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $Assets 'glyphbar.ico'), $out.ToArray())
"Assets written to $Assets"
