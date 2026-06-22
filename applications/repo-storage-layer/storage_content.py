#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Git read operations for bare repositories (web content browsing)."""

from __future__ import annotations

import subprocess
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

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".gif", ".webp"}


class GitContentError(Exception):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


def _run_git(git_dir: str, *args: str) -> str:
    result = subprocess.run(
        ["git", "--git-dir", git_dir, *args],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        stderr = (result.stderr or result.stdout or "").strip()
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
    return _run_git(git_dir, "rev-parse", "--verify", ref).strip()


def list_tree(git_dir: str, ref: str, path: str = "") -> dict[str, Any]:
    normalized_path = _normalize_repo_path(path)
    tree_ref = f"{ref}:{normalized_path}" if normalized_path else ref
    output = _run_git(git_dir, "ls-tree", tree_ref)
    entries: list[dict[str, Any]] = []
    for line in output.splitlines():
        if not line.strip():
            continue
        mode, obj_type, sha, name = line.split(maxsplit=3)
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


def get_raw_bytes(git_dir: str, ref: str, path: str) -> tuple[bytes, str]:
    normalized_path = _normalize_repo_path(unquote(path))
    if not normalized_path:
        raise GitContentError("invalid_path", "Blob path is required.")
    object_ref = f"{ref}:{normalized_path}"
    object_type = _run_git(git_dir, "cat-file", "-t", object_ref).strip()
    if object_type != "blob":
        raise GitContentError("not_found", "Path is not a file.")
    raw = subprocess.run(
        ["git", "--git-dir", git_dir, "cat-file", "-p", object_ref],
        check=True,
        capture_output=True,
    ).stdout
    return raw, normalized_path


def resolve_readme(git_dir: str, ref: str) -> dict[str, Any] | None:
    tree = list_tree(git_dir, ref, "")
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
