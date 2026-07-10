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
    get_blob,
    get_disk_usage,
    get_raw_bytes,
    is_empty_repository,
    list_branches,
    list_tags,
    list_tree,
    resolve_readme,
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


if __name__ == "__main__":
    unittest.main()
