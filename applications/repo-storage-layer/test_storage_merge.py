#!/usr/bin/env python3
"""Tests for storage merge and diff helpers."""

from __future__ import annotations

import os
import subprocess
import tempfile
import unittest
from pathlib import Path

from storage_merge import check_mergeability, execute_merge, get_diff, is_ancestor_commit, list_commits_since_merge_base


class StorageMergeTests(unittest.TestCase):
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

    def test_get_diff_returns_unified_hunks(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        diff = get_diff(self.repo_path, self.main_sha, feature_sha)
        self.assertEqual(diff["baseSha"], self.main_sha)
        self.assertEqual(diff["headSha"], feature_sha)
        self.assertEqual(len(diff["files"]), 1)
        file_entry = diff["files"][0]
        self.assertEqual(file_entry["status"], "added")
        self.assertEqual(file_entry["newPath"], "feature.txt")
        self.assertGreater(len(file_entry["hunks"]), 0)
        added_lines = [
            line for hunk in file_entry["hunks"] for line in hunk["lines"] if line["type"] == "add"
        ]
        self.assertTrue(any("feature" in line["content"] for line in added_lines))

    def test_list_commits_since_merge_base_returns_feature_commits(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        result = list_commits_since_merge_base(self.repo_path, self.main_sha, feature_sha)
        self.assertEqual(len(result["commits"]), 1)
        commit = result["commits"][0]
        self.assertEqual(commit["sha"], feature_sha)
        self.assertEqual(commit["message"], "add feature")
        self.assertEqual(commit["shortSha"], feature_sha[:8])
        self.assertEqual(commit["authorName"], "Test User")

    def test_mergeability_linear_pair_is_mergeable(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        result = check_mergeability(self.repo_path, self.main_sha, feature_sha)
        self.assertEqual(result["status"], "mergeable")
        self.assertTrue(result["canFastForward"])

    def test_is_ancestor_commit_detects_linear_history(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        self.assertTrue(is_ancestor_commit(self.repo_path, self.main_sha, feature_sha))
        self.assertFalse(is_ancestor_commit(self.repo_path, feature_sha, self.main_sha))

    def test_mergeability_conflicting_pair_reports_conflicts(self) -> None:
        feature_sha = self._create_branch("feature", "README.md", "# Feature\n", "feature change")
        other_sha = self._create_branch("other", "README.md", "# Other\n", "other change")
        self.assertNotEqual(feature_sha, other_sha)
        result = check_mergeability(self.repo_path, feature_sha, other_sha)
        self.assertEqual(result["status"], "conflicts")

    def test_execute_merge_commit_updates_target_ref(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        result = execute_merge(self.repo_path, "main", "feature", "merge_commit")
        self.assertEqual(result["strategy"], "merge_commit")
        target_sha = (
            subprocess.check_output(["git", "--git-dir", self.repo_path, "rev-parse", "main"], text=True).strip()
        )
        self.assertEqual(target_sha, result["commitSha"])
        self.assertNotEqual(target_sha, self.main_sha)
        self.assertNotEqual(target_sha, feature_sha)

    def test_execute_squash_produces_single_commit(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        result = execute_merge(
            self.repo_path,
            "main",
            "feature",
            "squash",
            commit_message="Squashed feature work",
        )
        self.assertEqual(result["strategy"], "squash")
        subject = subprocess.check_output(
            ["git", "--git-dir", self.repo_path, "log", "-1", "main", "--format=%s"],
            text=True,
        ).strip()
        self.assertEqual(subject, "Squashed feature work")
        self.assertNotEqual(result["commitSha"], feature_sha)

    def test_execute_fast_forward_updates_target_ref(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        result = execute_merge(self.repo_path, "main", "feature", "fast_forward")
        self.assertEqual(result["strategy"], "fast_forward")
        self.assertEqual(result["commitSha"], feature_sha)
        target_sha = (
            subprocess.check_output(["git", "--git-dir", self.repo_path, "rev-parse", "main"], text=True).strip()
        )
        self.assertEqual(target_sha, feature_sha)

    def test_execute_fast_forward_fails_when_not_linear(self) -> None:
        feature_sha = self._create_branch("feature", "feature.txt", "feature\n", "add feature")
        execute_merge(self.repo_path, "main", "feature", "fast_forward")
        self._create_branch("feature", "feature.txt", "feature 2\n", "feature update")
        with self.assertRaises(Exception) as ctx:
            execute_merge(self.repo_path, "main", "feature", "fast_forward")
        self.assertIn("Fast-forward", str(ctx.exception))

    def test_execute_merge_fails_without_mutating_refs_on_conflict(self) -> None:
        feature_sha = self._create_branch("feature", "README.md", "# Feature\n", "feature change")
        other_sha = self._create_branch("other", "README.md", "# Other\n", "other change")
        before = (
            subprocess.check_output(["git", "--git-dir", self.repo_path, "rev-parse", "feature"], text=True).strip()
        )
        with self.assertRaises(Exception) as ctx:
            execute_merge(self.repo_path, "feature", "other", "merge_commit")
        self.assertIn("conflict", str(ctx.exception).lower())
        after = (
            subprocess.check_output(["git", "--git-dir", self.repo_path, "rev-parse", "feature"], text=True).strip()
        )
        self.assertEqual(before, after)
        self.assertEqual(before, feature_sha)
        self.assertNotEqual(other_sha, feature_sha)


if __name__ == "__main__":
    unittest.main()
