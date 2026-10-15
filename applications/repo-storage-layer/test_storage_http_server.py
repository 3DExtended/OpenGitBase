#!/usr/bin/env python3
"""Tests for storage-http-server path validation and sync host allowlist."""

from __future__ import annotations

import base64
import hashlib
import http.client
import importlib.util
import io
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer
from pathlib import Path
from unittest import mock

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


class LfsInternalEndpointTests(unittest.TestCase):
    REPOSITORY_ID = "0f3c5a52-8f7e-4c55-9a3d-2b1e6f4d7c90"
    TOKEN = "node-token"

    def setUp(self) -> None:
        self.temp_dir = Path(tempfile.mkdtemp())
        self.repos_root = self.temp_dir / "git"
        self.repos_root.mkdir()
        self.patches = [
            mock.patch.object(storage_http_server, "REPOS_ROOT", self.repos_root),
            mock.patch.object(storage_http_server, "LFS_ROOT", self.repos_root / ".lfs"),
            mock.patch.object(storage_http_server, "LFS_ENCRYPTED_ROOT", self.temp_dir / "data" / "lfs"),
            mock.patch.object(storage_http_server, "ARTIFACT_ROOT", self.temp_dir / "data" / "artifacts"),
            mock.patch.object(storage_http_server, "ROLE_DIR", self.temp_dir / "roles"),
            mock.patch.object(storage_http_server, "WATERMARK_DIR", self.temp_dir / "watermarks"),
            mock.patch.object(storage_http_server, "STORAGE_API_TOKEN", self.TOKEN),
            mock.patch.object(storage_http_server, "STORAGE_TOKEN_FILE", str(self.temp_dir / "no-token-file")),
            # chown to the git user needs root; the hook content is covered elsewhere.
            mock.patch.object(storage_http_server, "_install_replication_hook", lambda physical_path: None),
        ]
        for patch in self.patches:
            patch.start()
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), storage_http_server.StorageHttpHandler)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def tearDown(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        for patch in reversed(self.patches):
            patch.stop()
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_head_reports_the_size_of_a_stored_object(self) -> None:
        content = b"lfs object" * 1000
        oid = self._store(content)

        status, headers, body = self._request("HEAD", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}")

        self.assertEqual(200, status)
        self.assertEqual(str(len(content)), headers["content-length"])
        self.assertEqual(b"", body)

    def test_get_streams_a_stored_object(self) -> None:
        content = b"lfs object" * 1000
        oid = self._store(content)

        status, _, body = self._request("GET", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}")

        self.assertEqual(200, status)
        self.assertEqual(content, body)

    def test_head_of_a_missing_object_answers_404(self) -> None:
        oid = hashlib.sha256(b"never stored").hexdigest()

        status, _, _ = self._request("HEAD", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}")

        self.assertEqual(404, status)

    def test_lfs_endpoints_require_the_node_token(self) -> None:
        oid = self._store(b"x")

        for method in ("HEAD", "GET"):
            with self.subTest(method=method):
                status, _, _ = self._request(method, f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}", token=None)
                self.assertEqual(401, status)

    def test_importing_a_bundle_over_the_repository_leaves_its_lfs_objects_intact(self) -> None:
        oid = self._store(b"large asset")
        physical_path = self.repos_root / f"{self.REPOSITORY_ID}.git"
        bundle = self._bundle_with_one_commit()

        for _ in range(2):  # the second import replaces an existing repository directory
            status, _, body = self._request(
                "POST",
                "/internal/repos/import-bundle",
                json.dumps({"physicalPath": str(physical_path), "bundleBase64": base64.b64encode(bundle).decode()}).encode(),
            )
            self.assertEqual(201, status, body)

        status, _, _ = self._request("HEAD", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}")
        self.assertEqual(200, status)
        self.assertNotIn(storage_http_server.LFS_ROOT.resolve(), physical_path.resolve().parents)

    def test_put_stores_a_plaintext_object_verified_against_its_oid(self) -> None:
        content = b"read replica copy" * 500
        oid = hashlib.sha256(content).hexdigest()

        status, _, _ = self._request("PUT", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}", content)
        wrong, _, _ = self._request("PUT", f"/internal/lfs/{self.REPOSITORY_ID}/objects/{'0' * 64}", content)

        self.assertEqual(200, status)
        self.assertEqual(len(content), storage_http_server.lfs_store().size(self.REPOSITORY_ID, oid))
        self.assertEqual(422, wrong)

    def test_encrypted_replica_stores_and_serves_encrypted_copies_outside_the_artifact_tree(self) -> None:
        self._set_role("EncryptedReplica")
        oid = hashlib.sha256(b"plaintext the replica never sees").hexdigest()
        ciphertext = b"OLFS" + bytes(range(256)) * 20

        put, _, _ = self._request("PUT", f"/internal/lfs/{self.REPOSITORY_ID}/encrypted/{oid}", ciphertext)
        head, headers, _ = self._request("HEAD", f"/internal/lfs/{self.REPOSITORY_ID}/encrypted/{oid}")
        get, _, body = self._request("GET", f"/internal/lfs/{self.REPOSITORY_ID}/encrypted/{oid}")

        self.assertEqual(200, put)
        self.assertEqual((200, str(len(ciphertext))), (head, headers["content-length"]))
        self.assertEqual((200, ciphertext), (get, body))
        stored = storage_http_server.encrypted_lfs_store().path(self.REPOSITORY_ID, oid)
        self.assertTrue(stored.name.endswith(".aead"))
        self.assertNotIn(storage_http_server.ARTIFACT_ROOT.resolve(), stored.resolve().parents)
        self.assertNotIn(storage_http_server.REPOS_ROOT.resolve(), stored.resolve().parents)

    def test_encrypted_endpoints_refuse_on_a_plaintext_node(self) -> None:
        oid = hashlib.sha256(b"x").hexdigest()
        for role in (None, "Primary", "ReadReplica"):
            if role:
                self._set_role(role)
            for method, body in (("PUT", b"OLFS"), ("GET", None), ("HEAD", None)):
                with self.subTest(role=role, method=method):
                    status, _, _ = self._request(method, f"/internal/lfs/{self.REPOSITORY_ID}/encrypted/{oid}", body)
                    self.assertEqual(403, status)

    def test_plaintext_endpoints_refuse_on_an_encrypted_replica(self) -> None:
        content = b"secret"
        oid = self._store(content)
        self._set_role("EncryptedReplica")

        for method, body in (("PUT", content), ("GET", None), ("HEAD", None)):
            with self.subTest(method=method):
                status, _, _ = self._request(method, f"/internal/lfs/{self.REPOSITORY_ID}/objects/{oid}", body)
                self.assertEqual(403, status)

    def test_encrypted_lfs_endpoints_require_the_node_token(self) -> None:
        self._set_role("EncryptedReplica")
        oid = hashlib.sha256(b"x").hexdigest()

        for method in ("HEAD", "GET", "PUT"):
            with self.subTest(method=method):
                status, _, _ = self._request(
                    method, f"/internal/lfs/{self.REPOSITORY_ID}/encrypted/{oid}", b"OLFS", token=None
                )
                self.assertEqual(401, status)

    def test_the_lfs_root_is_never_a_repository_path(self) -> None:
        lfs_root = storage_http_server.LFS_ROOT

        self.assertFalse(storage_http_server._is_valid_physical_path(str(lfs_root)))
        self.assertFalse(storage_http_server._is_valid_physical_path(str(lfs_root / self.REPOSITORY_ID)))
        self.assertFalse(storage_http_server._is_deletable_repo_path(str(lfs_root)))
        self.assertTrue(storage_http_server._is_valid_physical_path(str(self.repos_root / f"{self.REPOSITORY_ID}.git")))

    def _set_role(self, role: str) -> None:
        storage_http_server._write_repo_role(self.REPOSITORY_ID, role)

    def _store(self, content: bytes) -> str:
        oid = hashlib.sha256(content).hexdigest()
        storage_http_server.lfs_store().store(self.REPOSITORY_ID, oid, io.BytesIO(content), len(content))
        return oid

    def _bundle_with_one_commit(self) -> bytes:
        work = self.temp_dir / "work"
        subprocess.run(["git", "init", "-q", "-b", "main", str(work)], check=True)
        (work / "README.md").write_text("hello", encoding="utf-8")
        git = ["git", "-C", str(work), "-c", "user.name=t", "-c", "user.email=t@example.com"]
        subprocess.run([*git, "add", "."], check=True)
        subprocess.run([*git, "commit", "-q", "-m", "init"], check=True)
        bundle_path = self.temp_dir / "repo.bundle"
        subprocess.run([*git, "bundle", "create", "-q", str(bundle_path), "--all"], check=True)
        return bundle_path.read_bytes()

    def _request(
        self, method: str, path: str, body: bytes | None = None, token: str | None = TOKEN
    ) -> tuple[int, dict[str, str], bytes]:
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_address[1], timeout=30)
        headers = {"Authorization": f"Bearer {token}"} if token else {}
        try:
            connection.request(method, path, body=body, headers=headers)
            response = connection.getresponse()
            return response.status, {k.lower(): v for k, v in response.getheaders()}, response.read()
        finally:
            connection.close()


