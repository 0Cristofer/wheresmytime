# Runs the app from source against a throwaway data folder (samples/sandbox), so trying
# things out never touches your real entries. In the app, click Import and pick
# samples/sample-entries.csv.
#
# Usage: powershell -ExecutionPolicy Bypass -File samples/try.ps1 [-Reset]
#        -Reset empties the sandbox first.
param([switch]$Reset)
$ErrorActionPreference = 'Stop'

$sandbox = Join-Path $PSScriptRoot 'sandbox'
if ($Reset -and (Test-Path $sandbox)) { Remove-Item $sandbox -Recurse -Force }
New-Item -ItemType Directory -Force $sandbox | Out-Null

$env:WHERESMYTIME_DATA_DIR = $sandbox
Write-Host "Data folder: $sandbox"
Write-Host "Sample to import: $(Join-Path $PSScriptRoot 'sample-entries.csv')"
dotnet run --project (Join-Path $PSScriptRoot '..\src\WheresMyTime')
