$ErrorActionPreference = 'Stop'

$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }

$outputDirectory = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$sshLibrary = Join-Path $PSScriptRoot 'packages\SSH.NET.2020.0.2\lib\net40\Renci.SshNet.dll'
if (-not (Test-Path -LiteralPath $sshLibrary)) { throw "SSH.NET library not found: $sshLibrary" }
$outputFile = Join-Path $outputDirectory 'SshManager.exe'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\ssh-manager.ico')" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Security.dll `
    "/reference:$sshLibrary" "/resource:$sshLibrary,SshManager.Renci.SshNet.dll" "/out:$outputFile" (Join-Path $PSScriptRoot 'SshManager.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
Remove-Item -LiteralPath (Join-Path $outputDirectory 'Renci.SshNet.dll') -Force -ErrorAction SilentlyContinue
Write-Host "Build complete: $outputFile" -ForegroundColor Green
