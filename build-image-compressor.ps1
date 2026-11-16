$ErrorActionPreference = 'Stop'
$cache = Join-Path $PSScriptRoot 'packages\image-compressor'
$output = Join-Path $PSScriptRoot 'release\ImageCompressor'
New-Item -ItemType Directory -Force -Path $cache,$output | Out-Null
function Get-Package($id, $version, $name) {
    $zip = Join-Path $cache "$name.zip"
    $destination = Join-Path $cache $name
    if (!(Test-Path (Join-Path $destination '.ready'))) {
        $url = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
        if (!(Test-Path $zip)) {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            try { Invoke-WebRequest $url -OutFile $zip -UseBasicParsing }
            catch {
                if (!(Get-Command python -ErrorAction SilentlyContinue)) { throw }
                & python -c 'import sys,urllib.request; urllib.request.urlretrieve(sys.argv[1],sys.argv[2])' $url $zip
                if ($LASTEXITCODE -ne 0) { throw "Download failed: $id" }
            }
        }
        Expand-Archive -LiteralPath $zip -DestinationPath $destination -Force
        Set-Content (Join-Path $destination '.ready') $version
    }
    return $destination
}
$magick = Get-Package 'magick.net-q8-x64' '15.1.0' 'magick'
$core = Get-Package 'magick.net.core' '15.1.0' 'core'
$roslyn = Get-Package 'microsoft.net.compilers.toolset' '4.14.0' 'compiler'
$facade = Get-ChildItem "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\netstandard" -Filter netstandard.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (!$facade) { throw '.NET Framework 4.8 is required to build this tool.' }
Copy-Item (Join-Path $magick 'lib\netstandard20\Magick.NET-Q8-x64.dll') $output
Copy-Item (Join-Path $core 'lib\netstandard20\Magick.NET.Core.dll') $output
Copy-Item (Join-Path $magick 'runtimes\win-x64\native\Magick.Native-Q8-x64.dll') $output
Copy-Item (Join-Path $magick 'Notice.txt') (Join-Path $output 'Magick.NET-Notice.txt')
Add-Type -AssemblyName System.Drawing
$icon = Join-Path $PSScriptRoot 'assets\icons\image-compressor.ico'
$bitmap = New-Object Drawing.Bitmap 64,64
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([Drawing.Color]::FromArgb(24,40,65))
$white = [Drawing.Brushes]::White
$green = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(45,212,160))
$g.FillRectangle($white,9,12,46,36)
$g.FillRectangle($green,13,16,38,28)
$g.FillEllipse($white,36,19,8,8)
$g.FillPolygon($white,[Drawing.Point[]]@([Drawing.Point]::new(15,41),[Drawing.Point]::new(27,25),[Drawing.Point]::new(40,41)))
$pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255,190,70)),4
$g.DrawLine($pen,20,54,44,54)
$g.DrawLines($pen,[Drawing.Point[]]@([Drawing.Point]::new(25,49),[Drawing.Point]::new(20,54),[Drawing.Point]::new(25,59)))
$g.DrawLines($pen,[Drawing.Point[]]@([Drawing.Point]::new(39,49),[Drawing.Point]::new(44,54),[Drawing.Point]::new(39,59)))
$png = Join-Path $PSScriptRoot 'assets\icons\image-compressor.png'
$bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
$data = [IO.File]::ReadAllBytes($png)
$writer = [IO.BinaryWriter]::new([IO.File]::Create($icon))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$data.Length); $writer.Write([uint32]22); $writer.Write($data)
} finally { $writer.Dispose(); $g.Dispose(); $bitmap.Dispose(); $green.Dispose(); $pen.Dispose() }
$compiler = Join-Path $roslyn 'tasks\net472\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$facade" "/reference:$output\Magick.NET-Q8-x64.dll" "/reference:$output\Magick.NET.Core.dll" "/win32icon:$icon" "/out:$output\ImageCompressor.exe" (Join-Path $PSScriptRoot 'ImageCompressor.cs')
if ($LASTEXITCODE -ne 0) { throw 'ImageCompressor compilation failed.' }
@'
<?xml version="1.0" encoding="utf-8"?>
<configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>
'@ | Set-Content (Join-Path $output 'ImageCompressor.exe.config') -Encoding UTF8
Copy-Item (Join-Path $PSScriptRoot 'docs\image-compressor\README.md') (Join-Path $output 'README.md')
Copy-Item (Join-Path $PSScriptRoot 'docs\image-compressor\LICENSE-Magick.NET.txt') $output
Write-Host "Build complete: $output\ImageCompressor.exe"
