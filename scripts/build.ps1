param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$native = Join-Path $root 'out\native'
& cmake -S $root -B $native -G 'Visual Studio 17 2022' -A x64 -T ClangCL
if ($LASTEXITCODE -ne 0) { throw 'Native configure failed. Install VS 2022 C++ Build Tools, clang-cl, CMake and a Windows SDK.' }
& cmake --build $native --config $Configuration --parallel ([Environment]::ProcessorCount)
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
foreach ($project in @('src\Mh2TexEditor\Mh2TexEditor.csproj','tests\Mh2TexEditor.Tests.csproj')) {
    & dotnet build (Join-Path $root $project) -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
