#!/usr/bin/env python3
"""Tests for storage-http-server path validation and sync host allowlist."""

from __future__ import annotations

import importlib.util
import os
import sys
import tempfile
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).with_name("storage-http-server.py")
SPEC = importlib.util.spec_from_file_location("storage_http_server", MODULE_PATH)
assert SPEC and SPEC.loader
storage_http_server = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = storage_http_server
SPEC.loader.exec_module(storage_http_server)


class StorageHttpServerTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.repos_root = Path(self.temp_dir) / "git"
        self.repos_root.mkdir()
        self.repo_path = self.repos_root / "sample.git"
        self.repo_path.mkdir()
        self.artifact_root = Path(self.temp_dir) / "artifacts"
        self.role_dir = Path(self.temp_dir) / "roles"
        storage_http_server.REPOS_ROOT = self.repos_root
        storage_http_server.ARTIFACT_ROOT = self.artifact_root
        storage_http_server.ROLE_DIR = self.role_dir

    def tearDown(self) -> None:
        import shutil

        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_store_and_fetch_replication_artifact_round_trips(self) -> None:
        repository_id = "11111111-1111-1111-1111-111111111111"
        manifest = {"epoch": 1, "watermark": 2, "bundleSha256": "abc", "keyVersion": 1}
        bundle = b"encrypted-bundle-bytes"

        storage_http_server.store_replication_artifact(repository_id, 2, manifest, bundle)
        fetched_manifest, fetched_bundle = storage_http_server.fetch_replication_artifact(
            repository_id,
            2,
        )

        self.assertEqual(manifest, fetched_manifest)
        self.assertEqual(bundle, fetched_bundle)

    def test_is_encrypted_replica_reflects_written_role(self) -> None:
        repository_id = "22222222-2222-2222-2222-222222222222"
        storage_http_server._write_repo_role(repository_id, "EncryptedReplica")
        self.assertTrue(storage_http_server._is_encrypted_replica(repository_id))
        self.assertFalse(storage_http_server._is_encrypted_replica("other-repo-id"))

    def test_deletable_repo_path_rejects_repos_root(self) -> None:
        self.assertFalse(storage_http_server._is_deletable_repo_path(str(self.repos_root)))

    def test_deletable_repo_path_allows_child_repo(self) -> None:
        self.assertTrue(storage_http_server._is_deletable_repo_path(str(self.repo_path)))

    def test_sync_host_allowlist_accepts_known_storage_host(self) -> None:
        self.assertTrue(storage_http_server._is_allowed_sync_host("storage-1"))

    def test_sync_host_allowlist_rejects_external_host(self) -> None:
        self.assertFalse(storage_http_server._is_allowed_sync_host("evil.example.com"))

    def test_sync_host_allowlist_rejects_url_like_host(self) -> None:
        self.assertFalse(storage_http_server._is_allowed_sync_host("http://storage-1"))

    def test_sync_from_peer_rejects_disallowed_host(self) -> None:
        with self.assertRaises(ValueError):
            storage_http_server.sync_from_peer(
                str(self.repo_path),
                "evil.example.com",
            )


if __name__ == "__main__":
    unittest.main()
