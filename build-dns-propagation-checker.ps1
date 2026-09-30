$ErrorActionPreference = 'Stop'
$compiler = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }
$output = Join-Path $PSScriptRoot 'release\DnsPropagationChecker.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\domain-dns-tool.ico')" `
    "/out:$output" (Join-Path $PSScriptRoot 'DnsPropagationChecker.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
Write-Host "Build complete: $output"
