#!/usr/bin/env bash
# Runs the canonical Part 2 Postman collection against a running stack
# (docker compose up -d, or func start --port 7071) via newman.
#
# Split into two newman invocations with a real shell sleep between them,
# rather than a delay inside the collection's own pre-request scripts: a
# busy-wait inside newman's own Node process long enough to cover
# OrderStatusStepDelaySeconds' ~9-10s worst case measurably starved the
# containers of CPU on this machine (the order was still "Received"
# afterwards instead of "Collected"). A real, non-blocking `sleep` doesn't
# have that problem. ("Orders: Status Transitions" has its own short,
# self-contained retry-on-404 poll loop instead of a guessed fixed delay -
# see the comments on that request in the collection itself for why a fixed
# delay there was flaky in both directions.)
#
# Usage: ./run-tests.sh [baseUrl]  (default: http://localhost:7071)
set -euo pipefail
cd "$(dirname "$0")"

BASE_URL="${1:-http://localhost:7071}"
ENV_FILE="CoffeeNChill-Part2.postman_environment.json"
WORK_ENV_POSIX="$(mktemp -t coffeenchill-part2-env.XXXXXX.json)"
trap 'rm -f "$WORK_ENV_POSIX"' EXIT

# On Git Bash for Windows, node.exe and newman are native Windows binaries:
# a POSIX-style /tmp/... path that bash understands natively is NOT the same
# path node.exe resolves - it needs the real Windows-style path instead.
# cygpath converts it; on real Linux/macOS (no cygpath) the POSIX path from
# mktemp is already correct, so the fallback just reuses it unchanged.
WORK_ENV="$(cygpath -w "$WORK_ENV_POSIX" 2>/dev/null || echo "$WORK_ENV_POSIX")"

# Passing these through the process environment rather than interpolating
# them directly into the Node script's source avoids a second, unrelated
# pitfall: a path embedded as a literal string inside script content doesn't
# go through the same argument handling a separate CLI argument would.
ENV_FILE="$ENV_FILE" WORK_ENV="$WORK_ENV" BASE_URL="$BASE_URL" node -e "
  const fs = require('fs');
  const env = JSON.parse(fs.readFileSync(process.env.ENV_FILE, 'utf8'));
  env.values = env.values.map(v => v.key === 'baseUrl' ? { ...v, value: process.env.BASE_URL } : v);
  fs.writeFileSync(process.env.WORK_ENV, JSON.stringify(env, null, 2));
"

echo "== Pass 1/2: Menu, Staff Documents, Orders: Queue, Orders: Status, Orders: Status Transitions =="
newman run CoffeeNChill-Part2.postman_collection.json \
  -e "$WORK_ENV" --export-environment "$WORK_ENV" \
  --folder "Menu" --folder "Staff Documents" --folder "Orders: Queue" \
  --folder "Orders: Status" --folder "Orders: Status Transitions"

echo "== Waiting 10s for the main order's lifecycle to reach Collected =="
sleep 10

echo "== Pass 2/2: Orders: Processing =="
newman run CoffeeNChill-Part2.postman_collection.json \
  -e "$WORK_ENV" \
  --folder "Orders: Processing"

echo "All passes completed successfully."
