#!/usr/bin/env python3
"""Tests for single-commit read helpers."""

from __future__ import annotations

import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from storage_commit import get_commit, resolve_commit_sha
from storage_content import GitContentError


class StorageCommitTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.repo_path = os.path.join(self.temp_dir, "sample.git")
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", self.repo_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self.work = Path(self.temp_dir) / "work"
        self.work.mkdir()
        subprocess.run(
            ["git", "init", "-b", "main"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "config", "user.email", "test@example.com"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "config", "user.name", "Test User"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        (self.work / "README.md").write_text("# Base\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(["git", "commit", "-m", "init"], cwd=self.work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(
            ["git", "push", self.repo_path, "main"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self.main_sha = (
            subprocess.check_output(["git", "--git-dir", self.repo_path, "rev-parse", "main"], text=True).strip()
        )

    def tearDown(self) -> None:
        import shutil

        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def _create_branch(self, branch: str, file_name: str, content: str, commit_message: str) -> str:
        subprocess.run(
            ["git", "checkout", "-B", branch, "main"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        (self.work / file_name).write_text(content, encoding="utf-8")
        subprocess.run(["git", "add", file_name], cwd=self.work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(
            ["git", "commit", "-m", commit_message],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "push", "-f", self.repo_path, f"{branch}:{branch}"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        return subprocess.check_output(
            ["git", "--git-dir", self.repo_path, "rev-parse", branch],
            text=True,
        ).strip()

    def test_resolve_commit_sha_returns_full_hash(self) -> None:
        resolved = resolve_commit_sha(self.repo_path, self.main_sha)
        self.assertEqual(resolved, self.main_sha)

    def test_resolve_commit_sha_accepts_unique_prefix(self) -> None:
        resolved = resolve_commit_sha(self.repo_path, self.main_sha[:8])
        self.assertEqual(resolved, self.main_sha)

    def test_resolve_commit_sha_unknown_raises_not_found(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            resolve_commit_sha(self.repo_path, "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef")
        self.assertEqual(ctx.exception.code, "not_found")

    def test_get_commit_root_returns_tree_listing(self) -> None:
        payload = get_commit(self.repo_path, self.main_sha)
        self.assertEqual(payload["sha"], self.main_sha)
        self.assertEqual(payload["kind"], "root")
        self.assertEqual(payload["message"], "init")
        self.assertEqual(payload["authorName"], "Test User")
        self.assertEqual(payload["parents"], [])
        self.assertEqual(payload["stats"]["filesChanged"], 1)
        self.assertEqual(payload["files"][0]["path"], "README.md")
        self.assertEqual(payload["files"][0]["changeType"], "added")

    def test_get_commit_linear_returns_first_parent_diff(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        payload = get_commit(self.repo_path, feature_sha)
        self.assertEqual(payload["kind"], "diff")
        self.assertEqual(payload["message"], "add feature")
        self.assertEqual(len(payload["parents"]), 1)
        self.assertEqual(payload["parents"][0]["sha"], self.main_sha)
        self.assertEqual(payload["stats"]["filesChanged"], 1)
        self.assertGreater(payload["stats"]["insertions"], 0)
        self.assertEqual(len(payload["files"]), 1)
        self.assertEqual(payload["files"][0]["newPath"], "feature.txt")

    def test_resolve_commit_sha_ambiguous_prefix_raises(self) -> None:
        with patch(
            "storage_commit._run_git",
            side_effect=GitContentError("git_error", "ambiguous argument 'abc': unknown revision or path"),
        ):
            with self.assertRaises(GitContentError) as ctx:
                resolve_commit_sha(self.repo_path, "abc")
        self.assertEqual(ctx.exception.code, "ambiguous_sha")

    def test_get_commit_merge_returns_first_parent_diff(self) -> None:
        from storage_merge import execute_merge

        self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        execute_merge(self.repo_path, "main", "feature", "merge_commit")
        main_after_feature = subprocess.check_output(
            ["git", "--git-dir", self.repo_path, "rev-parse", "main"],
            text=True,
        ).strip()

        self._create_branch("other", "other.txt", "other\n", "add other")
        merge_result = execute_merge(self.repo_path, "main", "other", "merge_commit")
        merge_sha = merge_result["commitSha"]

        payload = get_commit(self.repo_path, merge_sha)
        self.assertEqual(payload["kind"], "diff")
        self.assertEqual(len(payload["parents"]), 2)
        self.assertEqual(payload["parents"][0]["sha"], main_after_feature)
        changed_paths = {
            file_entry.get("newPath") or file_entry.get("path")
            for file_entry in payload["files"]
        }
        self.assertIn("other.txt", changed_paths)
        self.assertNotIn("feature.txt", changed_paths)

    def test_get_commit_empty_tree_change_returns_empty_diff_files(self) -> None:
        subprocess.run(
            ["git", "checkout", "main"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "commit", "--allow-empty", "-m", "empty metadata"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "push", self.repo_path, "main"],
            cwd=self.work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        empty_sha = subprocess.check_output(
            ["git", "--git-dir", self.repo_path, "rev-parse", "main"],
            text=True,
        ).strip()
        payload = get_commit(self.repo_path, empty_sha)
        self.assertEqual(payload["kind"], "diff")
        self.assertEqual(payload["message"], "empty metadata")
        self.assertEqual(payload["stats"]["filesChanged"], 0)
        self.assertEqual(payload["files"], [])


if __name__ == "__main__":
    unittest.main()
