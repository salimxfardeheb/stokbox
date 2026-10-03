<#
.SYNOPSIS
    Génère assets\stokbox.ico (16, 24, 32, 48, 64 et 256 px) à partir de assets\icon.png.

.DESCRIPTION
    L'icône produite sert à l'exécutable, aux fenêtres, aux raccourcis et à l'installateur.
    Les tailles jusqu'à 64 px sont écrites en bitmap 32 bits (lisibles par toutes les versions de Windows,
    Windows 7 compris) ; la taille 256 px est écrite en PNG, comme le veut le format.
    À relancer chaque fois que icon.png change ; le fichier .ico se versionne avec le dépôt.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\assets\icon.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\assets\stokbox.ico'),
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 256)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Image source introuvable : $Source. Placez-y l'icône carrée (PNG, 1024 px, fond transparent)."
}

$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$original = [System.Drawing.Image]::FromFile($sourcePath)

try {
    if ($original.Width -ne $original.Height) {
        throw "L'image source doit être carrée (trouvé : $($original.Width) x $($original.Height))."
    }

    if ($original.Width -lt 256) {
        throw "L'image source doit faire au moins 256 px de côté (trouvé : $($original.Width))."
    }

    # Une image par taille, redimensionnée depuis l'original pour garder des bords nets.
    $images = foreach ($size in ($Sizes | Sort-Object)) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)

            $attributes = New-Object System.Drawing.Imaging.ImageAttributes
            # Sans ce réglage, le redimensionnement assombrit les pixels du bord.
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $target = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
            $graphics.DrawImage($original, $target, 0, 0, $original.Width, $original.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            $attributes.Dispose()
        }
        finally {
            $graphics.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        $writer = New-Object System.IO.BinaryWriter($stream)

        if ($size -ge 256) {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        else {
            # Bitmap d'icône : en-tête BITMAPINFOHEADER (hauteur doublée pour le masque), pixels BGRA
            # de bas en haut, puis masque de transparence 1 bit (tout à zéro : la transparence vient du canal alpha).
            $writer.Write([int]40)
            $writer.Write([int]$size)
            $writer.Write([int]($size * 2))
            $writer.Write([int16]1)
            $writer.Write([int16]32)
            $writer.Write([int]0)
            $writer.Write([int]0)
            $writer.Write([int]0)
            $writer.Write([int]0)
            $writer.Write([int]0)
            $writer.Write([int]0)

            $rectangle = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
            $data = $bitmap.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try {
                $row = New-Object byte[] ($size * 4)
                for ($y = $size - 1; $y -ge 0; $y--) {
                    $pointer = [IntPtr]::Add($data.Scan0, $y * $data.Stride)
                    [System.Runtime.InteropServices.Marshal]::Copy($pointer, $row, 0, $row.Length)
                    $writer.Write($row)
                }
            }
            finally {
                $bitmap.UnlockBits($data)
            }

            $maskRowLength = [int]([Math]::Ceiling($size / 32.0) * 4)
            $writer.Write((New-Object byte[] ($maskRowLength * $size)))
        }

        $writer.Flush()
        $bitmap.Dispose()

        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        $stream.Dispose()
    }
}
finally {
    $original.Dispose()
}

$images = @($images)

# Fichier .ico : en-tête, une entrée de répertoire par image, puis les images.
$output = New-Object System.IO.MemoryStream
$ico = New-Object System.IO.BinaryWriter($output)
$ico.Write([int16]0)
$ico.Write([int16]1)
$ico.Write([int16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    # Dans le répertoire, 256 px s'écrit 0.
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $ico.Write([byte]$dimension)
    $ico.Write([byte]$dimension)
    $ico.Write([byte]0)
    $ico.Write([byte]0)
    $ico.Write([int16]1)
    $ico.Write([int16]32)
    $ico.Write([int]$image.Bytes.Length)
    $ico.Write([int]$offset)
    $offset += $image.Bytes.Length
}

foreach ($image in $images) {
    $ico.Write($image.Bytes)
}

$ico.Flush()

$destinationDirectory = Split-Path -Parent $Destination
if (-not (Test-Path -LiteralPath $destinationDirectory)) {
    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
}

$destinationPath = Join-Path (Resolve-Path -LiteralPath $destinationDirectory).Path (Split-Path -Leaf $Destination)
[System.IO.File]::WriteAllBytes($destinationPath, $output.ToArray())
$output.Dispose()

Write-Host ("Icône générée : {0} ({1} tailles : {2})" -f $destinationPath, $images.Count, (($images | ForEach-Object { $_.Size }) -join ', '))