class StorageImageTests(unittest.TestCase):
    def test_the_image_trusts_repositories_owned_by_the_git_user(self) -> None:
        # Root runs git in repositories that belong to git (create-artifact, content, merges).
        # Without this, git stops at "detected dubious ownership"; the fix used to live only in
        # a container's writable layer and vanished with every recreate.
        dockerfile = Path(__file__).with_name("Dockerfile").read_text(encoding="utf-8")
        self.assertIn("git config --system --add safe.directory '*'", dockerfile)

    def test_the_image_ships_the_lfs_module(self) -> None:
        dockerfile = Path(__file__).with_name("Dockerfile").read_text(encoding="utf-8")
        self.assertIn("COPY storage_lfs.py /usr/local/bin/storage_lfs.py", dockerfile)

    def test_the_git_http_port_streams_lfs_transfers_to_the_loopback_server(self) -> None:
        dispatcher_server, mtls_server = self._nginx_servers()

        self.assertIn('location ~ "^/lfs/[0-9a-fA-F-]{36}/objects/[0-9a-f]{64}$"', dispatcher_server)
        self.assertIn("proxy_pass http://127.0.0.1:8083;", dispatcher_server)
        # Buffering would write every upload to nginx's temp dir before python sees a byte.
        self.assertIn("proxy_request_buffering off;", dispatcher_server)
        self.assertIn("proxy_buffering off;", dispatcher_server)
        self.assertNotIn("/lfs/", mtls_server)

    @staticmethod
    def _nginx_servers() -> tuple[str, str]:
        config = Path(__file__).with_name("nginx-git-http.conf").read_text(encoding="utf-8")
        dispatcher_start = config.index("listen 8082;")
        mtls_start = config.index("listen 8443 ssl;")
        return config[dispatcher_start:mtls_start], config[mtls_start:]


if __name__ == "__main__":
    unittest.main()

