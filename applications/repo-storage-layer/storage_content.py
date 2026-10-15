#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Git read operations for bare repositories (web content browsing)."""

from __future__ import annotations

import subprocess
import threading
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from urllib.parse import unquote

INLINE_MAX_BYTES = 1_048_576

README_CANDIDATES = (
    "README.md",
    "README.markdown",
    "README",
    "README.txt",
)

RAW_MAX_BYTES = INLINE_MAX_BYTES

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".webp"}

# Finding when an entry last changed walks history until every entry is
# accounted for. On very long histories that walk is cut off after this many
# seconds; entries not reached by then are listed without a date.
LAST_MODIFIED_BUDGET_SECONDS = 5.0

# Prefixes each commit header in the history walk. Git always quotes paths that
# contain control characters, so no path line can begin with this byte.
_COMMIT_MARKER = b"\x1e"

_C_ESCAPES = {
    ord("a"): 0x07,
    ord("b"): 0x08,
    ord("t"): 0x09,
    ord("n"): 0x0A,
    ord("v"): 0x0B,
    ord("f"): 0x0C,
    ord("r"): 0x0D,
    ord('"'): 0x22,
    ord("\\"): 0x5C,
}


class GitContentError(Exception):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


def require_rev(value: str, *, label: str = "ref") -> str:
    """Validate a client-supplied revision before it reaches git.

    Git passes a bare revision positionally, so a value beginning with '-'
    would be read as an option (e.g. ``--output=…``) rather than a ref. Git's
    own ref-format rules forbid a leading dash and SHAs are hex, so rejecting
    these rejects nothing legitimate while closing the argument-injection path.
    """
    cleaned = (value or "").strip()
    if not cleaned:
        raise GitContentError("invalid_ref", f"{label} is required.")
    if cleaned.startswith("-"):
        raise GitContentError("invalid_ref", f"Invalid {label}.")
    return cleaned


def _run_git(git_dir: str, *args: str) -> str:
    return _run_git_bytes(git_dir, *args).decode("utf-8", errors="replace")


def _run_git_bytes(git_dir: str, *args: str) -> bytes:
    result = subprocess.run(
        ["git", "--git-dir", git_dir, *args],
        check=False,
        capture_output=True,
    )
    if result.returncode != 0:
        stderr = (result.stderr or result.stdout or b"").decode("utf-8", errors="replace").strip()
        if "bad object" in stderr or "Not a valid object name" in stderr:
            raise GitContentError("invalid_ref", stderr or "Invalid ref.")
        if "exists on disk, but not in" in stderr or "did not match any file" in stderr:
            raise GitContentError("not_found", stderr or "Path not found.")
        raise GitContentError("git_error", stderr or "Git command failed.")
    return result.stdout


