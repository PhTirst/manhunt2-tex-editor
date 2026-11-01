$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-InOutput([string]$Path, [string]$Root) {
    $full = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath((Join-Path $Root 'out')).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Path is outside out/: $full" }
}

function Reset-Output([string]$Path, [string]$Root) {
    Assert-InOutput $Path $Root
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Copy-Documentation([string]$Root, [string]$Destination) {
    foreach ($name in @('README.md','README.zh-CN.md','LICENSE','NOTICE.md','CHANGELOG.md','docs','examples')) {
        $source = Join-Path $Root $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $Destination -Recurse -Force }
    }
    $notices = Join-Path $Destination 'THIRD_PARTY'
    New-Item -ItemType Directory -Force -Path $notices | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $Root 'third_party') -File | Copy-Item -Destination $notices
    $dotnet = Split-Path -Parent (Get-Command dotnet).Source
    foreach ($name in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        $source = Join-Path $dotnet $name
        if (-not (Test-Path -LiteralPath $source)) { throw "Missing .NET notice: $source" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $notices ('dotnet-' + $name))
    }
}

function Write-ReleaseManifest([string]$Directory, [string]$Name, [string]$Version) {
    $base = [IO.Path]::GetFullPath($Directory).TrimEnd('\') + '\'
    $files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object Name -ne 'release-manifest.json' | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path=$_.FullName.Substring($base.Length).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    [ordered]@{ application=$Name; version=$Version; runtime='win-x64'; selfContained=$true; files=$files } |
        ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Directory 'release-manifest.json') -Encoding UTF8
}

function New-ToolArchive([string]$Directory, [string]$Archive) {
    if (Test-Path -LiteralPath $Archive) { Remove-Item -LiteralPath $Archive -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $base = [IO.Path]::GetFullPath($Directory).TrimEnd('\') + '\'
    $name = Split-Path -Leaf $Directory
    $zip = [IO.Compression.ZipFile]::Open($Archive, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force) {
            $entry = $name + '/' + $file.FullName.Substring($base.Length).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
        foreach ($folder in Get-ChildItem -LiteralPath $Directory -Recurse -Directory -Force) {
            if (-not (Get-ChildItem -LiteralPath $folder.FullName -Force | Select-Object -First 1)) {
                $zip.CreateEntry($name + '/' + $folder.FullName.Substring($base.Length).Replace('\','/') + '/') | Out-Null
            }
        }
    }
    finally { $zip.Dispose() }
    $hash = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $([IO.Path]::GetFileName($Archive))" | Set-Content -LiteralPath ($Archive + '.sha256') -Encoding ASCII
}

function New-SourceArchive([string]$Root, [string]$Name, [string]$Version) {
    $stage = Join-Path $Root ('out\source-stage\' + $Name)
    Reset-Output $stage $Root
    $folders = @('src','tests','scripts','docs','examples','.github','cmake','third_party')
    $rootNames = @('README.md','README.zh-CN.md','LICENSE','NOTICE.md','CHANGELOG.md','Directory.Build.props','global.json','CMakeLists.txt','TtfToManhunt.sln','Mh2TexEditor.sln','.gitignore','.gitattributes')
    $files = @(Get-ChildItem -LiteralPath $Root -File -Force | Where-Object Name -in $rootNames)
    foreach ($folder in $folders) {
        $path = Join-Path $Root $folder
        if (Test-Path -LiteralPath $path) { $files += Get-ChildItem -LiteralPath $path -Recurse -File -Force }
    }
    $prefix = $Root.TrimEnd('\') + '\'
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($prefix.Length)
        if ($relative -match '(^|[\\/])(bin|obj|out|\.git)([\\/]|$)' -or $file.Extension -in @('.exe','.dll','.pdb','.log','.ttf','.otf','.dat','.txd','.gxt','.tex','.dds','.zip','.7z')) { continue }
        $destination = Join-Path $stage $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    $archive = Join-Path $Root ("out\release\$Name-$Version-source.zip")
    New-ToolArchive $stage $archive
    return $archive
}

function Assert-ReleaseArchive([string]$Archive, [switch]$Source) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '(^[\\/]|:|(^|[\\/])\.\.([\\/]|$))') { throw "Unsafe ZIP path: $($entry.FullName)" }
            if ($entry.FullName -match '(?i)(^|/)(bin|obj|\.git|test-output|deploy_backups)/|\.(ttf|otf|dat|txd|gxt|tex|dds|pdb|log|7z)$') { throw "Unwanted release entry: $($entry.FullName)" }
            if ($Source -and $entry.FullName -match '(?i)\.(exe|dll|zip)$') { throw "Binary in source archive: $($entry.FullName)" }
        }
        if (-not ($zip.Entries | Where-Object { $_.FullName.Replace('\','/') -match '/README.zh-CN.md$' })) { throw 'Missing Chinese README.' }
        if (-not ($zip.Entries | Where-Object { $_.FullName.Replace('\','/') -match '/LICENSE$' })) { throw 'Missing license.' }
    }
    finally { $zip.Dispose() }
}

function Invoke-Headless([string]$Executable, [string]$Arguments, [int]$ExpectedExitCode = 0) {
    $process = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru
    try {
        if (-not $process.WaitForExit(30000)) { $process.Kill(); $process.WaitForExit(); throw "Headless self-test timed out: $Executable" }
        if ($process.ExitCode -ne $ExpectedExitCode) { throw "Headless self-test failed ($($process.ExitCode), expected $ExpectedExitCode): $Executable" }
    }
    finally { $process.Dispose() }
}
