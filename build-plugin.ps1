$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'tools/nuget'
$sdk = Join-Path $PSScriptRoot 'tools/dotnet/dotnet.exe'
& $sdk build (Join-Path $PSScriptRoot 'plugin/GeneratorWorker') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Generator worker build failed.' }
& $sdk build (Join-Path $PSScriptRoot 'plugin/UltimateEditor') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$savePaths = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..') -Filter '*.sav' -File | ForEach-Object { $_.FullName })
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --linked-only @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Linked validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --language-only @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Trainer language validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --distribution-filter-only @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Distribution filter validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --generation-only @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Generation validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --dates-only @savePaths
if ($LASTEXITCODE -ne 0) { throw 'Distribution date validation failed.' }
& $sdk run --project (Join-Path $PSScriptRoot 'plugin/SmokeTests') -c Release -- --gen7-dates-only
if ($LASTEXITCODE -ne 0) { throw 'Gen7 original-date validation failed.' }
$releaseDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin/UltimateEditor/bin/Release/net10.0-windows/UltimateEditor.dll') -Destination $releaseDir
Write-Host "Validated plugin: $releaseDir/UltimateEditor.dll"

$generatorDist = Join-Path $releaseDir 'generator-package'
New-Item -ItemType Directory -Force -Path $generatorDist | Out-Null
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'plugin/GeneratorWorker/bin/Release/net10.0') -File | Where-Object Extension -ne '.pdb' | Copy-Item -Destination $generatorDist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE-PokeFinder.txt'), (Join-Path $PSScriptRoot 'THIRD_PARTY_NOTICES.md') -Destination $generatorDist -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'vendor/pkhex/kwsch-PKHeX-bfe89c7/LICENSE') -Destination (Join-Path $generatorDist 'LICENSE-PKHeX.txt') -Force
Write-Host "Validated generator: $generatorDist"
