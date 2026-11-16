#!/usr/bin/env bash
# Build dist/CydFlags.dll on Linux without Visual Studio.
#
# It compiles with a Roslyn csc.dll against the .NET Framework 4.8 reference
# assemblies and SimHub's SDK DLLs. Those inputs are not redistributed with this
# repository, so point the script at your local copies:
#
#   DOTNET_BIN  dotnet executable            (default: tools/dotnet/dotnet)
#   CSC         Roslyn csc.dll               (default: tools/dotnet/sdk/8.0.428/Roslyn/bincore/csc.dll)
#   REFASM      net48 reference assembly dir (default: tools/refasm/build/.NETFramework/v4.8)
#   SIMHUB_REF  folder with SimHub's DLLs    (default: refs/sh949/app)
#
# Example:
#   REFASM="$HOME/.nuget/.../v4.8" SIMHUB_REF="/opt/SimHub" bash tools/build/build-cydflags.sh
set -euo pipefail
cd "$(dirname "$0")/../.."

DOTNET_BIN="${DOTNET_BIN:-$PWD/tools/dotnet/dotnet}"
CSC="${CSC:-$PWD/tools/dotnet/sdk/8.0.428/Roslyn/bincore/csc.dll}"
REFASM="${REFASM:-$PWD/tools/refasm/build/.NETFramework/v4.8}"
SIMHUB_REF="${SIMHUB_REF:-$PWD/refs/sh949/app}"

export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$PWD/tools/dotnet-home}"

for f in "$CSC" "$REFASM/mscorlib.dll" "$SIMHUB_REF/GameReaderCommon.dll"; do
    [ -f "$f" ] || { echo "missing build dependency: $f" >&2; exit 1; }
done

mkdir -p dist
"$DOTNET_BIN" "$CSC" -nologo -target:library -nostdlib+ -langversion:7.3 \
    -warn:4 -deterministic -out:dist/CydFlags.dll \
    -reference:"$REFASM/mscorlib.dll" \
    -reference:"$REFASM/System.dll" \
    -reference:"$REFASM/System.Core.dll" \
    -reference:"$REFASM/System.Xml.dll" \
    -reference:"$REFASM/PresentationCore.dll" \
    -reference:"$REFASM/PresentationFramework.dll" \
    -reference:"$REFASM/WindowsBase.dll" \
    -reference:"$REFASM/System.Xaml.dll" \
    -reference:"$SIMHUB_REF/GameReaderCommon.dll" \
    -reference:"$SIMHUB_REF/SimHub.Plugins.dll" \
    -reference:"$SIMHUB_REF/SimHub.Logging.dll" \
    -reference:"$SIMHUB_REF/log4net.dll" \
    src/CydFlags/*.cs src/CydFlags/UI/*.cs src/CydFlags/Properties/AssemblyInfo.cs

echo "built dist/CydFlags.dll"