def get_disk_usage(git_dir: str) -> int:
    attempts = (["du", "-sb", git_dir], ["du", "-sk", git_dir])
    for args in attempts:
        result = subprocess.run(
            args,
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            continue
        value = int(result.stdout.split()[0])
        return value if args[1] == "-sb" else value * 1024

    raise GitContentError(
        "disk_usage_error",
        "Failed to read repository disk usage.",
    )


def _normalize_repo_path(path: str) -> str:
    cleaned = path.strip().strip("/")
    if cleaned in ("", "."):
        return ""
    segments = [segment for segment in cleaned.split("/") if segment and segment != "."]
    if any(segment == ".." for segment in segments):
        raise GitContentError("invalid_path", "Path must not contain '..'.")
    return "/".join(segments)


def _entry_name(mode: str, name: str) -> dict[str, Any]:
    entry_type = "tree" if mode.startswith("04") else "blob"
    return {"name": name, "path": name, "type": entry_type}


def list_branches(git_dir: str) -> list[dict[str, str]]:
    output = _run_git(git_dir, "for-each-ref", "--format=%(refname:short)\t%(objectname)", "refs/heads/")
    branches: list[dict[str, str]] = []
    for line in output.splitlines():
        if not line.strip():
            continue
        name, sha = line.split("\t", 1)
        branches.append({"name": name, "commitSha": sha})
    branches.sort(key=lambda item: item["name"].lower())
    return branches


def list_tags(git_dir: str) -> list[dict[str, str]]:
    output = _run_git(git_dir, "for-each-ref", "--format=%(refname:short)\t%(objectname)", "refs/tags/")
    tags: list[dict[str, str]] = []
    for line in output.splitlines():
        if not line.strip():
            continue
        name, sha = line.split("\t", 1)
        tags.append({"name": name, "commitSha": sha})
    tags.sort(key=lambda item: item["name"].lower())
    return tags


def resolve_ref(git_dir: str, ref: str) -> str:
    ref = require_rev(ref)
    return _run_git(git_dir, "rev-parse", "--verify", ref).strip()


def count_commits_ahead(git_dir: str, base_ref: str, head_ref: str) -> int:
    base_ref = require_rev(base_ref, label="baseRef")
    head_ref = require_rev(head_ref, label="headRef")
    output = _run_git(git_dir, "rev-list", "--count", f"{base_ref}..{head_ref}")
    return int(output.strip())


def list_tree(
    git_dir: str,
    ref: str,
    path: str = "",
    *,
    last_modified_budget_seconds: float = LAST_MODIFIED_BUDGET_SECONDS,
) -> dict[str, Any]:
    tree = _list_tree_entries(git_dir, ref, path)
    last_modified = _last_modified_by_name(
        git_dir,
        tree["ref"],
        tree["path"],
        {entry["name"] for entry in tree["entries"]},
        budget_seconds=last_modified_budget_seconds,
    )
    for entry in tree["entries"]:
        entry["lastModifiedAt"] = last_modified.get(entry["name"])
    return tree


def _list_tree_entries(git_dir: str, ref: str, path: str = "") -> dict[str, Any]:
    ref = require_rev(ref)
    normalized_path = _normalize_repo_path(path)
    tree_ref = f"{ref}:{normalized_path}" if normalized_path else ref
    # -z keeps names verbatim; without it git quotes non-ASCII names.
    output = _run_git_bytes(git_dir, "ls-tree", "-z", tree_ref)
    entries: list[dict[str, Any]] = []
    for record in output.split(b"\0"):
        if not record:
            continue
        meta, raw_name = record.split(b"\t", 1)
        mode, obj_type, sha = meta.decode("ascii").split()
        name = raw_name.decode("utf-8", errors="replace")
        entry_path = f"{normalized_path}/{name}" if normalized_path else name
        size: int | None = None
        if obj_type == "blob":
            size_output = _run_git(git_dir, "cat-file", "-s", sha)
            size = int(size_output.strip())
        entries.append(
            {
                "name": name,
                "path": entry_path,
                "type": "tree" if obj_type == "tree" else "blob",
                "size": size,
            }
        )
    return {"ref": ref, "path": normalized_path, "entries": entries}


def _last_modified_by_name(
    git_dir: str,
    ref: str,
    directory: str,
    names: set[str],
    *,
    budget_seconds: float,
) -> dict[str, str]:
    """Map each direct child of ``directory`` to its newest commit date (UTC ISO 8601).

    Walks ``git log`` once, newest first, and stops as soon as every name has
    been seen, so a directory costs one history walk rather than one per entry.
    """
    pending = set(names)
    found: dict[str, str] = {}
    if not pending:
        return found

    prefix = f"{directory}/" if directory else ""
    args = [
        "git",
        "--git-dir",
        git_dir,
        "--literal-pathspecs",
        "log",
        f"--format={_COMMIT_MARKER.decode('ascii')}%ct",
        "--name-only",
        "--no-renames",
        # Lists a merge's own changes (conflict resolutions), as `git log -1 -- <path>` would.
        "--diff-merges=dense-combined",
        ref,
    ]
    if directory:
        args += ["--", directory]

    deadline = time.monotonic() + budget_seconds
    process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    watchdog = threading.Timer(budget_seconds, process.kill)
    watchdog.start()
    try:
        assert process.stdout is not None
        committed_at: str | None = None
        for line in process.stdout:
            if not pending or time.monotonic() >= deadline:
                break
            line = line.rstrip(b"\n")
            if line.startswith(_COMMIT_MARKER):
                committed_at = _utc_iso(int(line[len(_COMMIT_MARKER):]))
                continue
            if not line or committed_at is None:
                continue
            changed_path = _unquote_git_path(line).decode("utf-8", errors="replace")
            if not changed_path.startswith(prefix):
                continue
            name = changed_path[len(prefix):].split("/", 1)[0]
            if name in pending:
                pending.discard(name)
                found[name] = committed_at
    finally:
        watchdog.cancel()
        if process.poll() is None:
            process.kill()
        process.stdout.close()
        process.wait()
    return found


def _utc_iso(unix_seconds: int) -> str:
    return datetime.fromtimestamp(unix_seconds, tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def _unquote_git_path(raw: bytes) -> bytes:
    """Undo the C-style quoting git applies to unusual paths in line output."""
    if len(raw) < 2 or not (raw.startswith(b'"') and raw.endswith(b'"')):
        return raw
    body = raw[1:-1]
    unquoted = bytearray()
    index = 0
    while index < len(body):
        byte = body[index]
        if byte != 0x5C or index + 1 >= len(body):
            unquoted.append(byte)
            index += 1
            continue
        escaped = body[index + 1]
        if 0x30 <= escaped <= 0x37:
            unquoted.append(int(body[index + 1:index + 4], 8))
            index += 4
        else:
            unquoted.append(_C_ESCAPES.get(escaped, escaped))
            index += 2
    return bytes(unquoted)


def _preview_kind(path: str, is_binary: bool) -> str:
    suffix = Path(path).suffix.lower()
    if suffix == ".svg":
        return "svg"
    if suffix in IMAGE_EXTENSIONS:
        return "image"
    if is_binary:
        return "binary"
    return "text"


def get_blob(git_dir: str, ref: str, path: str) -> dict[str, Any]:
    ref = require_rev(ref)
    normalized_path = _normalize_repo_path(unquote(path))
    if not normalized_path:
        raise GitContentError("invalid_path", "Blob path is required.")

    object_ref = f"{ref}:{normalized_path}"
    object_type = _run_git(git_dir, "cat-file", "-t", object_ref).strip()
    if object_type != "blob":
        raise GitContentError("not_found", "Path is not a file.")

    size = int(_run_git(git_dir, "cat-file", "-s", object_ref).strip())
    raw = subprocess.run(
        ["git", "--git-dir", git_dir, "cat-file", "-p", object_ref],
        check=True,
        capture_output=True,
        timeout=60,
    ).stdout
    is_binary = b"\0" in raw[:8192]

    preview_kind = _preview_kind(normalized_path, is_binary)
    payload: dict[str, Any] = {
        "ref": ref,
        "path": normalized_path,
        "size": size,
        "isBinary": is_binary,
        "isTooLarge": size > INLINE_MAX_BYTES,
        "previewKind": preview_kind,
    }

    if size <= INLINE_MAX_BYTES and not is_binary and preview_kind == "text":
        payload["textContent"] = raw.decode("utf-8", errors="replace")
        payload["encoding"] = "utf-8"
    elif size <= INLINE_MAX_BYTES and preview_kind == "image":
        payload["contentBase64"] = __import__("base64").b64encode(raw).decode("ascii")

    return payload


def get_raw_bytes(git_dir: str, ref: str, path: str, *, max_bytes: int = RAW_MAX_BYTES) -> tuple[bytes, str]:
    ref = require_rev(ref)
    normalized_path = _normalize_repo_path(unquote(path))
    if not normalized_path:
        raise GitContentError("invalid_path", "Blob path is required.")
    object_ref = f"{ref}:{normalized_path}"
    object_type = _run_git(git_dir, "cat-file", "-t", object_ref).strip()
    if object_type != "blob":
        raise GitContentError("not_found", "Path is not a file.")
    size = int(_run_git(git_dir, "cat-file", "-s", object_ref).strip())
    if size > max_bytes:
        raise GitContentError(
            "too_large",
            f"Blob exceeds maximum download size of {max_bytes} bytes.",
        )
    raw = subprocess.run(
        ["git", "--git-dir", git_dir, "cat-file", "-p", object_ref],
        check=True,
        capture_output=True,
    ).stdout
    return raw, normalized_path


def resolve_readme(git_dir: str, ref: str) -> dict[str, Any] | None:
    tree = _list_tree_entries(git_dir, ref, "")
    names = {entry["name"].lower(): entry for entry in tree["entries"] if entry["type"] == "blob"}
    for candidate in README_CANDIDATES:
        entry = names.get(candidate.lower())
        if entry is None:
            continue
        blob = get_blob(git_dir, ref, entry["path"])
        return {
            "ref": ref,
            "fileName": entry["name"],
            "markdownSource": blob.get("textContent", ""),
        }
    return None


def is_empty_repository(git_dir: str) -> bool:
    try:
        branches = list_branches(git_dir)
        return len(branches) == 0
    except GitContentError:
        return True
