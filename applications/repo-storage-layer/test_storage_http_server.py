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


    def test_create_replication_artifact_encrypts_bundle(self) -> None:
        import subprocess
        import sys
        import types
        from unittest import mock

        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", str(self.repo_path)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        work = Path(self.temp_dir) / "work"
        work.mkdir()
        subprocess.run(
            ["git", "clone", str(self.repo_path), str(work)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        (work / "README").write_text("hi", encoding="utf-8")
        subprocess.run(
            ["git", "-C", str(work), "add", "README"],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            [
                "git",
                "-C",
                str(work),
                "-c",
                "user.email=t@e",
                "-c",
                "user.name=t",
                "commit",
                "-m",
                "init",
            ],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["git", "-C", str(work), "push", "origin", "main"],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

        repository_id = "33333333-3333-3333-3333-333333333333"
        fake_crypto = types.ModuleType("storage_artifact_crypto")

        def fake_encrypt(bundle_plaintext, key, repo_id, watermark, epoch, key_version):
            self.assertGreater(len(bundle_plaintext), 0)
            self.assertEqual(repository_id, repo_id)
            return (
                {
                    "epoch": epoch,
                    "watermark": watermark,
                    "bundleSha256": "ABC",
                    "keyVersion": key_version,
                },
                b"\x00" * 32,
            )

        fake_crypto.encrypt_bundle = fake_encrypt
        with mock.patch.dict(sys.modules, {"storage_artifact_crypto": fake_crypto}):
            manifest, payload = storage_http_server.create_replication_artifact(
                str(self.repo_path),
                repository_id,
                watermark=7,
                epoch=1,
                key_hex="00" * 32,
                key_version=1,
            )

        self.assertEqual(7, manifest["watermark"])
        self.assertEqual(1, manifest["epoch"])
        self.assertEqual(1, manifest["keyVersion"])
        self.assertEqual(32, len(payload))

    def test_create_replication_artifact_rejects_encrypted_role(self) -> None:
        repository_id = "44444444-4444-4444-4444-444444444444"
        storage_http_server._write_repo_role(repository_id, "EncryptedReplica")
        with self.assertRaises(ValueError):
            storage_http_server.create_replication_artifact(
                str(self.repo_path),
                repository_id,
                watermark=1,
                epoch=0,
                key_hex="00" * 32,
                key_version=1,
            )


if __name__ == "__main__":
    unittest.main()

