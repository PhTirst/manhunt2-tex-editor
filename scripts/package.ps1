param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseHelpers.ps1')
& (Join-Path $PSScriptRoot 'test.ps1')
$version = ([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$name = "manhunt2-tex-editor-$version-windows-x64"
$stage = Join-Path $root ("out\package\$name")
Reset-Output $stage $root
& dotnet publish (Join-Path $root 'src\Mh2TexEditor\Mh2TexEditor.csproj') -c Release -r win-x64 --self-contained true -o $stage -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'GUI publish failed.' }
Copy-Documentation $root $stage
if (-not (Test-Path -LiteralPath (Join-Path $stage 'mh2core.dll'))) { throw 'Native TEX core is missing.' }
Write-ReleaseManifest $stage 'Mh2TexEditor' $version
$release = Join-Path $root 'out\release'
New-Item -ItemType Directory -Force -Path $release | Out-Null
$archive = Join-Path $release ($name + '.zip')
New-ToolArchive $stage $archive
Assert-ReleaseArchive $archive
$source = New-SourceArchive $root 'manhunt2-tex-editor' $version
Assert-ReleaseArchive $source -Source
& (Join-Path $PSScriptRoot 'verify-package.ps1') -Archive $archive
Write-Host "Tool: $archive"
Write-Host "Source: $source"
