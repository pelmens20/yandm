$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw '.NET 8 SDK is required. Install: https://dotnet.microsoft.com/download/dotnet/8.0' }
$version = (& dotnet --list-sdks | Select-String '^8\.' | Select-Object -First 1)
if (-not $version) { throw '.NET 8 SDK was not found.' }
dotnet publish .\YandexMusicController.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
Write-Host 'Build complete: publish\YandexMusicController.exe' -ForegroundColor Green
