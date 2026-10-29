#!/usr/bin/env sh
# Builds one self-contained executable per platform into dist/<runtime>/.
# Usage: ./publish.sh                  (all platforms)
#        ./publish.sh linux-x64        (just one)
set -e
cd "$(dirname "$0")"
runtimes=${*:-"win-x64 linux-x64 osx-arm64 osx-x64"}

for rid in $runtimes; do
    echo "Publishing $rid..."
    dotnet publish src/WheresMyTime/WheresMyTime.csproj -c Release -r "$rid" -o "dist/$rid"
done
ls -lh dist/*/
