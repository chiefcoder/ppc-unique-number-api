#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [[ -f .env ]]; then
  set -a
  source .env
  set +a
elif [[ -f .env.example ]]; then
  echo "No .env file found; loading .env.example as a template."
  set -a
  source .env.example
  set +a
fi

dotnet run --project src/PaidUniqueNumber.Api "$@"
