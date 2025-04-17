#!/bin/bash -e

# Update locally developed Catalyst library in Unity project (as NuGet for Unity style)

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
SRC_DIR="$SCRIPT_DIR/../catalyst/Catalyst/"
DST_DIR="$SCRIPT_DIR/Assets/Packages/Catalyst.1.0.54164/lib/netstandard2.1/"
SRC_DIR_EN="$SCRIPT_DIR/../catalyst/Languages/English/"
DST_DIR_EN="$SCRIPT_DIR/Assets/Packages/Catalyst.Models.English.1.0.30952/lib/netstandard2.1/"

# Build Catalyst library
cd $SRC_DIR
pwd
dotnet build
dotnet pack
cp bin/Debug/netstandard2.1/Catalyst.dll "$DST_DIR"

# Build English data
cd $SRC_DIR_EN
pwd
dotnet build
dotnet pack
cp bin/Debug/netstandard2.1/Catalyst.Models.English.dll "$DST_DIR"


echo "Build and packaging completed successfully."
exit 0
