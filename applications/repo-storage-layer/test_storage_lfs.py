#!/usr/bin/env python3
"""Tests for the Git LFS object store on storage nodes."""

from __future__ import annotations

import hashlib
import http.client
import io
import shutil
import tempfile
import threading
import unittest
from http.server import ThreadingHTTPServer
from pathlib import Path

import storage_lfs

REPOSITORY_ID = "0f3c5a52-8f7e-4c55-9a3d-2b1e6f4d7c90"


def oid_of(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


class PlaintextObjectStoreTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_dir = Path(tempfile.mkdtemp())
        self.store = storage_lfs.LfsObjectStore(self.temp_dir / "lfs")

    def tearDown(self) -> None:
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_stored_object_reads_back_byte_identical(self) -> None:
        content = b"large binary asset" * 1000

        self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content), len(content))

        with self.store.open(REPOSITORY_ID, oid_of(content)) as stored:
            self.assertEqual(content, stored.read())
        self.assertEqual(len(content), self.store.size(REPOSITORY_ID, oid_of(content)))

    def test_content_not_matching_the_oid_is_rejected_and_never_visible(self) -> None:
        announced = oid_of(b"the real file")

        with self.assertRaises(storage_lfs.LfsObjectMismatch):
            self.store.store(REPOSITORY_ID, announced, io.BytesIO(b"tampered file"), 13)

        self.assertIsNone(self.store.size(REPOSITORY_ID, announced))
        self.assertEqual([], self._leftover_files())

    def test_truncated_upload_is_rejected_and_a_retry_succeeds(self) -> None:
        content = b"0123456789" * 100

        with self.assertRaises(storage_lfs.LfsObjectMismatch):
            self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content[:500]), len(content))
        self.assertIsNone(self.store.size(REPOSITORY_ID, oid_of(content)))

        self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content), len(content))
        self.assertEqual(len(content), self.store.size(REPOSITORY_ID, oid_of(content)))

    def test_storing_an_existing_object_again_succeeds_without_change(self) -> None:
        content = b"same bytes twice"
        self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content), len(content))
        first_inode = self.store.path(REPOSITORY_ID, oid_of(content)).stat().st_ino

        self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content), len(content))

        self.assertEqual(first_inode, self.store.path(REPOSITORY_ID, oid_of(content)).stat().st_ino)

    def test_invalid_oid_or_repository_id_is_refused(self) -> None:
        for repository_id, oid in [
            ("../escape", oid_of(b"x")),
            (REPOSITORY_ID, "../../../../etc/passwd"),
            (REPOSITORY_ID, oid_of(b"x").upper()),
        ]:
            with self.subTest(repository_id=repository_id, oid=oid):
                with self.assertRaises(ValueError):
                    self.store.path(repository_id, oid)

    def _leftover_files(self) -> list[Path]:
        return [path for path in self.store.root.rglob("*") if path.is_file()]


class TransferServerTests(unittest.TestCase):
    """The loopback server nginx forwards dispatcher LFS transfers to (git HTTP port trust model)."""

    def setUp(self) -> None:
        self.temp_dir = Path(tempfile.mkdtemp())
        self.store = storage_lfs.LfsObjectStore(self.temp_dir / "lfs")
        self.encrypted_repositories: set[str] = set()
        handler = storage_lfs.make_transfer_handler(
            self.store, lambda repository_id: repository_id in self.encrypted_repositories
        )
        self.server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def tearDown(self) -> None:
        self.server.shutdown()
        self.server.server_close()
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_put_then_get_round_trips_the_object(self) -> None:
        content = b"\x00\x01binary" * 50_000

        put = self._request("PUT", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(content)}", content)
        get = self._request("GET", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(content)}")

        self.assertEqual(200, put[0])
        self.assertEqual(200, get[0])
        self.assertEqual(str(len(content)), get[1]["content-length"])
        self.assertEqual(content, get[2])

    def test_put_with_content_not_matching_the_oid_answers_422_and_stores_nothing(self) -> None:
        announced = oid_of(b"the real file")

        status, _, _ = self._request("PUT", f"/lfs/{REPOSITORY_ID}/objects/{announced}", b"tampered file")

        self.assertEqual(422, status)
        self.assertIsNone(self.store.size(REPOSITORY_ID, announced))

    def test_put_without_content_length_answers_411(self) -> None:
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_address[1], timeout=10)
        connection.putrequest("PUT", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(b'x')}")
        connection.endheaders()
        response = connection.getresponse()

        self.assertEqual(411, response.status)
        connection.close()

    def test_get_of_a_missing_object_answers_404(self) -> None:
        status, _, _ = self._request("GET", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(b'never stored')}")

        self.assertEqual(404, status)

    def test_malformed_paths_answer_404(self) -> None:
        for path in [
            f"/lfs/not-a-repository/objects/{oid_of(b'x')}",
            f"/lfs/{REPOSITORY_ID}/objects/{oid_of(b'x').upper()}",
            f"/lfs/{REPOSITORY_ID}/objects/{oid_of(b'x')}/extra",
            "/internal/repos",
        ]:
            with self.subTest(path=path):
                self.assertEqual(404, self._request("GET", path)[0])

    def test_an_encrypted_replica_never_takes_or_serves_plaintext(self) -> None:
        content = b"secret"
        self.encrypted_repositories.add(REPOSITORY_ID)

        put = self._request("PUT", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(content)}", content)
        get = self._request("GET", f"/lfs/{REPOSITORY_ID}/objects/{oid_of(content)}")

        self.assertEqual(403, put[0])
        self.assertEqual(403, get[0])
        self.assertIsNone(self.store.size(REPOSITORY_ID, oid_of(content)))

    def _request(self, method: str, path: str, body: bytes | None = None) -> tuple[int, dict[str, str], bytes]:
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_address[1], timeout=10)
        try:
            connection.request(method, path, body=body)
            response = connection.getresponse()
            headers = {key.lower(): value for key, value in response.getheaders()}
            return response.status, headers, response.read()
        finally:
            connection.close()
