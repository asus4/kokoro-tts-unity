#!/bin/bash -e

# Update locally developed Catalyst library in Unity project (as NuGet for Unity style)

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
SRC_DIR="$SCRIPT_DIR/../catalyst/Catalyst/"
DST_DIR="$SCRIPT_DIR/Assets/Packages/Catalyst.1.0.54164/lib/netstandard2.1/"


cd $SRC_DIR
pwd

dotnet build
dotnet pack
cp bin/Debug/netstandard2.1/Catalyst.dll "$DST_DIR"
# dotnet build -c Release
# dotnet pack -c Release

echo "Build and packaging completed successfully."
exit 0
