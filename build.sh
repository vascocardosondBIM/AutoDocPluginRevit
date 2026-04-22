#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet run --project "$(dirname "$0")/_build/_build.csproj" -- "$@"
