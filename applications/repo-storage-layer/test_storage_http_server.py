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


    def _init_repository_with_one_commit(self) -> None:
        import subprocess

        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", str(self.repo_path)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        work = Path(self.temp_dir) / "work"
        subprocess.run(
            ["git", "clone", str(self.repo_path), str(work)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        (work / "README").write_text("hi", encoding="utf-8")
        for command in (
            ["add", "README"],
            ["-c", "user.email=t@e", "-c", "user.name=t", "commit", "-m", "init"],
            ["push", "origin", "main"],
        ):
            subprocess.run(
                ["git", "-C", str(work), *command],
                check=True,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )

    def test_create_replication_artifact_trusts_a_repository_owned_by_the_git_user(self) -> None:
        # storage-http-server runs as root and the repositories belong to git, so a git call that
        # discovers the repository (-C) stops at "detected dubious ownership" unless it is trusted.
        import subprocess
        import types
        from unittest import mock

        self._init_repository_with_one_commit()
        fake_crypto = types.ModuleType("storage_artifact_crypto")
        fake_crypto.encrypt_bundle = lambda bundle, key, repo_id, watermark, epoch, version: (
            {"epoch": epoch, "watermark": watermark, "bundleSha256": "ABC", "keyVersion": version},
            bundle,
        )
        real_run = subprocess.run
        commands: list[list[str]] = []

        def spy(command, *args, **kwargs):
            commands.append(list(command))
            return real_run(command, *args, **kwargs)

        with mock.patch.dict(sys.modules, {"storage_artifact_crypto": fake_crypto}):
            with mock.patch.object(storage_http_server.subprocess, "run", side_effect=spy):
                storage_http_server.create_replication_artifact(
                    str(self.repo_path),
                    "33333333-3333-3333-3333-333333333333",
                    watermark=7,
                    epoch=1,
                    key_hex="00" * 32,
                    key_version=1,
                )

        bundle_commands = [command for command in commands if "bundle" in command]
        self.assertEqual(1, len(bundle_commands))
        self.assertEqual(["git", "-c", "safe.directory=*"], bundle_commands[0][:3])

    def test_replication_hook_hands_the_whole_push_to_the_replicate_script_once(self) -> None:
        import subprocess
        from unittest import mock

        log = Path(self.temp_dir) / "replicate.log"
        stub = Path(self.temp_dir) / "replicate.sh"
        stub.write_text(
            "#!/usr/bin/env bash\n"
            f'printf "args:%s\\n" "$*" >> "{log}"\n'
            f'cat >> "{log}"\n'
            "exit 3\n",
            encoding="utf-8",
        )
        stub.chmod(0o755)

        with mock.patch.object(storage_http_server, "REPLICATE_SCRIPT", str(stub)):
            with mock.patch.object(storage_http_server.subprocess, "run"):  # chown git:git
                storage_http_server._install_replication_hook(str(self.repo_path))

        hook = self.repo_path / "hooks" / "post-receive"
        result = subprocess.run(
            [str(hook)],
            input="o1 n1 refs/heads/main\no2 n2 refs/heads/feature\n",
            text=True,
            capture_output=True,
            check=False,
        )

        # The script's status reaches storage_merge.py, which rolls a merge back on failure.
        self.assertEqual(3, result.returncode)
        self.assertEqual(
            f"args:{self.repo_path} --post-receive\n"
            "o1 n1 refs/heads/main\n"
            "o2 n2 refs/heads/feature\n",
            log.read_text(encoding="utf-8"),
        )

    def test_a_failed_artifact_write_keeps_the_previous_artifact_whole(self) -> None:
        from unittest import mock

        repository_id = "55555555-5555-5555-5555-555555555555"
        old_manifest = {"epoch": 1, "watermark": 2, "bundleSha256": "old", "keyVersion": 1}
        storage_http_server.store_replication_artifact(repository_id, 2, old_manifest, b"old-bundle")

        with mock.patch.object(Path, "write_bytes", side_effect=OSError("No space left on device")):
            with self.assertRaises(OSError):
                storage_http_server.store_replication_artifact(
                    repository_id,
                    2,
                    {"epoch": 1, "watermark": 2, "bundleSha256": "new", "keyVersion": 1},
                    b"new-bundle",
                )

        manifest, bundle = storage_http_server.fetch_replication_artifact(repository_id, 2)
        self.assertEqual(old_manifest, manifest)
        self.assertEqual(b"old-bundle", bundle)
        self.assertEqual(["2"], sorted(entry.name for entry in (self.artifact_root / repository_id).iterdir()))

    def test_replacing_an_artifact_swaps_manifest_and_bundle_together(self) -> None:
        repository_id = "66666666-6666-6666-6666-666666666666"
        storage_http_server.store_replication_artifact(
            repository_id, 2, {"watermark": 2, "bundleSha256": "old"}, b"old-bundle"
        )
        new_manifest = {"watermark": 2, "bundleSha256": "new"}

        storage_http_server.store_replication_artifact(repository_id, 2, new_manifest, b"new-bundle")

        self.assertEqual(
            (new_manifest, b"new-bundle"),
            storage_http_server.fetch_replication_artifact(repository_id, 2),
        )
        self.assertEqual(["2"], sorted(entry.name for entry in (self.artifact_root / repository_id).iterdir()))
        self.assertEqual(2, storage_http_server.on_disk_artifact_watermark(repository_id))


class StorageHttpMissingBlobTests(unittest.TestCase):
    """The file endpoints answer 404 for a file that does not exist, never 500.

    The body's code tells a path absent at a revision this node holds (not_found) from a
    revision the node lacks (invalid_ref), as a replica that has not caught up reports it.
    """

    def setUp(self) -> None:
        import subprocess
        import threading
        from http.server import ThreadingHTTPServer
        from unittest import mock

        self.temp_dir = tempfile.mkdtemp()
        repos_root = Path(self.temp_dir) / "git"
        self.repo_path = repos_root / "sample.git"
        work = Path(self.temp_dir) / "work"
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", str(self.repo_path)],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(["git", "clone", str(self.repo_path), str(work)], check=True, capture_output=True)
        (work / "README").write_text("hi", encoding="utf-8")
        for command in (
            ["add", "README"],
            ["-c", "user.email=t@e", "-c", "user.name=t", "commit", "-m", "init"],
            ["push", "origin", "main"],
        ):
            subprocess.run(["git", "-C", str(work), *command], check=True, capture_output=True)

        for name, value in (
            ("REPOS_ROOT", repos_root),
            ("STORAGE_API_TOKEN", "test-token"),
            ("STORAGE_TOKEN_FILE", str(Path(self.temp_dir) / "no-token-file")),
        ):
            patcher = mock.patch.object(storage_http_server, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        quiet = mock.patch.object(storage_http_server.StorageHttpHandler, "log_message")
        quiet.start()
        self.addCleanup(quiet.stop)

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), storage_http_server.StorageHttpHandler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def tearDown(self) -> None:
        import shutil

        self.server.shutdown()
        self.server.server_close()
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def _get(self, endpoint: str, ref: str, path: str) -> tuple[int, dict]:
        import json
        import urllib.error
        import urllib.request
        from urllib.parse import urlencode

        query = urlencode({"physicalPath": str(self.repo_path), "ref": ref, "path": path})
        request = urllib.request.Request(
            f"http://127.0.0.1:{self.server.server_address[1]}{endpoint}?{query}",
            headers={"Authorization": "Bearer test-token"},
        )
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, {}
        except urllib.error.HTTPError as error:
            return error.code, json.loads(error.read())

    def _assert_status(self, ref: str, path: str, status: int, code: str) -> None:
        for endpoint in ("/internal/repos/content/blob", "/internal/repos/content/blob/raw"):
            with self.subTest(endpoint=endpoint):
                actual_status, body = self._get(endpoint, ref, path)
                self.assertEqual((status, code), (actual_status, body.get("code")))

    def test_a_missing_file_is_404_not_found(self) -> None:
        self._assert_status("main", "missing.txt", 404, "not_found")

    def test_a_commit_the_node_lacks_is_404_invalid_ref(self) -> None:
        self._assert_status("1" * 40, "README", 404, "invalid_ref")


class StorageImageTests(unittest.TestCase):
    def test_the_image_trusts_repositories_owned_by_the_git_user(self) -> None:
        # Root runs git in repositories that belong to git (create-artifact, content, merges).
        # Without this, git stops at "detected dubious ownership"; the fix used to live only in
        # a container's writable layer and vanished with every recreate.
        dockerfile = Path(__file__).with_name("Dockerfile").read_text(encoding="utf-8")
        self.assertIn("git config --system --add safe.directory '*'", dockerfile)


if __name__ == "__main__":
    unittest.main()

