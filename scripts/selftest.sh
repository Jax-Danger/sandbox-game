#!/bin/sh
# Builds and runs the headless self-test at low CPU priority. Usage: scripts/selftest.sh [--soak <secs> <seed>]
# Needs a Prowl clone at ~/Projects/Prowl (or PROWL_DIR) and a Vulkan driver.
set -e
cd "$(dirname "$0")/selftest"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
nice -n 15 dotnet build -v q -nologo ${PROWL_DIR:+-p:ProwlDir=$PROWL_DIR} | grep -E " error |Error\(s\)" || true
exec nice -n 15 dotnet bin/Debug/net10.0/Sandbox.dll --headless "$@"
