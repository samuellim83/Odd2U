$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $root 'build.ps1')
$exe = Join-Path $root 'build\Odd2U.exe'
$output = Join-Path $root 'output'
$process = Start-Process -FilePath $exe -ArgumentList @('--verify', ('"' + $output + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw ('Verification failed. Read ' + (Join-Path $root 'build\odd-error.log')) }
Get-Content -LiteralPath (Join-Path $output 'verification.txt')
