#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Git LFS object store on storage nodes.

Objects are content-addressed files ``{root}/{repositoryId}/{oid[0:2]}/{oid[2:4]}/{oid}``. The root sits
outside every bare repository directory, because cold recovery and re-cloning replace that directory as a
whole. Objects are streamed in and out; nothing reads a whole object into memory.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
from http.server import BaseHTTPRequestHandler
from pathlib import Path
from typing import Any, BinaryIO, Callable

CHUNK_BYTES = 1024 * 1024
OID_PATTERN = re.compile(r"^[0-9a-f]{64}$")
REPOSITORY_ID_PATTERN = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
TRANSFER_PATH_PATTERN = re.compile(r"^/lfs/(?P<repository_id>[^/]+)/objects/(?P<oid>[^/]+)$")
# A stalled client must not hold a server thread forever.
TRANSFER_SOCKET_TIMEOUT_SECONDS = 300


class LfsObjectMismatch(ValueError):
    """The uploaded bytes do not match the announced oid or size."""


def require_repository_id(repository_id: str) -> str:
    if not REPOSITORY_ID_PATTERN.match(repository_id or ""):
        raise ValueError("Invalid repository id.")
    return repository_id.lower()


def require_oid(oid: str) -> str:
    if not OID_PATTERN.match(oid or ""):
        raise ValueError("Invalid LFS oid.")
    return oid


class LfsObjectStore:
    """Plaintext objects (``content_addressed``: verified against the oid) or encrypted copies (suffix ``.aead``:
    opaque ciphertext that only the API can check)."""

    def __init__(self, root: Path, suffix: str = "", content_addressed: bool = True) -> None:
        self.root = Path(root)
        self.suffix = suffix
        self.content_addressed = content_addressed

    def path(self, repository_id: str, oid: str) -> Path:
        repository_id = require_repository_id(repository_id)
        oid = require_oid(oid)
        return self.root / repository_id / oid[0:2] / oid[2:4] / f"{oid}{self.suffix}"

    def size(self, repository_id: str, oid: str) -> int | None:
        try:
            return self.path(repository_id, oid).stat().st_size
        except FileNotFoundError:
            return None

    def open(self, repository_id: str, oid: str) -> BinaryIO:
        return self.path(repository_id, oid).open("rb")

    def store(self, repository_id: str, oid: str, stream: BinaryIO, size: int) -> None:
        """Reads exactly ``size`` bytes from ``stream`` and stores them under ``oid``.

        The bytes go to a temporary file beside the target and are moved into place only when complete (and,
        for plaintext, when their SHA-256 equals the oid), so a reader never sees a partial or wrong object. Two
        uploads of the same object at once both succeed: either result is valid, and the rename is atomic.
        An encrypted copy is always replaced, so a re-upload repairs a damaged one.
        """
        target = self.path(repository_id, oid)
        if size < 0:
            raise LfsObjectMismatch("Object size must not be negative.")
        if self.content_addressed and self.size(repository_id, oid) == size:
            _drain(stream, size)
            return

        target.parent.mkdir(parents=True, exist_ok=True)
        temporary = target.with_name(f".{target.name}.{os.getpid()}.{id(stream)}.tmp")
        digest = hashlib.sha256()
        received = 0
        try:
            with temporary.open("wb") as output:
                while received < size:
                    chunk = stream.read(min(CHUNK_BYTES, size - received))
                    if not chunk:
                        break
                    digest.update(chunk)
                    output.write(chunk)
                    received += len(chunk)
                output.flush()
                os.fsync(output.fileno())
            if received != size:
                raise LfsObjectMismatch(f"Expected {size} bytes, received {received}.")
            if self.content_addressed and digest.hexdigest() != oid:
                raise LfsObjectMismatch("Content does not match the LFS oid.")
            os.replace(temporary, target)
        finally:
            temporary.unlink(missing_ok=True)


def _drain(stream: BinaryIO, size: int) -> None:
    remaining = size
    while remaining > 0:
        chunk = stream.read(min(CHUNK_BYTES, remaining))
        if not chunk:
            return
        remaining -= len(chunk)


def send_json(handler: BaseHTTPRequestHandler, status: int, payload: dict[str, Any]) -> None:
    body = json.dumps(payload).encode("utf-8")
    handler.send_response(status)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(body)))
    handler.end_headers()
    handler.wfile.write(body)


def send_object(
    handler: BaseHTTPRequestHandler, store: LfsObjectStore, repository_id: str, oid: str, include_body: bool
) -> None:
    """Answers GET or HEAD for one object, streaming the file in chunks."""
    try:
        source = store.open(repository_id, oid)
    except FileNotFoundError:
        send_json(handler, 404, {"error": "LFS object not found."})
        return
    with source:
        size = os.fstat(source.fileno()).st_size
        handler.send_response(200)
        handler.send_header("Content-Type", "application/octet-stream")
        handler.send_header("Content-Length", str(size))
        handler.end_headers()
        if include_body:
            shutil.copyfileobj(source, handler.wfile, CHUNK_BYTES)


def receive_object(handler: BaseHTTPRequestHandler, store: LfsObjectStore, repository_id: str, oid: str) -> None:
    """Answers PUT for one object: exactly Content-Length bytes, verified against the oid."""
    content_length = handler.headers.get("Content-Length")
    if content_length is None or not content_length.isdigit():
        send_json(handler, 411, {"error": "Content-Length is required."})
        return
    try:
        store.store(repository_id, oid, handler.rfile, int(content_length))
    except LfsObjectMismatch as exc:
        send_json(handler, 422, {"error": str(exc)})
        return
    send_json(handler, 200, {"oid": oid, "size": int(content_length)})


def make_transfer_handler(
    store: LfsObjectStore, is_encrypted_replica: Callable[[str], bool]
) -> type[BaseHTTPRequestHandler]:
    """Handler for the loopback transfer server. nginx forwards ``/lfs/...`` from the git HTTP port here,
    so these routes share that port's trust model: no token, reachable only from the internal network.
    Objects are content-addressed, so a caller can add an object but never change one."""

    class LfsTransferHandler(BaseHTTPRequestHandler):
        server_version = "OpenGitBaseStorageLfs/1.0"
        timeout = TRANSFER_SOCKET_TIMEOUT_SECONDS

        def log_message(self, format: str, *args: Any) -> None:  # noqa: A002 - stdlib name
            print(f"storage-lfs: {self.address_string()} - {format % args}")

        def do_GET(self) -> None:
            self._route(lambda repository_id, oid: send_object(self, store, repository_id, oid, True))

        def do_HEAD(self) -> None:
            self._route(lambda repository_id, oid: send_object(self, store, repository_id, oid, False))

        def do_PUT(self) -> None:
            self._route(lambda repository_id, oid: receive_object(self, store, repository_id, oid))

        def _route(self, action: Callable[[str, str], None]) -> None:
            match = TRANSFER_PATH_PATTERN.match(self.path)
            if not match or not _is_valid_object_reference(match.group("repository_id"), match.group("oid")):
                send_json(self, 404, {"error": "Not found."})
                return
            repository_id = match.group("repository_id").lower()
            if is_encrypted_replica(repository_id):
                send_json(self, 403, {"error": "Plaintext LFS objects are not allowed on encrypted replica storage."})
                return
            action(repository_id, match.group("oid"))

    return LfsTransferHandler


def _is_valid_object_reference(repository_id: str, oid: str) -> bool:
    return bool(REPOSITORY_ID_PATTERN.match(repository_id) and OID_PATTERN.match(oid))
