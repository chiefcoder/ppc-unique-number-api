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

case "${1:-both}" in
  api)
    dotnet run --project src/PaidUniqueNumber.Api
    ;;
  client)
    dotnet run --project src/PaidUniqueNumber.Client
    ;;
  both)
    dotnet run --project src/PaidUniqueNumber.Api &
    API_PID=$!
    trap 'kill "$API_PID" 2>/dev/null || true' EXIT

    until curl -sf http://127.0.0.1:4242/health >/dev/null 2>&1; do
      if ! kill -0 "$API_PID" 2>/dev/null; then
        echo "API failed to start."
        exit 1
      fi
      echo "Waiting for API to start..."
      sleep 1
    done

    dotnet run --project src/PaidUniqueNumber.Client
    ;;
  *)
    echo "Usage: ./run-local.sh [api|client|both]"
    exit 1
    ;;
esac
