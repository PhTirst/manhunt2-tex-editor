param([Parameter(Mandatory=$true)][string]$Archive)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'ReleaseHelpers.ps1')
$archivePath = (Resolve-Path -LiteralPath $Archive).Path
Assert-ReleaseArchive $archivePath
$expected = ((Get-Content -LiteralPath ($archivePath + '.sha256') -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expected) { throw 'Archive checksum mismatch.' }
$extract = Join-Path $root ('out\verify\' + [guid]::NewGuid().ToString('N'))
Reset-Output $extract $root
Expand-Archive -LiteralPath $archivePath -DestinationPath $extract
$folders = @(Get-ChildItem -LiteralPath $extract -Directory)
if ($folders.Count -ne 1) { throw 'Expected exactly one top-level package folder.' }
$directory = $folders[0].FullName
$manifest = Get-Content -LiteralPath (Join-Path $directory 'release-manifest.json') -Raw | ConvertFrom-Json
$prefix = $directory.TrimEnd('\') + '\'
foreach ($entry in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $directory $entry.path))
    if (-not $path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes package.' }
    $file = Get-Item -LiteralPath $path
    if ($file.Length -ne $entry.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Runtime file mismatch: $($entry.path)" }
}
$actual = @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object Name -ne 'release-manifest.json')
if ($actual.Count -ne @($manifest.files).Count) { throw 'Manifest does not cover every file.' }
if ($manifest.application -eq 'TtfToManhunt') {
    Invoke-Headless (Join-Path $directory 'TtfToManhunt.Gui.exe') '--drawing-smoke'
    $cli = Join-Path $directory 'TtfToManhunt.exe'
    & $cli --help | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'CLI help failed.' }
    $csv = Join-Path $extract 'languages.csv'
    $gxt = Join-Path $extract 'english.gxt'
    "Key,en_US,zh_CN`r`nKEY,Alpha,LocalizedA`r`nINPRO,Beta,LocalizedB" | Set-Content -LiteralPath $csv -Encoding UTF8
    & $cli csv2gxt $csv $gxt --language en_US
    if ($LASTEXITCODE -ne 0) { throw 'CLI language selection failed.' }
    $bytes = [IO.File]::ReadAllBytes($gxt)
    if ([BitConverter]::ToInt32($bytes,4) -ne 24 -or [Text.Encoding]::Unicode.GetString($bytes,40,$bytes.Length-40) -cne "Alpha`0Beta`0") { throw 'KEY/INPRO or selected language was damaged.' }
    'Key,Value','123456789,invalid' | Set-Content -LiteralPath $csv -Encoding UTF8
    $bad = Join-Path $extract 'invalid.gxt'
    Invoke-Headless $cli "csv2gxt `"$csv`" `"$bad`"" 2
    if (Test-Path -LiteralPath $bad) { throw 'Invalid Key was not safely rejected.' }
}
elseif ($manifest.application -eq 'Mh2TexEditor') {
    Invoke-Headless (Join-Path $directory 'Mh2TexEditor.exe') '--self-test'
}
else { throw 'Unknown package application.' }
Write-Host "PASS extracted package: $($manifest.application) $($manifest.version)"
