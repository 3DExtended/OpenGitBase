#!/usr/bin/env python3
"""Tests for scripts/storage-quorum-replicate.sh against a stub control-plane API (#413)."""

from __future__ import annotations

import json
import os
import shutil
import socket
import subprocess
import tempfile
import threading
import time
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

SCRIPT = Path(__file__).with_name("scripts") / "storage-quorum-replicate.sh"
REPO_ID = "0f3c5a52-8f7e-4c55-9a3d-2b1e6f4d7c90"
SHA_A = "a" * 40
SHA_B = "b" * 40
ZERO = "0" * 40


class StubApi:
    """Answers the calls the script makes and records them."""

    def __init__(self) -> None:
        self.contexts: list[tuple[int, Any]] = []
        self.quorum_responses: list[tuple[int, Any]] = []
        self.ingest_status = 202
        self.quorum_delay_seconds = 0.0
        self.requests: list[dict[str, Any]] = []
        self._lock = threading.Lock()
        stub = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, format: str, *args: Any) -> None:  # noqa: A002 - stdlib name
                return

            def _record(self) -> dict[str, Any]:
                length = int(self.headers.get("Content-Length") or 0)
                body = self.rfile.read(length).decode("utf-8") if length else ""
                entry = {
                    "method": self.command,
                    "path": self.path,
                    "headers": {key.lower(): value for key, value in self.headers.items()},
                    "body": json.loads(body) if body else None,
                }
                with stub._lock:
                    stub.requests.append(entry)
                return entry

            def _answer(self, status: int, payload: Any) -> None:
                data = b"" if payload is None else (
                    payload.encode("utf-8") if isinstance(payload, str) else json.dumps(payload).encode("utf-8")
                )
                self.send_response(status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def do_GET(self) -> None:  # noqa: N802 - stdlib name
                self._record()
                if self.path.endswith("/replication"):
                    with stub._lock:
                        status, payload = stub.contexts.pop(0) if len(stub.contexts) > 1 else stub.contexts[0]
                    self._answer(status, payload)
                    return
                self._answer(404, {"error": "unexpected"})

            def do_POST(self) -> None:  # noqa: N802 - stdlib name
                self._record()
                if self.path.endswith("/quorum-replicate"):
                    if stub.quorum_delay_seconds:
                        time.sleep(stub.quorum_delay_seconds)
                    with stub._lock:
                        status, payload = (
                            stub.quorum_responses.pop(0)
                            if len(stub.quorum_responses) > 1
                            else stub.quorum_responses[0]
                        )
                    self._answer(status, payload)
                    return
                if self.path == "/api/v1/internal/pipelines/git-push-ingest":
                    self._answer(stub.ingest_status, None)
                    return
                self._answer(404, {"error": "unexpected"})

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.url = f"http://127.0.0.1:{self.server.server_address[1]}"
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def close(self) -> None:
        self.server.shutdown()
        self.server.server_close()

    def calls(self, suffix: str) -> list[dict[str, Any]]:
        with self._lock:
            return [request for request in self.requests if request["path"].endswith(suffix)]


def context(primary_watermark: int, **overrides: Any) -> dict[str, Any]:
    payload: dict[str, Any] = {
        "repositoryId": REPO_ID,
        "replicationEpoch": 1,
        "primaryWatermark": primary_watermark,
        "isPrimary": True,
        "physicalPath": f"/srv/git/{REPO_ID}.git",
        "replicationState": "Rf4Healthy",
        "peers": [],
        "apiCreatesArtifacts": True,
    }
    payload.update(overrides)
    return payload


class StorageQuorumReplicateScriptTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.cert_dir = tempfile.mkdtemp()
        subprocess.run(
            [
                "openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes",
                "-keyout", os.path.join(cls.cert_dir, "node.key"),
                "-out", os.path.join(cls.cert_dir, "node.crt"),
                "-days", "1", "-subj", "/CN=storage-test",
            ],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.cert_dir, ignore_errors=True)

    def setUp(self) -> None:
        self.temp_dir = tempfile.mkdtemp()
        self.repo_path = os.path.join(self.temp_dir, "git", f"{REPO_ID}.git")
        os.makedirs(self.repo_path)
        self.watermark_dir = Path(self.temp_dir) / "watermarks"
        self.watermark_dir.mkdir()
        self.token_file = Path(self.temp_dir) / "api-token"
        self.token_file.write_text("node-token", encoding="utf-8")
        self.api = StubApi()
        self.api.contexts = [(200, context(5))]
        self.api.quorum_responses = [(200, {"success": True, "primaryWatermark": 6})]

    def tearDown(self) -> None:
        self.api.close()
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def run_script(
        self,
        *args: str,
        stdin: str = "",
        api_url: str | None = None,
        extra_env: dict[str, str] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        env = {
            "PATH": os.environ.get("PATH", "/usr/bin:/bin"),
            "TMPDIR": self.temp_dir,
            "STORAGE_CONFIG_DIR": self.temp_dir,
            "STORAGE_API_URL": api_url or self.api.url,
            "STORAGE_TOKEN_FILE": str(self.token_file),
            "STORAGE_NODE_ID": "storage-1",
            "STORAGE_NODE_CERT_FILE": os.path.join(self.cert_dir, "node.crt"),
            "STORAGE_WATERMARK_DIR": str(self.watermark_dir),
            "STORAGE_API_RETRIES": "0",
        }
        env.update(extra_env or {})
        return subprocess.run(
            ["bash", str(SCRIPT), self.repo_path, *args],
            input=stdin,
            env=env,
            capture_output=True,
            text=True,
            timeout=60,
        )

    def watermark(self) -> str:
        return (self.watermark_dir / f"{REPO_ID}.txt").read_text(encoding="utf-8")

    def set_watermark(self, value: int) -> None:
        (self.watermark_dir / f"{REPO_ID}.txt").write_text(str(value), encoding="utf-8")

    def stderr_lines(self, result: subprocess.CompletedProcess[str]) -> list[str]:
        self.assertNotIn("Traceback", result.stderr)
        return [line for line in result.stderr.splitlines() if line.strip()]

    def test_post_receive_replicates_once_and_hands_every_ref_to_pipelines(self) -> None:
        self.set_watermark(5)

        result = self.run_script(
            "--post-receive",
            stdin=f"{ZERO} {SHA_A} refs/heads/main\n{ZERO} {SHA_B} refs/heads/feature\n",
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([], self.stderr_lines(result))
        quorum = self.api.calls("/quorum-replicate")
        self.assertEqual(1, len(quorum))
        self.assertEqual({"appliedWatermark": 6, "createArtifact": True}, quorum[0]["body"])
        self.assertEqual([], self.api.calls("/repository-key"))
        self.assertEqual([], self.api.calls("/replicate-artifact"))
        ingested = [call["body"] for call in self.api.calls("/git-push-ingest")]
        self.assertEqual(
            [
                {"repositoryId": REPO_ID, "ref": "refs/heads/main", "afterSha": SHA_A},
                {"repositoryId": REPO_ID, "ref": "refs/heads/feature", "afterSha": SHA_B},
            ],
            ingested,
        )
        self.assertEqual("6", self.watermark())

    def test_credentials_travel_as_headers(self) -> None:
        self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        headers = self.api.calls("/quorum-replicate")[0]["headers"]
        self.assertEqual("Bearer node-token", headers["authorization"])
        self.assertEqual("storage-1", headers["x-storage-node-id"])
        self.assertRegex(headers["x-storage-node-certificate-thumbprint"], r"^[0-9A-F]{64}$")

    def test_a_failed_replication_says_so_in_one_line_and_still_hands_the_push_to_pipelines(self) -> None:
        self.set_watermark(5)
        self.api.quorum_responses = [
            (409, {"success": False, "error": "No encrypted replica stored the artifact (storage-2: HTTP 413: Payload Too Large)."}),
        ]

        result = self.run_script(
            "--post-receive",
            stdin=f"{ZERO} {SHA_A} refs/heads/main\n{ZERO} {SHA_B} refs/heads/feature\n",
        )

        self.assertEqual(1, result.returncode)
        self.assertEqual(
            [
                f"storage-quorum-replicate: {REPO_ID} stored on the primary only; encrypted replication failed "
                "(HTTP 409: No encrypted replica stored the artifact (storage-2: HTTP 413: Payload Too Large).) "
                "- the reconciler will retry"
            ],
            self.stderr_lines(result),
        )
        # Not rolled back: the heartbeat reports 6, so the reconciler finds the write.
        self.assertEqual("6", self.watermark())
        self.assertEqual(2, len(self.api.calls("/git-push-ingest")))

    def test_an_http_error_without_a_body_is_reported_with_its_status(self) -> None:
        self.api.quorum_responses = [(413, None)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual(
            [
                f"storage-quorum-replicate: {REPO_ID} stored on the primary only; encrypted replication failed "
                "(HTTP 413) - the reconciler will retry"
            ],
            self.stderr_lines(result),
        )

    def test_a_stale_watermark_is_retried_once_with_the_next_one(self) -> None:
        self.api.contexts = [(200, context(5)), (200, context(6))]
        self.api.quorum_responses = [
            (409, {"success": False, "stale": True, "error": "Watermark 6 is already committed."}),
            (200, {"success": True, "primaryWatermark": 7}),
        ]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(
            [6, 7],
            [call["body"]["appliedWatermark"] for call in self.api.calls("/quorum-replicate")],
        )
        self.assertEqual("7", self.watermark())

    def test_a_failure_after_the_watermark_was_committed_elsewhere_keeps_the_write_pending(self) -> None:
        # Another push or the reconciler committed 6 while this push's call failed, from an
        # artifact that may lack this push. Left at 6 the heartbeat would report the primary in
        # sync and nothing would ever replicate this push.
        self.api.contexts = [(200, context(5)), (200, context(6))]
        self.api.quorum_responses = [(502, None)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual(
            [
                f"storage-quorum-replicate: {REPO_ID} stored on the primary only; encrypted replication failed "
                "(HTTP 502) - the reconciler will retry"
            ],
            self.stderr_lines(result),
        )
        self.assertEqual("7", self.watermark())
        self.assertEqual(1, len(self.api.calls("/git-push-ingest")))

    def test_a_watermark_taken_twice_keeps_the_write_pending(self) -> None:
        self.api.contexts = [(200, context(5)), (200, context(6)), (200, context(7))]
        self.api.quorum_responses = [
            (409, {"success": False, "stale": True, "error": "Watermark 6 was committed concurrently."}),
            (409, {"success": False, "stale": True, "error": "Watermark 7 was committed concurrently."}),
        ]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual(
            [6, 7],
            [call["body"]["appliedWatermark"] for call in self.api.calls("/quorum-replicate")],
        )
        self.assertEqual("8", self.watermark())

    def test_a_quorum_call_that_times_out_while_the_api_commits_keeps_the_write_pending(self) -> None:
        self.api.contexts = [(200, context(5)), (200, context(6))]
        self.api.quorum_delay_seconds = 3

        result = self.run_script(
            "--post-receive",
            stdin=f"{ZERO} {SHA_A} refs/heads/main\n",
            extra_env={"STORAGE_QUORUM_TIMEOUT_SECONDS": "1"},
        )

        self.assertEqual(1, result.returncode)
        lines = self.stderr_lines(result)
        self.assertEqual(1, len(lines), lines)
        self.assertIn("encrypted replication failed (API unreachable", lines[0])
        self.assertEqual("7", self.watermark())

    def test_when_the_api_cannot_be_asked_again_the_file_goes_past_the_target(self) -> None:
        self.api.contexts = [(200, context(5)), (503, {"error": "Service Unavailable"})]
        self.api.quorum_responses = [(502, None)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual(1, len(self.stderr_lines(result)))
        self.assertEqual("7", self.watermark())

    def test_a_failure_with_nothing_committed_meanwhile_leaves_the_file_at_the_target(self) -> None:
        self.api.contexts = [(200, context(5)), (200, context(5))]
        self.api.quorum_responses = [(502, None)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual("6", self.watermark())
        self.assertEqual(2, len(self.api.calls("/replication")))

    def test_a_node_that_lost_the_primary_role_meanwhile_leaves_its_file_alone(self) -> None:
        self.api.contexts = [(200, context(5)), (200, context(6, isPrimary=False))]
        self.api.quorum_responses = [(502, None)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        self.assertEqual("6", self.watermark())

    def test_an_unreachable_api_is_one_line_and_leaves_the_write_for_the_reconciler(self) -> None:
        self.set_watermark(5)
        with socket.socket() as probe:
            probe.bind(("127.0.0.1", 0))
            closed_port = probe.getsockname()[1]

        result = self.run_script(
            "--post-receive",
            stdin=f"{ZERO} {SHA_A} refs/heads/main\n",
            api_url=f"http://127.0.0.1:{closed_port}",
        )

        self.assertEqual(1, result.returncode)
        lines = self.stderr_lines(result)
        self.assertEqual(2, len(lines), lines)
        self.assertTrue(
            lines[0].startswith(
                f"storage-quorum-replicate: {REPO_ID} stored on the primary only; "
                "replication context unavailable (API unreachable"
            ),
            lines[0],
        )
        self.assertTrue(lines[0].endswith(" - the reconciler will retry"), lines[0])
        self.assertTrue(
            lines[1].startswith(
                f"storage-quorum-replicate: {REPO_ID} refs/heads/main not handed to pipelines (API unreachable"
            ),
            lines[1],
        )
        self.assertEqual("6", self.watermark())

    def test_on_a_node_that_is_not_the_primary_it_does_nothing(self) -> None:
        self.api.contexts = [(200, context(5, isPrimary=False))]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(0, result.returncode)
        self.assertEqual("", result.stderr)
        self.assertEqual([], self.api.calls("/quorum-replicate"))
        self.assertEqual([], self.api.calls("/git-push-ingest"))

    def test_an_api_that_cannot_build_artifacts_is_not_asked_to(self) -> None:
        legacy_context = context(5)
        del legacy_context["apiCreatesArtifacts"]
        self.api.contexts = [(200, legacy_context)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(1, result.returncode)
        lines = self.stderr_lines(result)
        self.assertEqual(1, len(lines), lines)
        self.assertIn("update the API before the storage nodes", lines[0])
        self.assertEqual([], self.api.calls("/quorum-replicate"))
        self.assertEqual("6", self.watermark())
        self.assertEqual(1, len(self.api.calls("/git-push-ingest")))

    def test_an_older_api_still_replicates_a_repository_without_encrypted_replicas(self) -> None:
        legacy_context = context(5, replicationState="Rf3Healthy")
        del legacy_context["apiCreatesArtifacts"]
        self.api.contexts = [(200, legacy_context)]

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(1, len(self.api.calls("/quorum-replicate")))

    def test_with_rollback_on_failure_nothing_is_ingested_after_a_failure(self) -> None:
        self.api.quorum_responses = [(409, {"success": False, "error": "Write quorum unavailable."})]

        result = self.run_script(
            "--post-receive",
            stdin=f"{SHA_A} {SHA_B} refs/heads/main\n",
            extra_env={"STORAGE_HOOK_ROLLBACK_ON_FAILURE": "1"},
        )

        self.assertEqual(1, result.returncode)
        self.assertEqual(1, len(self.stderr_lines(result)))
        self.assertEqual([], self.api.calls("/git-push-ingest"))

    def test_the_older_one_ref_arguments_still_work(self) -> None:
        result = self.run_script("refs/heads/main", SHA_A)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(1, len(self.api.calls("/quorum-replicate")))
        self.assertEqual(
            [{"repositoryId": REPO_ID, "ref": "refs/heads/main", "afterSha": SHA_A}],
            [call["body"] for call in self.api.calls("/git-push-ingest")],
        )

    def test_without_refs_it_only_replicates(self) -> None:
        result = self.run_script("", "")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(1, len(self.api.calls("/quorum-replicate")))
        self.assertEqual([], self.api.calls("/git-push-ingest"))

    def test_the_watermark_file_is_never_lowered(self) -> None:
        self.set_watermark(9)

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(6, self.api.calls("/quorum-replicate")[0]["body"]["appliedWatermark"])
        self.assertEqual("9", self.watermark())

    def test_a_ref_pipelines_did_not_take_is_reported_in_one_line(self) -> None:
        self.api.ingest_status = 500

        result = self.run_script("--post-receive", stdin=f"{ZERO} {SHA_A} refs/heads/main\n")

        self.assertEqual(0, result.returncode)
        self.assertEqual(
            [f"storage-quorum-replicate: {REPO_ID} refs/heads/main not handed to pipelines (HTTP 500)"],
            self.stderr_lines(result),
        )


if __name__ == "__main__":
    unittest.main()
