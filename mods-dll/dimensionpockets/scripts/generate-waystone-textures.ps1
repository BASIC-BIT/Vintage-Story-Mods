$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$textureDir = Join-Path $projectRoot "assets\pocketdimensions\textures\block"

if (-not (Test-Path -LiteralPath $textureDir)) {
    New-Item -ItemType Directory -Path $textureDir -Force | Out-Null
}

function New-Color([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function Mix-Color([System.Drawing.Color]$a, [System.Drawing.Color]$b, [double]$t) {
    $t = [Math]::Max(0, [Math]::Min(1, $t))
    return New-Color `
        ([int]($a.R + (($b.R - $a.R) * $t))) `
        ([int]($a.G + (($b.G - $a.G) * $t))) `
        ([int]($a.B + (($b.B - $a.B) * $t))) `
        ([int]($a.A + (($b.A - $a.A) * $t)))
}

function Save-Texture([string]$fileName, [scriptblock]$pixelFactory) {
    $bitmap = New-Object System.Drawing.Bitmap 16, 16, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt 16; $y++) {
            for ($x = 0; $x -lt 16; $x++) {
                $bitmap.SetPixel($x, $y, (& $pixelFactory $x $y))
            }
        }

        $path = Join-Path $textureDir $fileName
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Wrote $path"
    }
    finally {
        $bitmap.Dispose()
    }
}

$stoneDark = New-Color 36 39 48
$stoneMid = New-Color 70 76 88
$stoneLight = New-Color 112 120 133
$trimDark = New-Color 77 54 34
$trimMid = New-Color 138 96 54
$trimLight = New-Color 201 155 83
$accentDark = New-Color 16 44 58
$accentMid = New-Color 39 132 153
$accentLight = New-Color 137 235 224

Save-Texture "pocketwaystone-stone.png" {
    param($x, $y)
    $grain = (($x * 17 + $y * 29 + (($x -band 1) * 11)) % 19) / 18.0
    $shade = ($x + $y) / 30.0
    $base = Mix-Color $stoneDark $stoneMid (0.25 + ($grain * 0.45) + ($shade * 0.15))

    if (($x -eq 0) -or ($y -eq 0)) { return Mix-Color $base $stoneLight 0.22 }
    if (($x -eq 15) -or ($y -eq 15)) { return Mix-Color $base $stoneDark 0.28 }
    if ((($x + ($y * 3)) % 11) -eq 0) { return Mix-Color $base $stoneLight 0.18 }
    if ((($x * 5 + $y) % 13) -eq 0) { return Mix-Color $base $stoneDark 0.20 }
    return $base
}

Save-Texture "pocketwaystone-trim.png" {
    param($x, $y)
    $band = if (($x -eq 0) -or ($x -eq 15) -or ($y -eq 0) -or ($y -eq 15)) { 0.85 } elseif (($x + $y) % 5 -eq 0) { 0.58 } else { 0.38 }
    $base = Mix-Color $trimDark $trimMid $band
    if (($x -ge 2) -and ($x -le 13) -and (($y -eq 3) -or ($y -eq 12))) { return Mix-Color $base $trimLight 0.45 }
    if (($y -lt 3) -or ($x -lt 2)) { return Mix-Color $base $trimLight 0.22 }
    if (($y -gt 12) -or ($x -gt 13)) { return Mix-Color $base $trimDark 0.28 }
    return $base
}

Save-Texture "pocketwaystone-accent.png" {
    param($x, $y)
    $dx = $x - 7.5
    $dy = $y - 7.5
    $distance = [Math]::Sqrt(($dx * $dx) + ($dy * $dy)) / 10.6
    $glow = 1.0 - [Math]::Min(1.0, $distance)
    $pulse = ((($x * 9 + $y * 7) % 17) / 16.0) * 0.18
    $base = Mix-Color $accentDark $accentMid (0.25 + ($glow * 0.55) + $pulse)

    if (($x -eq 0) -or ($x -eq 15) -or ($y -eq 0) -or ($y -eq 15)) { return Mix-Color $base $accentDark 0.45 }
    if (($x -ge 6) -and ($x -le 9) -and ($y -ge 6) -and ($y -le 9)) { return Mix-Color $base $accentLight 0.55 }
    if ((($x + $y) % 7) -eq 0) { return Mix-Color $base $accentLight 0.22 }
    return $base
}
