#!/usr/bin/env bash
# Symlink project git hooks into .git/hooks for this repository.

set -euo pipefail

ROOT=$(git rev-parse --show-toplevel)
HOOKS_SRC="${ROOT}/scripts/git-hooks"
HOOKS_DST="${ROOT}/.git/hooks"

if [ ! -d "${HOOKS_DST}" ]; then
  echo "error: ${HOOKS_DST} not found — is this a git repository?" >&2
  exit 1
fi

for hook in "${HOOKS_SRC}"/*; do
  [ -f "${hook}" ] || continue
  name=$(basename "${hook}")
  chmod +x "${hook}"
  ln -sf "../../scripts/git-hooks/${name}" "${HOOKS_DST}/${name}"
  echo "installed ${name}"
done

echo "Done. DCO Signed-off-by will be added automatically on commit."
