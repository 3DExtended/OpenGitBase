#!/usr/bin/env python3
"""Tests for storage_content git read helpers."""

from __future__ import annotations

import os
import subprocess
import tempfile
import unittest
from pathlib import Path

from storage_content import (
    INLINE_MAX_BYTES,
    GitContentError,
    count_commits_ahead,
    get_blob,
    get_disk_usage,
    get_raw_bytes,
    is_empty_repository,
    list_branches,
    list_tags,
    list_tree,
    resolve_readme,
    resolve_ref,
)


class StorageContentTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.repo_path = os.path.join(self.temp_dir, "sample.git")
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", self.repo_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        work = Path(self.temp_dir) / "work"
        work.mkdir()
        subprocess.run(["git", "init", "-b", "main"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        (work / "README.md").write_text("# Hello\n", encoding="utf-8")
        (work / "readme.txt").write_text("ignored\n", encoding="utf-8")
        (work / "src").mkdir()
        (work / "src" / "app.ts").write_text("export const ok = true;\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(["git", "commit", "-m", "init"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(
            ["git", "push", self.repo_path, "main"],
            cwd=work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "--git-dir", self.repo_path, "tag", "v1.0.0"],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

    def tearDown(self) -> None:
        import shutil

        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_list_branches_and_tags(self) -> None:
        branches = list_branches(self.repo_path)
        self.assertEqual([item["name"] for item in branches], ["main"])
        tags = list_tags(self.repo_path)
        self.assertEqual([item["name"] for item in tags], ["v1.0.0"])

    def test_list_tree_root(self) -> None:
        tree = list_tree(self.repo_path, "main", "")
        names = {entry["name"] for entry in tree["entries"]}
        self.assertIn("README.md", names)
        self.assertIn("src", names)

    def test_readme_precedence(self) -> None:
        readme = resolve_readme(self.repo_path, "main")
        assert readme is not None
        self.assertEqual(readme["fileName"], "README.md")
        self.assertIn("# Hello", readme["markdownSource"])

    def test_get_blob_text(self) -> None:
        blob = get_blob(self.repo_path, "main", "src/app.ts")
        self.assertEqual(blob["previewKind"], "text")
        self.assertIn("export const ok", blob["textContent"])

    def test_get_raw_bytes(self) -> None:
        raw, path = get_raw_bytes(self.repo_path, "main", "README.md")
        self.assertEqual(path, "README.md")
        self.assertIn(b"# Hello", raw)

    def test_get_raw_bytes_rejects_oversized_blob(self) -> None:
        work = Path(self.temp_dir) / "work"
        large = work / "large.bin"
        large.write_bytes(b"\0" * (INLINE_MAX_BYTES + 1))
        subprocess.run(["git", "add", "large.bin"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(["git", "commit", "-m", "large"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(
            ["git", "push", self.repo_path, "main"],
            cwd=work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        with self.assertRaises(GitContentError) as ctx:
            get_raw_bytes(self.repo_path, "main", "large.bin")
        self.assertEqual(ctx.exception.code, "too_large")

    def test_oversized_blob_flag(self) -> None:
        work = Path(self.temp_dir) / "work"
        large = work / "large.bin"
        large.write_bytes(b"\0" * (INLINE_MAX_BYTES + 1))
        subprocess.run(["git", "add", "large.bin"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(["git", "commit", "-m", "large"], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(
            ["git", "push", self.repo_path, "main"],
            cwd=work,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        blob = get_blob(self.repo_path, "main", "large.bin")
        self.assertTrue(blob["isTooLarge"])
        self.assertNotIn("textContent", blob)

    def test_get_disk_usage(self) -> None:
        usage = get_disk_usage(self.repo_path)
        self.assertGreater(usage, 0)

    def test_get_disk_usage_empty_repo(self) -> None:
        empty_repo = os.path.join(self.temp_dir, "empty.git")
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", empty_repo],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        usage = get_disk_usage(empty_repo)
        self.assertGreaterEqual(usage, 0)

    def test_get_disk_usage_matches_du_sk(self) -> None:
        usage = get_disk_usage(self.repo_path)
        du_result = subprocess.run(
            ["du", "-sk", self.repo_path],
            check=True,
            capture_output=True,
            text=True,
        )
        expected_kb = int(du_result.stdout.split()[0])
        self.assertGreaterEqual(usage, expected_kb * 1024)

    def test_empty_repository(self) -> None:
        empty_repo = os.path.join(self.temp_dir, "empty.git")
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", empty_repo],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self.assertTrue(is_empty_repository(empty_repo))
        self.assertFalse(is_empty_repository(self.repo_path))


class StorageContentRefInjectionTests(StorageContentTests):
    """Refs that begin with '-' must never reach git as an option.

    Git's own ref-format rules forbid a leading dash, so rejecting these
    rejects nothing a client could legitimately ask for, while closing the
    argument-injection path (e.g. a value that git would read as --output=…).
    """

    def test_resolve_ref_rejects_leading_dash(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            resolve_ref(self.repo_path, "--output=/tmp/pwned")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_count_commits_ahead_rejects_leading_dash_base(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            count_commits_ahead(self.repo_path, "--all", "main")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_count_commits_ahead_rejects_leading_dash_head(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            count_commits_ahead(self.repo_path, "main", "--all")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_list_tree_rejects_leading_dash_ref(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            list_tree(self.repo_path, "--output=/tmp/pwned", "")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_get_blob_rejects_leading_dash_ref(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            get_blob(self.repo_path, "--output=/tmp/pwned", "README.md")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_get_raw_bytes_rejects_leading_dash_ref(self) -> None:
        with self.assertRaises(GitContentError) as ctx:
            get_raw_bytes(self.repo_path, "--output=/tmp/pwned", "README.md")
        self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_resolve_ref_still_resolves_a_real_branch(self) -> None:
        self.assertEqual(len(resolve_ref(self.repo_path, "main")), 40)


class StorageContentLastModifiedTests(unittest.TestCase):
    """Each listed entry carries the date of the newest commit that changed it."""

    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.repo_path = os.path.join(self.temp_dir, "dated.git")
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", self.repo_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        self.work = Path(self.temp_dir) / "work"
        self.work.mkdir()
        self._git("init", "-b", "main")

    def tearDown(self) -> None:
        import shutil

        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def _git(self, *args: str, when: str | None = None, check: bool = True) -> None:
        env = {
            **os.environ,
            "GIT_AUTHOR_NAME": "Dated Tester",
            "GIT_AUTHOR_EMAIL": "dated@example.com",
            "GIT_COMMITTER_NAME": "Dated Tester",
            "GIT_COMMITTER_EMAIL": "dated@example.com",
        }
        if when is not None:
            env["GIT_AUTHOR_DATE"] = when
            env["GIT_COMMITTER_DATE"] = when
        subprocess.run(
            ["git", *args],
            cwd=self.work,
            env=env,
            check=check,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

    def _write(self, files: dict[str, str]) -> None:
        for relative_path, content in files.items():
            target = self.work / relative_path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(content, encoding="utf-8")

    def _commit(self, files: dict[str, str], when: str) -> None:
        self._write(files)
        self._git("add", "-A")
        self._git("commit", "-m", f"change at {when}", when=when)

    def _push(self) -> None:
        self._git("push", self.repo_path, "main")

    @staticmethod
    def _dates(tree: dict) -> dict[str, str | None]:
        return {entry["name"]: entry["lastModifiedAt"] for entry in tree["entries"]}

    def _commit_history(self) -> None:
        self._commit(
            {"README.md": "# v1\n", "LICENSE": "MIT\n", "src/app.ts": "export {}\n"},
            "2024-01-01T00:00:00+00:00",
        )
        self._commit({"src/util.ts": "export const util = 1\n"}, "2024-06-01T12:00:00+00:00")
        self._commit({"README.md": "# v2\n"}, "2025-02-03T04:05:06+00:00")
        self._push()

    def test_root_entries_carry_their_last_commit_date(self) -> None:
        self._commit_history()

        tree = list_tree(self.repo_path, "main", "")

        self.assertEqual(
            self._dates(tree),
            {
                "LICENSE": "2024-01-01T00:00:00Z",
                "README.md": "2025-02-03T04:05:06Z",
                "src": "2024-06-01T12:00:00Z",
            },
        )

    def test_subdirectory_entries_carry_their_last_commit_date(self) -> None:
        self._commit_history()

        tree = list_tree(self.repo_path, "main", "src")

        self.assertEqual(
            self._dates(tree),
            {
                "app.ts": "2024-01-01T00:00:00Z",
                "util.ts": "2024-06-01T12:00:00Z",
            },
        )

    def test_dates_follow_the_requested_ref_not_the_newest_branch(self) -> None:
        self._commit_history()
        self._git("--git-dir", self.repo_path, "tag", "before-readme", "main~1")

        tree = list_tree(self.repo_path, "before-readme", "")

        self.assertEqual(self._dates(tree)["README.md"], "2024-01-01T00:00:00Z")

    def test_names_that_git_would_quote_are_listed_verbatim_with_dates(self) -> None:
        self._commit(
            {"über \"quoted\".md": "x\n", "docs/new\nline.txt": "y\n"},
            "2024-03-04T05:06:07+00:00",
        )
        self._push()

        root = list_tree(self.repo_path, "main", "")
        docs = list_tree(self.repo_path, "main", "docs")

        self.assertEqual(
            self._dates(root),
            {"docs": "2024-03-04T05:06:07Z", "über \"quoted\".md": "2024-03-04T05:06:07Z"},
        )
        self.assertEqual(
            [(entry["name"], entry["path"]) for entry in docs["entries"]],
            [("new\nline.txt", "docs/new\nline.txt")],
        )
        self.assertEqual(self._dates(docs), {"new\nline.txt": "2024-03-04T05:06:07Z"})

    def test_merge_that_resolves_a_conflict_counts_as_a_change(self) -> None:
        self._commit({"shared.txt": "base\n", "other.txt": "o\n"}, "2024-01-01T00:00:00+00:00")
        self._git("checkout", "-b", "feature")
        self._commit({"shared.txt": "feature\n"}, "2024-02-01T00:00:00+00:00")
        self._git("checkout", "main")
        self._commit({"shared.txt": "main\n"}, "2024-03-01T00:00:00+00:00")
        self._git("merge", "feature", check=False)
        self._write({"shared.txt": "resolved\n"})
        self._git("add", "shared.txt")
        self._git("commit", "--no-edit", when="2024-04-01T00:00:00+00:00")
        self._push()

        tree = list_tree(self.repo_path, "main", "")

        self.assertEqual(
            self._dates(tree),
            {"other.txt": "2024-01-01T00:00:00Z", "shared.txt": "2024-04-01T00:00:00Z"},
        )

    def test_entries_stay_listed_without_dates_once_the_time_budget_runs_out(self) -> None:
        self._commit_history()

        tree = list_tree(self.repo_path, "main", "", last_modified_budget_seconds=0)

        self.assertEqual(
            self._dates(tree),
            {"LICENSE": None, "README.md": None, "src": None},
        )


if __name__ == "__main__":
    unittest.main()
