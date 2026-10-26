#!/usr/bin/env python3
"""Tests for storage push validation fail-closed behavior and LFS pointer detection."""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from typing import Any
from unittest.mock import patch

from storage_push_validation import NULL_SHA, collect_lfs_pointers, validate_push

OID_A = "a" * 64
OID_B = "b" * 64
OID_C = "c" * 64


def pointer(oid: str, size: int) -> bytes:
    return f"version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize {size}\n".encode("ascii")


class BareRepository:
    """A bare repository in which commits can be created without moving refs, like objects a push brings in
    before pre-receive accepts it."""

    def __init__(self, root: Path) -> None:
        self.git_dir = str(root / "repo.git")
        subprocess.run(["git", "init", "--bare", "-q", self.git_dir], check=True)

    def git(self, *args: str, data: bytes | None = None) -> str:
        env = {
            **os.environ,
            "GIT_AUTHOR_NAME": "t",
            "GIT_AUTHOR_EMAIL": "t@example.com",
            "GIT_COMMITTER_NAME": "t",
            "GIT_COMMITTER_EMAIL": "t@example.com",
        }
        result = subprocess.run(
            ["git", "--git-dir", self.git_dir, *args], input=data, capture_output=True, check=True, env=env
        )
        return result.stdout.decode("utf-8").strip()

    def commit(self, files: dict[str, bytes], parent: str | None = None) -> str:
        """A commit whose tree is ``files`` (paths may contain one directory level)."""
        entries: dict[str, list[str]] = {}
        root: list[str] = []
        for path, content in files.items():
            blob = self.git("hash-object", "-w", "--stdin", data=content)
            if "/" in path:
                directory, name = path.split("/", 1)
                entries.setdefault(directory, []).append(f"100644 blob {blob}\t{name}")
            else:
                root.append(f"100644 blob {blob}\t{path}")
        for directory, lines in entries.items():
            tree = self.git("mktree", data=("\n".join(lines) + "\n").encode("utf-8"))
            root.append(f"040000 tree {tree}\t{directory}")
        tree = self.git("mktree", data=("\n".join(root) + "\n").encode("utf-8"))
        args = ["commit-tree", tree, "-m", "c"]
        if parent:
            args += ["-p", parent]
        return self.git(*args)

    def set_ref(self, ref: str, sha: str) -> None:
        self.git("update-ref", ref, sha)


class StoragePushValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temp = tempfile.TemporaryDirectory()
        self.repo = BareRepository(Path(self._temp.name))

    def tearDown(self) -> None:
        self._temp.cleanup()

    def test_validate_push_fails_closed_when_api_url_missing(self) -> None:
        with patch.dict(os.environ, {}, clear=True):
            with patch("storage_push_validation.API_URL_FILE") as api_url_file:
                api_url_file.is_file.return_value = False
                with self.assertRaises(RuntimeError) as ctx:
                    validate_push("/srv/git/sample.git", [("0" * 40, "a" * 40, "refs/heads/main")])

        self.assertIn("OPENGITBASE_API_URL", str(ctx.exception))

    def test_collect_lfs_pointers_finds_pointers_with_their_path_and_ref(self) -> None:
        commit = self.repo.commit({"assets/big.bin": pointer(OID_A, 5000), "README.md": b"hello\n"})

        pointers = collect_lfs_pointers(self.repo.git_dir, [(NULL_SHA, commit, "refs/heads/main")])

        self.assertEqual([{"oid": OID_A, "size": 5000, "path": "assets/big.bin", "refName": "refs/heads/main"}], pointers)

    def test_collect_lfs_pointers_ignores_pointers_already_in_history(self) -> None:
        base = self.repo.commit({"old.bin": pointer(OID_A, 1)})
        self.repo.set_ref("refs/heads/main", base)
        update = self.repo.commit({"old.bin": pointer(OID_A, 1), "new.bin": pointer(OID_B, 2)}, parent=base)

        pointers = collect_lfs_pointers(self.repo.git_dir, [(base, update, "refs/heads/main")])

        self.assertEqual([OID_B], [item["oid"] for item in pointers])

    def test_collect_lfs_pointers_for_a_new_branch_checks_only_commits_not_on_other_branches(self) -> None:
        base = self.repo.commit({"old.bin": pointer(OID_A, 1)})
        self.repo.set_ref("refs/heads/main", base)
        middle = self.repo.commit({"old.bin": pointer(OID_A, 1), "mid.bin": pointer(OID_B, 2)}, parent=base)
        tip = self.repo.commit(
            {"old.bin": pointer(OID_A, 1), "mid.bin": pointer(OID_B, 2), "tip.bin": pointer(OID_C, 3)}, parent=middle
        )

        pointers = collect_lfs_pointers(self.repo.git_dir, [(NULL_SHA, tip, "refs/heads/feature")])

        self.assertEqual({OID_B, OID_C}, {item["oid"] for item in pointers})

    def test_collect_lfs_pointers_ignores_small_non_pointer_and_malformed_blobs(self) -> None:
        commit = self.repo.commit(
            {
                "notes.txt": b"version https://git-lfs.github.com/spec/v1 is mentioned here\n",
                "short-oid.bin": b"version https://git-lfs.github.com/spec/v1\noid sha256:abc\nsize 3\n",
                "no-size.bin": b"version https://git-lfs.github.com/spec/v1\noid sha256:" + OID_A.encode() + b"\n",
                "extra.bin": pointer(OID_A, 1) + b"trailing garbage\n",
                "large.bin": pointer(OID_B, 1) + b"x" * 2000,
            }
        )

        pointers = collect_lfs_pointers(self.repo.git_dir, [(NULL_SHA, commit, "refs/heads/main")])

        self.assertEqual([], pointers)

    def test_collect_lfs_pointers_reports_each_oid_once(self) -> None:
        commit = self.repo.commit({"a.bin": pointer(OID_A, 7), "copy/a.bin": pointer(OID_A, 7)})

        pointers = collect_lfs_pointers(
            self.repo.git_dir,
            [(NULL_SHA, commit, "refs/heads/main"), (NULL_SHA, commit, "refs/heads/other")],
        )

        self.assertEqual([OID_A], [item["oid"] for item in pointers])

    def test_collect_lfs_pointers_ignores_deleted_refs(self) -> None:
        commit = self.repo.commit({"a.bin": pointer(OID_A, 7)})
        self.repo.set_ref("refs/heads/main", commit)

        self.assertEqual([], collect_lfs_pointers(self.repo.git_dir, [(commit, NULL_SHA, "refs/heads/main")]))

    def test_validate_push_sends_the_pointers_with_the_commits(self) -> None:
        commit = self.repo.commit({"a.bin": pointer(OID_A, 7)})
        sent: list[dict[str, Any]] = []

        class Response:
            def __enter__(self) -> "Response":
                return self

            def __exit__(self, *args: object) -> None:
                return None

            def read(self) -> bytes:
                return b'{"allowed": true}'

        def urlopen(request: Any, timeout: int) -> Response:
            sent.append(json.loads(request.data.decode("utf-8")))
            return Response()

        with patch.dict(os.environ, {"OPENGITBASE_API_URL": "http://api.test"}):
            with patch("storage_push_validation.urllib.request.urlopen", urlopen):
                validate_push(self.repo.git_dir, [(NULL_SHA, commit, "refs/heads/main")])

        self.assertEqual([OID_A], [item["oid"] for item in sent[0]["lfsPointers"]])
        self.assertEqual([commit], [item["sha"] for item in sent[0]["commits"]])

    def test_validate_push_reports_the_api_reason_when_rejected(self) -> None:
        commit = self.repo.commit({"a.bin": pointer(OID_A, 7)})

        class Response:
            def __enter__(self) -> "Response":
                return self

            def __exit__(self, *args: object) -> None:
                return None

            def read(self) -> bytes:
                return json.dumps({"allowed": False, "reason": f"LFS object {OID_A} (a.bin) is missing"}).encode()

        with patch.dict(os.environ, {"OPENGITBASE_API_URL": "http://api.test"}):
            with patch("storage_push_validation.urllib.request.urlopen", lambda request, timeout: Response()):
                with self.assertRaises(RuntimeError) as ctx:
                    validate_push(self.repo.git_dir, [(NULL_SHA, commit, "refs/heads/main")])

        self.assertIn("is missing", str(ctx.exception))


if __name__ == "__main__":
    unittest.main()
