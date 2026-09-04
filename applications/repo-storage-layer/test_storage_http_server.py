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

    def test_on_disk_artifact_watermark_none_when_no_artifacts(self) -> None:
        repository_id = "33333333-3333-3333-3333-333333333333"
        self.assertIsNone(storage_http_server.on_disk_artifact_watermark(repository_id))

    def test_on_disk_artifact_watermark_returns_highest_complete(self) -> None:
        repository_id = "44444444-4444-4444-4444-444444444444"
        manifest = {"epoch": 1, "watermark": 1, "bundleSha256": "abc", "keyVersion": 1}
        storage_http_server.store_replication_artifact(repository_id, 2, manifest, b"x")
        storage_http_server.store_replication_artifact(repository_id, 5, manifest, b"y")
        storage_http_server.store_replication_artifact(repository_id, 3, manifest, b"z")

        self.assertEqual(5, storage_http_server.on_disk_artifact_watermark(repository_id))

    def test_on_disk_artifact_watermark_ignores_incomplete_and_nondigit(self) -> None:
        repository_id = "55555555-5555-5555-5555-555555555555"
        manifest = {"epoch": 1, "watermark": 1, "bundleSha256": "abc", "keyVersion": 1}
        storage_http_server.store_replication_artifact(repository_id, 4, manifest, b"complete")
        # A half-written artifact (manifest present, bundle missing) must not count.
        incomplete = self.artifact_root / repository_id / "9"
        incomplete.mkdir(parents=True)
        (incomplete / "manifest.json").write_text("{}", encoding="utf-8")
        # A stray non-numeric directory must be ignored.
        (self.artifact_root / repository_id / "tmp").mkdir()

        self.assertEqual(4, storage_http_server.on_disk_artifact_watermark(repository_id))

    def test_on_disk_inventory_reports_guid_plaintext_repos_only(self) -> None:
        # setUp already created a non-GUID "sample.git" repo, which must be ignored.
        guid_repo = "66666666-6666-6666-6666-666666666666"
        (self.repos_root / f"{guid_repo}.git").mkdir()
        (self.repos_root / "not-a-repo").mkdir()  # no .git suffix -> ignored

        inventory = storage_http_server.on_disk_inventory()

        self.assertEqual([guid_repo], inventory["plaintextRepositories"])

    def test_on_disk_inventory_reports_artifact_repos_with_watermark(self) -> None:
        repository_id = "77777777-7777-7777-7777-777777777777"
        manifest = {"epoch": 1, "watermark": 1, "bundleSha256": "abc", "keyVersion": 1}
        storage_http_server.store_replication_artifact(repository_id, 2, manifest, b"x")
        storage_http_server.store_replication_artifact(repository_id, 6, manifest, b"y")

        inventory = storage_http_server.on_disk_inventory()

        self.assertIn(
            {"repositoryId": repository_id, "artifactWatermark": 6},
            inventory["artifactRepositories"],
        )

    def test_on_disk_inventory_excludes_artifact_repo_with_no_complete_set(self) -> None:
        repository_id = "88888888-8888-8888-8888-888888888888"
        # A directory with only a half-written artifact contributes no artifact watermark.
        incomplete = self.artifact_root / repository_id / "3"
        incomplete.mkdir(parents=True)
        (incomplete / "manifest.json").write_text("{}", encoding="utf-8")

        inventory = storage_http_server.on_disk_inventory()

        self.assertNotIn(
            repository_id,
            [entry["repositoryId"] for entry in inventory["artifactRepositories"]],
        )

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

