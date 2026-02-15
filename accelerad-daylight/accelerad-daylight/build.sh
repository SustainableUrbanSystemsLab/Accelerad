#!/usr/bin/env bash
# Build the Accelerad Daylight solution
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Building Accelerad Daylight..."
dotnet build --configuration Release "$SCRIPT_DIR/AcceleradDaylight.sln"
chmod +x "$SCRIPT_DIR/src/Accelerad.Daylight.Cli/bin/Release/net8.0/Accelerad.Daylight.Cli"

echo ""
echo "Build complete."
echo "CLI binary: $SCRIPT_DIR/src/Accelerad.Daylight.Cli/bin/Release/net8.0/Accelerad.Daylight.Cli"
