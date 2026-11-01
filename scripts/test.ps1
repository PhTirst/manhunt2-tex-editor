param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration }
$output = Join-Path $root ('out\test-results\' + [guid]::NewGuid().ToString('N'))
& dotnet run --project (Join-Path $root 'tests\Mh2TexEditor.Tests.csproj') -c $Configuration --no-build -- $output
if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
