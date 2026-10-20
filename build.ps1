# Project Mantis build script — https://github.com/kemalselimkoc/Project-Mantis
# Copyright (c) 2026 kemalselimkoc. MIT licensed.
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $project 'dist'
$obj = Join-Path $project 'obj'
New-Item -ItemType Directory -Force -Path $out, $obj | Out-Null

$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$gac = 'C:\Windows\Microsoft.NET\assembly'

function Find-GacAssembly([string]$name) {
    $file = Get-ChildItem $gac -Recurse -Filter "$name.dll" -ErrorAction Stop | Select-Object -First 1
    if (-not $file) { throw "Required framework assembly not found: $name" }
    return $file.FullName
}

$refs = @(
    (Find-GacAssembly 'PresentationCore'),
    (Find-GacAssembly 'PresentationFramework'),
    (Find-GacAssembly 'WindowsBase'),
    (Find-GacAssembly 'System.Xaml')
)

$refArgs = $refs | ForEach-Object { "/reference:$_" }
$resources = Join-Path $obj 'ProjectMantis.g.resources'

Add-Type -AssemblyName PresentationCore
$png = Join-Path $project 'assets\mantis-logo.png'
$stream = [System.IO.File]::OpenRead($png)
try {
    $decoder = [System.Windows.Media.Imaging.PngBitmapDecoder]::new($stream, [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $frame = $decoder.Frames[0]
    $writer = [System.Resources.ResourceWriter]::new($resources)
    $imageStream = [System.IO.MemoryStream]::new()
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add($frame)
    $encoder.Save($imageStream)
    $imageStream.Position = 0
    $writer.AddResource('assets/mantis-logo.png', $imageStream.ToArray())
    $writer.Close()
} finally { $stream.Dispose() }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /win32icon:"$project\assets\mantis.ico" /out:"$out\ProjectMantis-v1.3.1.exe" @refArgs "$project\App.xaml.cs" "$project\MainWindow.cs"
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }

Copy-Item "$project\README.md" "$out\README.txt" -Force
New-Item -ItemType Directory -Force -Path "$out\assets" | Out-Null
if (-not (Test-Path "$out\assets\mantis-logo.png")) { Copy-Item "$project\assets\mantis-logo.png" "$out\assets\mantis-logo.png" }
Write-Host "Built: $out\ProjectMantis-v1.3.1.exe"
