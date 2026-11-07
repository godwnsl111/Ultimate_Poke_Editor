$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginDir = Join-Path $projectRoot 'plugins'
$distDir = Join-Path $PSScriptRoot 'dist'
$sourcePlugin = Join-Path $distDir 'UltimateEditor.dll'
$runtimeGenerator = Join-Path $distDir 'UltimateEditor.Generator'
$sourceGenerator = Join-Path $distDir 'generator-package'
if (!(Test-Path -LiteralPath (Join-Path $sourceGenerator 'UltimateEditor.Generator.exe'))) { $sourceGenerator = $runtimeGenerator }
if (!(Test-Path -LiteralPath $sourcePlugin) -or !(Test-Path -LiteralPath (Join-Path $sourceGenerator 'UltimateEditor.Generator.exe'))) {
    throw 'Validated plugin/generator package is missing. Run build-plugin.ps1 first.'
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
# Keep compiled helper assets inside ultimate_editor, outside PKHeX plugin scanning.
try { Copy-Item -LiteralPath $sourcePlugin -Destination (Join-Path $pluginDir 'UltimateEditor.dll') -Force }
catch { throw 'PKHeX is using UltimateEditor.dll. Close PKHeX and run install-plugin.ps1 again.' }
# Promote worker files only after the plugin can be replaced; a running older
# plugin must not receive a worker with different card identifiers.
if ($sourceGenerator -ne $runtimeGenerator) {
    New-Item -ItemType Directory -Force -Path $runtimeGenerator | Out-Null
    Get-ChildItem -LiteralPath $sourceGenerator -File | Copy-Item -Destination $runtimeGenerator -Force
}
Write-Host 'Installed Ultimate Editor. Keep ultimate_editor/dist/UltimateEditor.Generator and restart PKHeX.'
