import hashlib
import tempfile
import unittest
from pathlib import Path

from storage_artifact_crypto import decrypt_bundle, encrypt_bundle


class StorageArtifactCryptoTests(unittest.TestCase):
    def test_encrypt_decrypt_roundtrip(self) -> None:
        repository_id = "11111111-1111-1111-1111-111111111111"
        repository_key = bytes.fromhex("ab" * 32)
        bundle_plaintext = b"git bundle fake payload"
        manifest, payload = encrypt_bundle(
            bundle_plaintext,
            repository_key,
            repository_id,
            watermark=3,
            epoch=7,
            key_version=1,
        )
        self.assertEqual(manifest["watermark"], 3)
        self.assertEqual(
            manifest["bundleSha256"],
            hashlib.sha256(bundle_plaintext).hexdigest().upper(),
        )
        decrypted = decrypt_bundle(payload, repository_key, repository_id, 3, 7)
        self.assertEqual(decrypted, bundle_plaintext)

    def test_cli_encrypt(self) -> None:
        repository_id = "22222222-2222-2222-2222-222222222222"
        repository_key = bytes.fromhex("cd" * 32)
        with tempfile.NamedTemporaryFile(delete=False) as bundle_file:
            bundle_file.write(b"bundle-bytes")
            bundle_path = bundle_file.name
        try:
            manifest, payload = encrypt_bundle(
                Path(bundle_path).read_bytes(),
                repository_key,
                repository_id,
                watermark=1,
                epoch=2,
                key_version=1,
            )
            self.assertIn("bundleSha256", manifest)
            self.assertGreater(len(payload), 28)
        finally:
            Path(bundle_path).unlink(missing_ok=True)


if __name__ == "__main__":
    unittest.main()
