#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Validate git push rules by calling the API internal push-validation endpoint."""

from __future__ import annotations

import json
import os
import subprocess
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

from storage_lfs import POINTER_MAX_BYTES, parse_pointer

API_URL_FILE = Path("/var/lib/opengitbase/api-url")
NULL_SHA = "0" * 40


def _read_api_url() -> str:
    if url := os.environ.get("OPENGITBASE_API_URL", "").strip():
        return url.rstrip("/")
    if API_URL_FILE.is_file():
        return API_URL_FILE.read_text(encoding="utf-8").strip().rstrip("/")
    return ""


def _run_git(git_dir: str, *args: str) -> str:
    result = subprocess.run(
        ["git", "--git-dir", git_dir, *args],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        stderr = (result.stderr or result.stdout or "").strip()
        raise RuntimeError(stderr or "git command failed")
    return result.stdout


def _commit_paths(git_dir: str, commit_sha: str) -> list[str]:
    output = _run_git(
        git_dir,
        "diff-tree",
        "--no-commit-id",
        "--name-only",
        "-r",
        commit_sha,
    )
    return [line.strip() for line in output.splitlines() if line.strip()]


def _commit_message(git_dir: str, commit_sha: str) -> str:
    return _run_git(git_dir, "log", "-1", "--format=%B", commit_sha).rstrip("\n")


def _max_blob_bytes(git_dir: str, commit_sha: str) -> int:
    objects = [
        line.split()[0]
        for line in _run_git(git_dir, "rev-list", "--objects", commit_sha).splitlines()
        if line.strip()
    ]
    if not objects:
        return 0

    process = subprocess.Popen(
        ["git", "--git-dir", git_dir, "cat-file", "--batch-check=%(objectsize)"],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
    )
    stdout, _ = process.communicate(input="\n".join(objects) + "\n", timeout=60)
    max_size = 0
    for line in stdout.splitlines():
        try:
            max_size = max(max_size, int(line.strip()))
        except ValueError:
            continue
    return max_size


def _commits_for_update(git_dir: str, old_sha: str, new_sha: str) -> list[str]:
    if new_sha == NULL_SHA:
        return []
    if old_sha == NULL_SHA:
        return [new_sha]
    return [
        line.strip()
        for line in _run_git(git_dir, "rev-list", f"{old_sha}..{new_sha}").splitlines()
        if line.strip()
    ]


def collect_commits(git_dir: str, updates: list[tuple[str, str, str]]) -> list[dict[str, Any]]:
    commits: list[dict[str, Any]] = []
    seen: set[str] = set()
    for old_sha, new_sha, _ref in updates:
        for commit_sha in _commits_for_update(git_dir, old_sha, new_sha):
            if commit_sha in seen:
                continue
            seen.add(commit_sha)
            commits.append(
                {
                    "sha": commit_sha,
                    "message": _commit_message(git_dir, commit_sha),
                    "changedPaths": _commit_paths(git_dir, commit_sha),
                    "maxBlobBytes": _max_blob_bytes(git_dir, commit_sha),
                }
            )
    return commits


def _new_objects(git_dir: str, new_sha: str) -> list[tuple[str, str]]:
    """Objects reachable from ``new_sha`` but from no existing ref, with the path each was found at. In
    pre-receive the refs are not updated yet, so ``--all`` is the history before the push."""
    objects: list[tuple[str, str]] = []
    for line in _run_git(git_dir, "rev-list", "--objects", new_sha, "--not", "--all").splitlines():
        sha, _, path = line.partition(" ")
        if sha and path:
            objects.append((sha, path))
    return objects


def _small_blobs(git_dir: str, shas: list[str]) -> set[str]:
    process = subprocess.run(
        ["git", "--git-dir", git_dir, "cat-file", "--batch-check=%(objectname) %(objecttype) %(objectsize)"],
        input="\n".join(shas) + "\n",
        capture_output=True,
        text=True,
        check=True,
        timeout=120,
    )
    small: set[str] = set()
    for line in process.stdout.splitlines():
        parts = line.split()
        if len(parts) == 3 and parts[1] == "blob" and int(parts[2]) <= POINTER_MAX_BYTES:
            small.add(parts[0])
    return small


def _blob_contents(git_dir: str, shas: list[str]) -> dict[str, bytes]:
    process = subprocess.run(
        ["git", "--git-dir", git_dir, "cat-file", "--batch"],
        input=("\n".join(shas) + "\n").encode("ascii"),
        capture_output=True,
        check=True,
        timeout=120,
    )
    contents: dict[str, bytes] = {}
    output = process.stdout
    position = 0
    while position < len(output):
        header_end = output.index(b"\n", position)
        sha, _type, size = output[position:header_end].decode("ascii").split()
        start = header_end + 1
        contents[sha] = output[start : start + int(size)]
        position = start + int(size) + 1
    return contents


def collect_lfs_pointers(git_dir: str, updates: list[tuple[str, str, str]]) -> list[dict[str, Any]]:
    """The LFS pointers the push introduces: blobs of at most 1024 bytes in the new history that parse exactly as
    a pointer. Each oid once, with the first path and ref it was found at."""
    found: dict[str, tuple[str, str]] = {}
    for _old_sha, new_sha, ref in updates:
        if new_sha == NULL_SHA:
            continue
        for sha, path in _new_objects(git_dir, new_sha):
            found.setdefault(sha, (path, ref))
    if not found:
        return []

    candidates = sorted(_small_blobs(git_dir, list(found)))
    if not candidates:
        return []

    pointers: dict[str, dict[str, Any]] = {}
    for sha, content in _blob_contents(git_dir, candidates).items():
        pointer = parse_pointer(content)
        if pointer is None:
            continue
        oid, size = pointer
        path, ref = found[sha]
        pointers.setdefault(oid, {"oid": oid, "size": size, "path": path, "refName": ref})
    return sorted(pointers.values(), key=lambda item: (item["path"], item["oid"]))


def validate_push(git_dir: str, updates: list[tuple[str, str, str]]) -> None:
    api_url = _read_api_url()
    if not api_url:
        raise RuntimeError(
            "Push validation unavailable: OPENGITBASE_API_URL is not configured."
        )

    commits = collect_commits(git_dir, updates)
    lfs_pointers = collect_lfs_pointers(git_dir, updates)
    if not commits and not lfs_pointers:
        return

    payload = {
        "physicalPath": git_dir,
        "commits": commits,
        "lfsPointers": lfs_pointers,
        "validatePushRulesOnly": True,
        "refUpdates": [],
    }
    request = urllib.request.Request(
        f"{api_url}/api/v1/internal/repositories/push-validation",
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            body = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(detail or f"push validation failed with HTTP {exc.code}") from exc

    if not body.get("allowed", False):
        reason = body.get("reason") or "Push rejected by repository policy."
        raise RuntimeError(reason)


def main() -> int:
    git_dir = subprocess.check_output(
        ["git", "rev-parse", "--git-dir"],
        text=True,
    ).strip()
    git_dir = str(Path(git_dir).resolve())
    updates: list[tuple[str, str, str]] = []
    for line in sys.stdin:
        parts = line.strip().split()
        if len(parts) != 3:
            continue
        updates.append((parts[0], parts[1], parts[2]))

    try:
        validate_push(git_dir, updates)
    except RuntimeError as exc:
        print(str(exc), file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
