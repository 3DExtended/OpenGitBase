#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://localhost:8089}"
OWNER="${OWNER:-admin}"
REPO_SLUG="${REPO_SLUG:-open-git-base}"
OUTPUT_DIR="$(mktemp -d)"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

trap 'rm -rf "${OUTPUT_DIR}" "${OGB_CONFIG_DIR:-}"; if [[ "$(uname)" == "Darwin" ]]; then security delete-generic-password -s "opengitbase-cli/${API_URL}" 2>/dev/null || true; fi' EXIT

"${ROOT}/scripts/test-ogb-cli-docs-e2e.sh"

echo "==> Comparing pulled inventory to git mirror markers"
ogb --hostname "${API_URL}" docs -R "${OWNER}/${REPO_SLUG}" pull --output-dir "${OUTPUT_DIR}" --json > "${OUTPUT_DIR}/inventory.json"

python3 - <<'PY' "${ROOT}" "${OUTPUT_DIR}/inventory.json"
import json
import re
import sys
from pathlib import Path

root = Path(sys.argv[1])
inventory = json.loads(Path(sys.argv[2]).read_text())
paths = {item["path"].replace("\\", "/") for item in inventory.get("files", [])}
marker = re.compile(r"<!--\s*forge:\s*#\d+\s*-->")
stale = []
for relative_root in ("docs/prd", "docs/adr", "docs/issues"):
    absolute_root = root / relative_root
    if not absolute_root.exists():
        continue
    for file in absolute_root.rglob("*.md"):
        rel = file.relative_to(root).as_posix()
        text = file.read_text(encoding="utf-8")
        if marker.search(text) and rel not in paths:
            stale.append(rel)

if stale:
    print("Stale mirror files not returned by ogb docs pull:", file=sys.stderr)
    for path in stale:
        print(f"  - {path}", file=sys.stderr)
    raise SystemExit(1)

print("Docs mirror freshness check passed.")
PY
