#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Single-commit read operations for bare repositories."""

from __future__ import annotations

import re
from typing import Any

from storage_content import GitContentError, _run_git
from storage_merge import get_diff

AMBIGUOUS_SHA = re.compile(r"ambiguous", re.IGNORECASE)


def resolve_commit_sha(git_dir: str, sha: str) -> str:
    cleaned = sha.strip()
    if not cleaned:
        raise GitContentError("not_found", "Commit not found.")

    try:
        return _run_git(git_dir, "rev-parse", "--verify", f"{cleaned}^{{commit}}").strip()
    except GitContentError as exc:
        if AMBIGUOUS_SHA.search(exc.message):
            raise GitContentError("ambiguous_sha", exc.message) from exc
        if exc.code in {"invalid_ref", "git_error", "not_found"}:
            raise GitContentError("not_found", "Commit not found.") from exc
        raise


def get_commit(git_dir: str, sha: str) -> dict[str, Any]:
    full_sha = resolve_commit_sha(git_dir, sha)
    metadata = _read_commit_metadata(git_dir, full_sha)
    parents: list[dict[str, str]] = metadata["parents"]

    if not parents:
        files = _list_root_tree_files(git_dir, full_sha)
        return {
            "sha": full_sha,
            "shortSha": full_sha[:8],
            "message": metadata["message"],
            "authorName": metadata["authorName"],
            "authoredAt": metadata["authoredAt"],
            "parents": parents,
            "stats": {
                "filesChanged": len(files),
                "insertions": 0,
                "deletions": 0,
            },
            "kind": "root",
            "files": files,
        }

    parent_sha = parents[0]["sha"]
    diff = get_diff(git_dir, parent_sha, full_sha)
    stats = _stats_from_diff_files(diff["files"])
    return {
        "sha": full_sha,
        "shortSha": full_sha[:8],
        "message": metadata["message"],
        "authorName": metadata["authorName"],
        "authoredAt": metadata["authoredAt"],
        "parents": parents,
        "stats": stats,
        "kind": "diff",
        "files": diff["files"],
    }


def _read_commit_metadata(git_dir: str, full_sha: str) -> dict[str, Any]:
    output = _run_git(
        git_dir,
        "log",
        "-1",
        "--format=%P%x1f%an%x1f%aI%x1f%B",
        full_sha,
    )
    parts = output.split("\x1f", 3)
    if len(parts) != 4:
        raise GitContentError("git_error", "Failed to read commit metadata.")

    parent_part, author_name, authored_at, message = parts
    parent_shas = [item.strip() for item in parent_part.split() if item.strip()]
    parents = [{"sha": parent, "shortSha": parent[:8]} for parent in parent_shas]
    return {
        "message": message.rstrip("\n"),
        "authorName": author_name,
        "authoredAt": authored_at,
        "parents": parents,
    }


def _list_root_tree_files(git_dir: str, full_sha: str) -> list[dict[str, str]]:
    output = _run_git(git_dir, "ls-tree", "-r", "--name-only", full_sha)
    files: list[dict[str, str]] = []
    for line in output.splitlines():
        path = line.strip()
        if not path:
            continue
        files.append(
            {
                "path": path,
                "changeType": "added",
            }
        )
    files.sort(key=lambda item: item["path"].lower())
    return files


def _stats_from_diff_files(files: list[dict[str, Any]]) -> dict[str, int]:
    insertions = 0
    deletions = 0
    for file_entry in files:
        for hunk in file_entry.get("hunks", []):
            for line in hunk.get("lines", []):
                line_type = line.get("type")
                if line_type == "add":
                    insertions += 1
                elif line_type == "delete":
                    deletions += 1
    return {
        "filesChanged": len(files),
        "insertions": insertions,
        "deletions": deletions,
    }
