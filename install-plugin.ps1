$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginDir = Join-Path $projectRoot 'plugins'
$distDir = Join-Path $PSScriptRoot 'dist'
$sourcePlugin = Join-Path $distDir 'UltimateEditor.dll'
$sourceGenerator = Join-Path $distDir 'UltimateEditor.Generator'
if (!(Test-Path -LiteralPath $sourcePlugin) -or !(Test-Path -LiteralPath (Join-Path $sourceGenerator 'UltimateEditor.Generator.exe'))) {
    throw 'Validated plugin/generator package is missing. Run build-plugin.ps1 first.'
}
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
# Keep compiled helper assets inside ultimate_editor, outside PKHeX plugin scanning.
try { Copy-Item -LiteralPath $sourcePlugin -Destination (Join-Path $pluginDir 'UltimateEditor.dll') -Force }
catch { throw 'PKHeX is using UltimateEditor.dll. Close PKHeX and run install-plugin.ps1 again.' }
Write-Host 'Installed Ultimate Editor. Keep ultimate_editor/dist/UltimateEditor.Generator and restart PKHeX.'
