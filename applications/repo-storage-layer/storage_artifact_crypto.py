#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Encrypt and decrypt git bundles for RF=4 replication artifacts."""

from __future__ import annotations

import hashlib
import json
import secrets
import sys
from typing import Any

from cryptography.hazmat.primitives.ciphers.aead import AESGCM


def compute_bundle_sha256(bundle_plaintext: bytes) -> str:
    return hashlib.sha256(bundle_plaintext).hexdigest().upper()


def build_associated_data(repository_id: str, watermark: int, epoch: int) -> bytes:
    return f"{repository_id}:{watermark}:{epoch}".encode("utf-8")


def encrypt_bundle(
    bundle_plaintext: bytes,
    repository_key: bytes,
    repository_id: str,
    watermark: int,
    epoch: int,
    key_version: int,
) -> tuple[dict[str, Any], bytes]:
    if len(repository_key) != 32:
        raise ValueError("Repository key must be 32 bytes.")

    bundle_sha256 = compute_bundle_sha256(bundle_plaintext)
    manifest: dict[str, Any] = {
        "epoch": epoch,
        "watermark": watermark,
        "bundleSha256": bundle_sha256,
        "keyVersion": key_version,
    }
    associated_data = build_associated_data(repository_id, watermark, epoch)
    nonce = secrets.token_bytes(12)
    aesgcm = AESGCM(repository_key)
    ciphertext_with_tag = aesgcm.encrypt(nonce, bundle_plaintext, associated_data)
    tag = ciphertext_with_tag[-16:]
    ciphertext = ciphertext_with_tag[:-16]
    payload = nonce + tag + ciphertext
    return manifest, payload


def decrypt_bundle(
    payload: bytes,
    repository_key: bytes,
    repository_id: str,
    watermark: int,
    epoch: int,
) -> bytes:
    if len(repository_key) != 32:
        raise ValueError("Repository key must be 32 bytes.")
    if len(payload) < 28:
        raise ValueError("Encrypted payload is too short.")

    nonce = payload[:12]
    tag = payload[12:28]
    ciphertext = payload[28:]
    associated_data = build_associated_data(repository_id, watermark, epoch)
    aesgcm = AESGCM(repository_key)
    return aesgcm.decrypt(nonce, ciphertext + tag, associated_data)


def main() -> None:
    if len(sys.argv) < 8:
        print(
            "usage: storage_artifact_crypto.py encrypt "
            "<bundle-path> <key-hex> <repository-id> <watermark> <epoch> <key-version>",
            file=sys.stderr,
        )
        sys.exit(2)

    command = sys.argv[1]
    if command != "encrypt":
        print(f"unsupported command: {command}", file=sys.stderr)
        sys.exit(2)

    bundle_path = sys.argv[2]
    repository_key = bytes.fromhex(sys.argv[3])
    repository_id = sys.argv[4]
    watermark = int(sys.argv[5])
    epoch = int(sys.argv[6])
    key_version = int(sys.argv[7])
    bundle_plaintext = open(bundle_path, "rb").read()
    manifest, payload = encrypt_bundle(
        bundle_plaintext,
        repository_key,
        repository_id,
        watermark,
        epoch,
        key_version,
    )
    print(
        json.dumps(
            {
                "manifest": manifest,
                "bundleHex": payload.hex(),
            }
        )
    )


if __name__ == "__main__":
    main()
