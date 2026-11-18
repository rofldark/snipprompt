# Generates icon.ico (16/24/32/48/256 px, PNG entries): white tile, selection corners, arrow into a tray.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$sizes = 16, 24, 32, 48, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $k = $s / 32.0
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 7 * $k; $w = $s - 1
    $tile.AddArc(0, 0, $r, $r, 180, 90); $tile.AddArc($w - $r, 0, $r, $r, 270, 90)
    $tile.AddArc($w - $r, $w - $r, $r, $r, 0, 90); $tile.AddArc(0, $w - $r, $r, $r, 90, 90); $tile.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255))), $tile)
    $g.DrawPath((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170, 170, 170)), ([Math]::Max(1, $k))), $tile)
    $blue = [System.Drawing.Color]::FromArgb(37, 99, 235)
    $pen = New-Object System.Drawing.Pen $blue, ([Math]::Max(1.5, 2.2 * $k))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    # selection corners
    $a = 6 * $k; $b = 26 * $k; $c = 5 * $k
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $a, ($a + $c)), (New-Object System.Drawing.PointF $a, $a), (New-Object System.Drawing.PointF ($a + $c), $a)))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF ($b - $c), $a), (New-Object System.Drawing.PointF $b, $a), (New-Object System.Drawing.PointF $b, ($a + $c))))
    # arrow down into a tray
    $mx = 16 * $k
    $g.DrawLine($pen, $mx, (10 * $k), $mx, (19 * $k))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF (12 * $k), (15.5 * $k)), (New-Object System.Drawing.PointF $mx, (19.5 * $k)), (New-Object System.Drawing.PointF (20 * $k), (15.5 * $k))))
    $g.DrawLines($pen, [System.Drawing.PointF[]]@((New-Object System.Drawing.PointF $a, (21 * $k)), (New-Object System.Drawing.PointF $a, $b), (New-Object System.Drawing.PointF $b, $b), (New-Object System.Drawing.PointF $b, (21 * $k))))
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
[System.IO.File]::WriteAllBytes((Join-Path $root 'icon.ico'), $out.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $root 'docs\icon-preview.png'), $pngs[-1])
"icon.ico geschrieben"
