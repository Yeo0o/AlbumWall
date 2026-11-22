param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string] $Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'AlbumWall.csproj'
$output = Join-Path $PSScriptRoot "build/$Runtime"
$publishArgs = @(
    'publish', $project, '-c', 'Release', '-r', $Runtime, '--self-contained', 'true',
    '-o', $output, '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None'
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}
Copy-Item (Join-Path $output 'AlbumWall.exe') (Join-Path $output 'AlbumWall.scr') -Force
Write-Output (Join-Path $output 'AlbumWall.scr')
