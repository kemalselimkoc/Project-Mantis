# Project Mantis icon packer — https://github.com/kemalselimkoc/Project-Mantis
param([Parameter(Mandatory=$true)][string]$PngPath, [Parameter(Mandatory=$true)][string]$IcoPath)
$bytes = [System.IO.File]::ReadAllBytes($PngPath)
$stream = [System.IO.File]::Create($IcoPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]1)
    $writer.Write([Byte]0); $writer.Write([Byte]0); $writer.Write([Byte]0); $writer.Write([Byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$bytes.Length); $writer.Write([UInt32]22)
    $writer.Write($bytes)
} finally { $writer.Dispose(); $stream.Dispose() }
