$ErrorActionPreference = 'Stop'
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$testDirectory = Join-Path $PSScriptRoot ('.build\disk-cleanup-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$allowed = New-Item -ItemType Directory -Path (Join-Path $testDirectory 'allowed')
$outside = New-Item -ItemType Directory -Path (Join-Path $testDirectory 'outside')
New-Item -ItemType Junction -Path (Join-Path $allowed.FullName 'junction') -Target $outside.FullName | Out-Null
$exe = Join-Path $testDirectory 'Tests.exe'
& $compiler /nologo /target:exe /main:DiskCleanupHelperTests /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$exe" (Join-Path $PSScriptRoot 'DiskCleanupHelper.cs') (Join-Path $PSScriptRoot 'tests\DiskCleanupHelperTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
& $exe $testDirectory
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
Write-Host "Test artifacts: $testDirectory"
