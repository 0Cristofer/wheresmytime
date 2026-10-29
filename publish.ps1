# Builds one self-contained executable per platform into dist/<runtime>/.
# Usage: ./publish.ps1                 (all platforms)
#        ./publish.ps1 win-x64         (just one)
param([string[]]$Runtimes = @('win-x64', 'linux-x64', 'osx-arm64', 'osx-x64'))
$ErrorActionPreference = 'Stop'

foreach ($rid in $Runtimes) {
    Write-Host "Publishing $rid..."
    dotnet publish "$PSScriptRoot/src/WheresMyTime/WheresMyTime.csproj" -c Release -r $rid -o "$PSScriptRoot/dist/$rid"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Get-ChildItem "$PSScriptRoot/dist" -Recurse -File | Select-Object @{ n = 'File'; e = { $_.FullName.Substring($PSScriptRoot.Length + 1) } }, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
