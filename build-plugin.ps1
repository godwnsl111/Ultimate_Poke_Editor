$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot 'tools/cli-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot 'tools/nuget'
$sdk = Join-Path $PSScriptRoot 'tools/dotnet/dotnet.exe'
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
$releaseDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'plugin/UltimateEditor/bin/Release/net10.0-windows/UltimateEditor.dll') -Destination $releaseDir
Write-Host "Validated plugin: $releaseDir/UltimateEditor.dll"
