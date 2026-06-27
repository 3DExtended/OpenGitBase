#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Git compare and merge operations for bare repositories."""

from __future__ import annotations

import re
import shutil
import subprocess
import tempfile
from contextlib import contextmanager
from pathlib import Path
from typing import Any, Iterator

from storage_content import GitContentError, _run_git, resolve_ref

MERGE_STRATEGIES = frozenset({"merge_commit", "squash", "fast_forward"})
HUNK_HEADER = re.compile(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def get_diff(git_dir: str, base_sha: str, head_sha: str) -> dict[str, Any]:
    _verify_object(git_dir, base_sha)
    _verify_object(git_dir, head_sha)

    name_status_output = _run_git(git_dir, "diff", "--name-status", base_sha, head_sha)
    unified_output = _run_git(git_dir, "diff", "--unified=3", "--no-color", base_sha, head_sha)
    numstat_output = _run_git(git_dir, "diff", "--numstat", base_sha, head_sha)

    status_by_path = _parse_name_status(name_status_output)
    binary_paths = _parse_binary_paths(numstat_output)
    files = _parse_unified_diff(unified_output, status_by_path, binary_paths)

    return {
        "baseSha": base_sha,
        "headSha": head_sha,
        "files": files,
    }


def check_mergeability(git_dir: str, target_sha: str, source_sha: str) -> dict[str, Any]:
    try:
        _verify_object(git_dir, target_sha)
        _verify_object(git_dir, source_sha)
    except GitContentError:
        return {"status": "unknown"}

    if target_sha == source_sha:
        return {
            "status": "mergeable",
            "canFastForward": True,
            "alreadyUpToDate": True,
        }

    can_fast_forward = _is_ancestor(git_dir, target_sha, source_sha)
    already_up_to_date = _is_ancestor(git_dir, source_sha, target_sha)
    if already_up_to_date:
        return {
            "status": "mergeable",
            "canFastForward": False,
            "alreadyUpToDate": True,
        }

    merge_base = _run_git(git_dir, "merge-base", target_sha, source_sha).strip()
    merge_tree_output = _run_git(git_dir, "merge-tree", merge_base, target_sha, source_sha)
    if _merge_tree_has_conflicts(merge_tree_output):
        return {
            "status": "conflicts",
            "canFastForward": False,
        }

    return {
        "status": "mergeable",
        "canFastForward": can_fast_forward,
        "alreadyUpToDate": False,
    }


def execute_merge(
    git_dir: str,
    target_ref: str,
    source_ref: str,
    strategy: str,
    commit_message: str | None = None,
) -> dict[str, Any]:
    normalized_strategy = strategy.strip().lower().replace("-", "_")
    if normalized_strategy not in MERGE_STRATEGIES:
        raise GitContentError("invalid_strategy", f"Unsupported merge strategy: {strategy}")

    target_ref_name = _normalize_head_ref(git_dir, target_ref)
    source_ref_name = _normalize_head_ref(git_dir, source_ref)
    target_sha = resolve_ref(git_dir, target_ref_name)
    source_sha = resolve_ref(git_dir, source_ref_name)

    mergeability = check_mergeability(git_dir, target_sha, source_sha)
    if mergeability["status"] == "unknown":
        raise GitContentError("merge_error", "Unable to determine mergeability.")
    if mergeability["status"] == "conflicts":
        raise GitContentError("conflicts", "Merge has conflicts.")

    if normalized_strategy == "fast_forward":
        if not mergeability.get("canFastForward"):
            raise GitContentError(
                "not_fast_forwardable",
                "Fast-forward merge is not possible.",
            )
        new_sha = source_sha
    elif normalized_strategy == "squash":
        message = commit_message or f"Squashed commit of '{source_ref_name}'"
        new_sha = _squash_merge(git_dir, target_sha, source_sha, message)
    else:
        message = commit_message or f"Merge '{source_ref_name}' into '{target_ref_name}'"
        new_sha = _merge_commit(git_dir, target_sha, source_sha, message)

    old_sha = resolve_ref(git_dir, target_ref_name)
    _update_ref_with_hook(git_dir, target_ref_name, new_sha, old_sha)
    return {
        "commitSha": new_sha,
        "strategy": normalized_strategy,
        "targetRef": target_ref_name,
    }


def delete_branch_ref(git_dir: str, ref_name: str) -> None:
    branch_ref = _normalize_head_ref(git_dir, ref_name)
    _run_git(git_dir, "update-ref", "-d", branch_ref)


def _verify_object(git_dir: str, sha: str) -> None:
    _run_git(git_dir, "cat-file", "-e", sha)


def _parse_name_status(output: str) -> dict[str, dict[str, str | None]]:
    statuses: dict[str, dict[str, str | None]] = {}
    for line in output.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        code = parts[0]
        if code.startswith("R") and len(parts) >= 3:
            statuses[parts[2]] = {
                "status": "renamed",
                "oldPath": parts[1],
                "newPath": parts[2],
            }
            continue
        if len(parts) < 2:
            continue
        path = parts[1]
        if code == "A":
            statuses[path] = {"status": "added", "oldPath": None, "newPath": path}
        elif code == "D":
            statuses[path] = {"status": "deleted", "oldPath": path, "newPath": None}
        elif code == "M":
            statuses[path] = {"status": "modified", "oldPath": path, "newPath": path}
        else:
            statuses[path] = {"status": "modified", "oldPath": path, "newPath": path}
    return statuses


def _parse_binary_paths(output: str) -> set[str]:
    binary_paths: set[str] = set()
    for line in output.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t")
        if len(parts) >= 3 and parts[0] == "-" and parts[1] == "-":
            binary_paths.add(parts[2])
    return binary_paths


def _parse_unified_diff(
    output: str,
    status_by_path: dict[str, dict[str, str | None]],
    binary_paths: set[str],
) -> list[dict[str, Any]]:
    if not output.strip():
        return []

    files: list[dict[str, Any]] = []
    current: dict[str, Any] | None = None
    current_hunk: dict[str, Any] | None = None
    old_line = 0
    new_line = 0

    for raw_line in output.splitlines():
        if raw_line.startswith("diff --git "):
            if current is not None:
                if current_hunk is not None:
                    current["hunks"].append(current_hunk)
                files.append(current)
            old_path, new_path = _parse_diff_git_header(raw_line)
            lookup_path = new_path or old_path or ""
            status_info = status_by_path.get(
                lookup_path,
                {
                    "status": _infer_status(old_path, new_path),
                    "oldPath": old_path,
                    "newPath": new_path,
                },
            )
            current = {
                "oldPath": status_info.get("oldPath", old_path),
                "newPath": status_info.get("newPath", new_path),
                "status": status_info.get("status", _infer_status(old_path, new_path)),
                "isBinary": lookup_path in binary_paths,
                "hunks": [],
            }
            current_hunk = None
            continue

        if current is None:
            continue

        if raw_line.startswith("Binary files "):
            current["isBinary"] = True
            continue

        match = HUNK_HEADER.match(raw_line)
        if match:
            if current_hunk is not None:
                current["hunks"].append(current_hunk)
            old_start = int(match.group(1))
            old_count = int(match.group(2) or "1")
            new_start = int(match.group(3))
            new_count = int(match.group(4) or "1")
            current_hunk = {
                "oldStart": old_start,
                "oldLines": old_count,
                "newStart": new_start,
                "newLines": new_count,
                "lines": [],
            }
            old_line = old_start
            new_line = new_start
            continue

        if current_hunk is None:
            continue

        if not raw_line:
            line_type = "context"
            content = ""
            current_old = old_line if old_line > 0 else None
            current_new = new_line if new_line > 0 else None
        elif raw_line.startswith("+"):
            line_type = "add"
            content = raw_line[1:]
            current_old = None
            current_new = new_line
            new_line += 1
        elif raw_line.startswith("-"):
            line_type = "delete"
            content = raw_line[1:]
            current_old = old_line
            current_new = None
            old_line += 1
        elif raw_line.startswith(" "):
            line_type = "context"
            content = raw_line[1:]
            current_old = old_line
            current_new = new_line
            old_line += 1
            new_line += 1
        elif raw_line.startswith("\\"):
            continue
        else:
            continue

        current_hunk["lines"].append(
            {
                "type": line_type,
                "content": content,
                "oldLineNumber": current_old,
                "newLineNumber": current_new,
            }
        )

    if current is not None:
        if current_hunk is not None:
            current["hunks"].append(current_hunk)
        files.append(current)

    return files


def _parse_diff_git_header(line: str) -> tuple[str | None, str | None]:
    match = re.match(r"^diff --git a/(.+?) b/(.+)$", line)
    if match is None:
        return None, None
    old_path = None if match.group(1) == "/dev/null" else match.group(1)
    new_path = None if match.group(2) == "/dev/null" else match.group(2)
    return old_path, new_path


def _infer_status(old_path: str | None, new_path: str | None) -> str:
    if old_path is None and new_path is not None:
        return "added"
    if old_path is not None and new_path is None:
        return "deleted"
    if old_path != new_path:
        return "renamed"
    return "modified"


def _is_ancestor(git_dir: str, ancestor_sha: str, descendant_sha: str) -> bool:
    result = subprocess.run(
        ["git", "--git-dir", git_dir, "merge-base", "--is-ancestor", ancestor_sha, descendant_sha],
        check=False,
        capture_output=True,
        text=True,
    )
    return result.returncode == 0


def is_ancestor_commit(repo_path: str, ancestor_sha: str, descendant_sha: str) -> bool:
    git_dir = _git_dir(repo_path)
    return _is_ancestor(git_dir, ancestor_sha, descendant_sha)


def _merge_tree_has_conflicts(output: str) -> bool:
    lowered = output.lower()
    return "changed in both" in lowered or "conflict" in lowered


def _normalize_head_ref(git_dir: str, ref: str) -> str:
    cleaned = ref.strip()
    if cleaned.startswith("refs/"):
        resolve_ref(git_dir, cleaned)
        return cleaned
    head_ref = f"refs/heads/{cleaned}"
    resolve_ref(git_dir, head_ref)
    return head_ref


def _squash_merge(
    git_dir: str,
    target_sha: str,
    source_sha: str,
    commit_message: str,
) -> str:
    with _temporary_worktree(git_dir, target_sha) as workdir:
        result = subprocess.run(
            ["git", "-C", workdir, "merge", "--squash", source_sha],
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            stderr = (result.stderr or result.stdout or "").strip()
            if "conflict" in stderr.lower():
                raise GitContentError("conflicts", "Merge has conflicts.")
            raise GitContentError("merge_error", stderr or "Squash merge failed.")
        commit_result = subprocess.run(
            ["git", "-C", workdir, "commit", "-m", commit_message],
            check=False,
            capture_output=True,
            text=True,
        )
        if commit_result.returncode != 0:
            stderr = (commit_result.stderr or commit_result.stdout or "").strip()
            raise GitContentError("merge_error", stderr or "Squash commit failed.")
        rev_parse = subprocess.run(
            ["git", "-C", workdir, "rev-parse", "HEAD"],
            check=False,
            capture_output=True,
            text=True,
        )
        if rev_parse.returncode != 0:
            stderr = (rev_parse.stderr or rev_parse.stdout or "").strip()
            raise GitContentError("merge_error", stderr or "Failed to resolve squash commit.")
        return rev_parse.stdout.strip()


def _merge_commit(
    git_dir: str,
    target_sha: str,
    source_sha: str,
    commit_message: str,
) -> str:
    with _temporary_worktree(git_dir, target_sha) as workdir:
        result = subprocess.run(
            ["git", "-C", workdir, "merge", "--no-ff", source_sha, "-m", commit_message],
            check=False,
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            stderr = (result.stderr or result.stdout or "").strip()
            if "conflict" in stderr.lower():
                raise GitContentError("conflicts", "Merge has conflicts.")
            raise GitContentError("merge_error", stderr or "Merge commit failed.")
        rev_parse = subprocess.run(
            ["git", "-C", workdir, "rev-parse", "HEAD"],
            check=False,
            capture_output=True,
            text=True,
        )
        if rev_parse.returncode != 0:
            stderr = (rev_parse.stderr or rev_parse.stdout or "").strip()
            raise GitContentError("merge_error", stderr or "Failed to resolve merge commit.")
        return rev_parse.stdout.strip()


def _update_ref_with_hook(
    git_dir: str,
    ref_name: str,
    new_sha: str,
    old_sha: str,
) -> None:
    _run_git(git_dir, "update-ref", ref_name, new_sha, old_sha)
    hook_path = Path(git_dir) / "hooks" / "post-receive"
    if not hook_path.is_file():
        return
    hook_input = f"{old_sha} {new_sha} {ref_name}\n"
    hook_result = subprocess.run(
        [str(hook_path)],
        input=hook_input,
        text=True,
        capture_output=True,
        check=False,
    )
    if hook_result.returncode != 0:
        _run_git(git_dir, "update-ref", ref_name, old_sha, new_sha)
        stderr = (hook_result.stderr or hook_result.stdout or "").strip()
        raise GitContentError("merge_error", stderr or "Post-receive hook failed.")


@contextmanager
def _temporary_worktree(git_dir: str, start_sha: str) -> Iterator[str]:
    subprocess.run(
        ["git", "--git-dir", git_dir, "worktree", "prune"],
        check=False,
        capture_output=True,
        text=True,
    )
    tmp = tempfile.mkdtemp(prefix="ogb-merge-")
    try:
        add_result = subprocess.run(
            ["git", "--git-dir", git_dir, "worktree", "add", "--detach", tmp, start_sha],
            check=False,
            capture_output=True,
            text=True,
        )
        if add_result.returncode != 0:
            stderr = (add_result.stderr or add_result.stdout or "").strip()
            raise GitContentError("merge_error", stderr or "Failed to create worktree.")
        subprocess.run(["git", "-C", tmp, "reset", "--hard"], check=False, capture_output=True, text=True)
        subprocess.run(["git", "-C", tmp, "clean", "-fdx"], check=False, capture_output=True, text=True)
        yield tmp
    finally:
        subprocess.run(
            ["git", "--git-dir", git_dir, "worktree", "remove", "--force", tmp],
            check=False,
            capture_output=True,
            text=True,
        )
        shutil.rmtree(tmp, ignore_errors=True)
        subprocess.run(
            ["git", "--git-dir", git_dir, "worktree", "prune"],
            check=False,
            capture_output=True,
            text=True,
        )
