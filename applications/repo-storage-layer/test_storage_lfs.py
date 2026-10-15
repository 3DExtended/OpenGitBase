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


def pointer_text(oid: str, size: int) -> bytes:
    return f"version https://git-lfs.github.com/spec/v1\noid sha256:{oid}\nsize {size}\n".encode("ascii")


class PointerTests(unittest.TestCase):
    OID = "a" * 64

    def test_parse_pointer_reads_oid_and_size_of_an_exact_pointer(self) -> None:
        self.assertEqual((self.OID, 5000), storage_lfs.parse_pointer(pointer_text(self.OID, 5000)))

    def test_parse_pointer_accepts_an_empty_object(self) -> None:
        self.assertEqual((self.OID, 0), storage_lfs.parse_pointer(pointer_text(self.OID, 0)))

    def test_parse_pointer_rejects_what_only_looks_like_a_pointer(self) -> None:
        exact = pointer_text(self.OID, 12).decode("ascii")
        for name, content in {
            "crlf line ends": exact.replace("\n", "\r\n"),
            "no trailing newline": exact.rstrip("\n"),
            "extension key": exact.replace("oid ", "ext-0-foo sha256:" + "b" * 64 + "\noid "),
            "upper-case oid": exact.replace(self.OID, self.OID.upper()),
            "short oid": exact.replace(self.OID, "a" * 63),
            "leading zero size": exact.replace("size 12", "size 012"),
            "other spec version": exact.replace("spec/v1", "spec/v2"),
            "trailing text": exact + "more\n",
            "leading blank line": "\n" + exact,
        }.items():
            with self.subTest(name):
                self.assertIsNone(storage_lfs.parse_pointer(content.encode("ascii")))

    def test_parse_pointer_rejects_content_larger_than_a_pointer(self) -> None:
        padded = pointer_text(self.OID, 1) + b" " * storage_lfs.POINTER_MAX_BYTES

        self.assertIsNone(storage_lfs.parse_pointer(padded))


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

    def test_inventory_lists_the_stored_objects_of_one_repository_with_their_sizes(self) -> None:
        first, second = b"first object", b"second, longer object"
        other_repository = "1a2b3c4d-0000-4000-8000-000000000001"
        for content in (first, second):
            self.store.store(REPOSITORY_ID, oid_of(content), io.BytesIO(content), len(content))
        self.store.store(other_repository, oid_of(b"other"), io.BytesIO(b"other"), 5)

        inventory = self.store.inventory(REPOSITORY_ID)

        self.assertEqual(
            sorted([{"oid": oid_of(first), "size": len(first)}, {"oid": oid_of(second), "size": len(second)}],
                   key=lambda item: item["oid"]),
            inventory,
        )

    def test_inventory_of_a_repository_without_objects_is_empty(self) -> None:
        self.assertEqual([], self.store.inventory(REPOSITORY_ID))

    def test_inventory_skips_partial_uploads_and_misplaced_files(self) -> None:
        content = b"complete object"
        oid = oid_of(content)
        self.store.store(REPOSITORY_ID, oid, io.BytesIO(content), len(content))
        directory = self.store.path(REPOSITORY_ID, oid).parent
        (directory / f".{oid}.123.456.tmp").write_bytes(b"partial")
        misplaced = oid_of(b"misplaced")
        (directory / misplaced).write_bytes(b"misplaced")
        (directory / "notes.txt").write_bytes(b"not an object")

        self.assertEqual([{"oid": oid, "size": len(content)}], self.store.inventory(REPOSITORY_ID))

    def test_inventory_of_encrypted_copies_names_them_by_oid(self) -> None:
        store = storage_lfs.LfsObjectStore(self.temp_dir / "encrypted", suffix=".aead", content_addressed=False)
        oid = oid_of(b"plaintext")
        store.store(REPOSITORY_ID, oid, io.BytesIO(b"OLFS ciphertext"), 15)

        self.assertEqual([{"oid": oid, "size": 15}], store.inventory(REPOSITORY_ID))

    def test_inventory_refuses_an_invalid_repository_id(self) -> None:
        with self.assertRaises(ValueError):
            self.store.inventory("../escape")

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
