param([string]$OutputDirectory = 'build')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (Test-Path -LiteralPath (Join-Path $root 'BUILD-HOLD.txt')) { throw 'Build held pending Avast detection review. See AVAST-REVIEW.md.' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework 4.x C# compiler is required.' }
$destination = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll','System.Xml.dll','System.Xml.Linq.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$speech = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Speech') -Filter System.Speech.dll -Recurse | Select-Object -First 1
if (-not $speech) { throw 'Windows .NET Framework System.Speech is required.' }
$references += '/reference:' + $speech.FullName
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path (Join-Path $framework 'WPF') $_) }
$sources = Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/win32manifest:' + (Join-Path $root 'app.manifest')) ('/out:' + (Join-Path $destination 'Odd2U.exe')) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'odd-personality.txt') -Destination $destination -Force
Write-Output ('Built ' + (Join-Path $destination 'Odd2U.exe'))
