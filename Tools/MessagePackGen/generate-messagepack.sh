#!/bin/bash -e


SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Script directory: $SCRIPT_DIR" 

# git clone --depth 1 https://github.com/curiosity-ai/catalyst.git

dotnet tool restore
# dotnet mpc --help
dotnet mpc -i "$SCRIPT_DIR/MessagePackGen.csproj" -o "$SCRIPT_DIR/../../Packages/com.github.asus4.kokoro-tts/Runtime/Generated/MessagePack.Generated.cs" -m
