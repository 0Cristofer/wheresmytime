#!/usr/bin/env sh
# Runs the app from source against a throwaway data folder (samples/sandbox), so trying
# things out never touches your real entries. In the app, click Import and pick
# samples/sample-month.csv (about a month) or samples/sample-entries.csv (12 weeks).
#
# Usage: ./samples/try.sh [--reset]      (--reset empties the sandbox first)
set -e
here="$(cd "$(dirname "$0")" && pwd)"
sandbox="$here/sandbox"
[ "$1" = "--reset" ] && rm -rf "$sandbox"
mkdir -p "$sandbox"

echo "Data folder: $sandbox"
echo "Samples to import (one at a time): $here/sample-month.csv or $here/sample-entries.csv"
WHERESMYTIME_DATA_DIR="$sandbox" dotnet run --project "$here/../src/WheresMyTime"
