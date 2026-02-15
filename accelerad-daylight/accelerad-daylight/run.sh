#!/usr/bin/env bash
# Run an accelerad-daylight simulation with sample test data
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Default Radiance location on macOS
RADIANCE_DIR="${RADIANCE_DIR:-/usr/local/radiance/bin}"

echo "Running Accelerad Daylight CLI..."
echo "  Radiance: $RADIANCE_DIR"
echo ""

dotnet run --project "$SCRIPT_DIR/src/Accelerad.Daylight.Cli" -- run \
  --obj "$SCRIPT_DIR/testdata/box.obj" \
  --epw "$SCRIPT_DIR/testdata/boston.epw" \
  --sensors "$SCRIPT_DIR/testdata/sensors.csv" \
  --output "$SCRIPT_DIR/results" \
  --radiance-dir "$RADIANCE_DIR" \
  --keep-temp
