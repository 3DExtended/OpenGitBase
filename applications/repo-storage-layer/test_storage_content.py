#!/usr/bin/env python3
"""Tests for storage_content git read helpers."""

from __future__ import annotations

import io
import os
import subprocess
import tarfile
import tempfile
import unittest
from pathlib import Path

from storage_content import (
    INLINE_MAX_BYTES,
    GitContentError,
    build_workspace_archive,
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


def lfs_pointer(oid: str, size: int) -> bytes:
    return f"version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize {size}\n".encode("ascii")


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

    def _commit_files(self, files: dict[str, bytes]) -> None:
        work = Path(self.temp_dir) / "work"
        for name, content in files.items():
            (work / name).parent.mkdir(parents=True, exist_ok=True)
            (work / name).write_bytes(content)
        for command in (["add", "."], ["commit", "-m", "files"], ["push", self.repo_path, "main"]):
            subprocess.run(["git", *command], cwd=work, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def test_get_blob_of_an_lfs_pointer_describes_the_lfs_object(self) -> None:
        oid = "c" * 64
        self._commit_files({"assets/logo.png": lfs_pointer(oid, 5_000_000), "assets/data.bin": lfs_pointer(oid, 7)})

        image = get_blob(self.repo_path, "main", "assets/logo.png")
        binary = get_blob(self.repo_path, "main", "assets/data.bin")

        self.assertEqual({"oid": oid, "size": 5_000_000}, image["lfs"])
        self.assertEqual(5_000_000, image["size"])
        self.assertEqual("image", image["previewKind"])
        self.assertTrue(image["isBinary"])
        self.assertNotIn("contentBase64", image)
        self.assertNotIn("textContent", image)
        self.assertEqual({"oid": oid, "size": 7}, binary["lfs"])
        self.assertEqual("binary", binary["previewKind"])
        self.assertNotIn("textContent", binary)

    def test_get_blob_of_text_that_only_looks_like_a_pointer_returns_the_text(self) -> None:
        crlf = lfs_pointer("d" * 64, 3).replace(b"\n", b"\r\n")
        self._commit_files({"notes.txt": crlf})

        blob = get_blob(self.repo_path, "main", "notes.txt")

        self.assertNotIn("lfs", blob)
        self.assertEqual("text", blob["previewKind"])
        self.assertIn("git-lfs.github.com", blob["textContent"])

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

    def test_get_disk_usage_counts_the_bytes_stored(self) -> None:
        # GNU du answers -sb with apparent bytes, which for a repository of small files is less
        # than its 1 KiB blocks (-sk, the fallback on macOS and busybox). Either way a
        # 200 000-byte file counts as about 200 000 bytes.
        directory = Path(self.temp_dir) / "sized"
        directory.mkdir()
        (directory / "blob").write_bytes(b"x" * 200_000)

        usage = get_disk_usage(str(directory))

        self.assertGreaterEqual(usage, 200_000)
        self.assertLess(usage, 200_000 + 64 * 1024)

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


class StorageContentMissingBlobTests(unittest.TestCase):
    """A file read calls a path that is absent at a revision this repository holds not_found.

    Git words a commit the repository lacks the same way ("path 'x' does not exist in
    '<sha>'"), for example on a replica that has not caught up yet. That case stays
    invalid_ref, so a lagging replica never claims the file does not exist.
    """

    # Shares the sample repository without running StorageContentTests' tests again.
    setUp = StorageContentTests.setUp
    tearDown = StorageContentTests.tearDown

    def _assert_read_fails(self, ref: str, path: str, code: str) -> None:
        for read in (get_blob, get_raw_bytes):
            with self.subTest(read=read.__name__, ref=ref, path=path):
                with self.assertRaises(GitContentError) as ctx:
                    read(self.repo_path, ref, path)
                self.assertEqual(ctx.exception.code, code)

    def test_a_path_missing_at_a_branch_is_not_found(self) -> None:
        for path in ("missing.txt", "src/missing.ts", "README.md/child"):
            self._assert_read_fails("main", path, "not_found")

    def test_a_path_missing_at_a_commit_sha_is_not_found(self) -> None:
        self._assert_read_fails(resolve_ref(self.repo_path, "main"), "missing.txt", "not_found")

    def test_a_commit_the_repository_lacks_is_an_invalid_ref(self) -> None:
        self._assert_read_fails("1" * 40, "README.md", "invalid_ref")

    def test_an_unknown_branch_is_an_invalid_ref(self) -> None:
        self._assert_read_fails("no-such-branch", "README.md", "invalid_ref")


class StorageContentMissingTreeTests(unittest.TestCase):
    """A listing calls a directory that is absent at a revision this repository holds not_found.

    ls-tree words a revision the repository lacks like a missing directory ("Not a valid
    object name", "not a tree object"), for example on a replica that has not caught up
    yet. That case stays invalid_ref, so a lagging replica never claims the directory does
    not exist.
    """

    # Shares the sample repository without running StorageContentTests' tests again.
    setUp = StorageContentTests.setUp
    tearDown = StorageContentTests.tearDown

    def _assert_listing_fails(self, ref: str, path: str, code: str) -> None:
        with self.subTest(ref=ref, path=path):
            with self.assertRaises(GitContentError) as ctx:
                list_tree(self.repo_path, ref, path)
            self.assertEqual(ctx.exception.code, code)

    def test_a_directory_missing_at_a_branch_is_not_found(self) -> None:
        for path in ("missing-dir", "src/missing", "README.md/child"):
            self._assert_listing_fails("main", path, "not_found")

    def test_a_path_naming_a_file_is_not_found(self) -> None:
        for path in ("README.md", "src/app.ts"):
            self._assert_listing_fails("main", path, "not_found")

    def test_a_directory_missing_at_a_commit_sha_is_not_found(self) -> None:
        self._assert_listing_fails(resolve_ref(self.repo_path, "main"), "missing-dir", "not_found")

    def test_a_commit_the_repository_lacks_is_an_invalid_ref(self) -> None:
        for path in ("", "src"):
            self._assert_listing_fails("1" * 40, path, "invalid_ref")

    def test_an_unknown_branch_is_an_invalid_ref(self) -> None:
        for path in ("", "src"):
            self._assert_listing_fails("no-such-branch", path, "invalid_ref")


class StorageContentResolveRefTests(unittest.TestCase):
    """resolve_ref calls a revision the repository lacks invalid_ref, not a git failure."""

    setUp = StorageContentTests.setUp
    tearDown = StorageContentTests.tearDown

    def test_an_unknown_revision_is_an_invalid_ref(self) -> None:
        for ref in ("no-such-branch", "main~5"):
            with self.subTest(ref=ref):
                with self.assertRaises(GitContentError) as ctx:
                    resolve_ref(self.repo_path, ref)
                self.assertEqual(ctx.exception.code, "invalid_ref")


class StorageContentWorkspaceArchiveTests(unittest.TestCase):
    """A CI job's workspace is a tar.gz of the pushed commit, built where the bare repository lives.

    Depth 0 packs the commit's files alone; depth N packs a shallow clone of that commit with
    its last N commits and .git, for jobs that need history.
    """

    setUp = StorageContentTests.setUp
    tearDown = StorageContentTests.tearDown

    def _push_second_commit(self) -> tuple[str, str]:
        first = resolve_ref(self.repo_path, "main")
        work = Path(self.temp_dir) / "work"
        (work / "README.md").write_text("# Changed\n", encoding="utf-8")
        (work / "later.txt").write_text("later\n", encoding="utf-8")
        for command in (["add", "-A"], ["commit", "-m", "second"], ["push", self.repo_path, "main"]):
            subprocess.run(["git", *command], cwd=work, check=True, capture_output=True)
        return first, resolve_ref(self.repo_path, "main")

    def _extract(self, archive: bytes) -> Path:
        target = Path(tempfile.mkdtemp(dir=self.temp_dir))
        with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as tar:
            tar.extractall(target, filter="data")
        return target

    def test_depth_zero_packs_the_files_of_the_requested_commit_without_history(self) -> None:
        first, _ = self._push_second_commit()

        workspace = self._extract(build_workspace_archive(self.repo_path, first, 0))

        self.assertEqual("# Hello\n", (workspace / "README.md").read_text(encoding="utf-8"))
        self.assertEqual("export const ok = true;\n", (workspace / "src" / "app.ts").read_text(encoding="utf-8"))
        self.assertFalse((workspace / "later.txt").exists())
        self.assertFalse((workspace / ".git").exists())

    def test_depth_packs_a_shallow_clone_checked_out_at_the_requested_commit(self) -> None:
        first, second = self._push_second_commit()

        for sha, depth, readme in ((first, 1, "# Hello\n"), (second, 2, "# Changed\n")):
            with self.subTest(sha=sha, depth=depth):
                workspace = self._extract(build_workspace_archive(self.repo_path, sha, depth))

                def git(*args: str) -> str:
                    return subprocess.run(
                        ["git", "-C", str(workspace), *args], check=True, capture_output=True, text=True
                    ).stdout.strip()

                self.assertEqual(readme, (workspace / "README.md").read_text(encoding="utf-8"))
                self.assertEqual(sha, git("rev-parse", "HEAD"))
                self.assertEqual("true", git("rev-parse", "--is-shallow-repository"))
                self.assertEqual(str(depth), git("rev-list", "--count", "HEAD"))
                self.assertEqual("", git("status", "--porcelain"))

    def test_a_commit_the_repository_lacks_is_an_invalid_ref(self) -> None:
        for depth in (0, 1):
            with self.subTest(depth=depth):
                with self.assertRaises(GitContentError) as ctx:
                    build_workspace_archive(self.repo_path, "1" * 40, depth)
                self.assertEqual(ctx.exception.code, "invalid_ref")

    def test_only_a_full_commit_id_is_accepted(self) -> None:
        for sha in ("main", resolve_ref(self.repo_path, "main")[:12], "--output=/tmp/x"):
            with self.subTest(sha=sha):
                with self.assertRaises(GitContentError) as ctx:
                    build_workspace_archive(self.repo_path, sha, 0)
                self.assertEqual(ctx.exception.code, "invalid_ref")


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
