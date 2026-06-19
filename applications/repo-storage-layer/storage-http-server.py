#!/usr/bin/env python3
# Copyright (c) 2026 OpenGitBase Authors
# SPDX-License-Identifier: LicenseRef-OpenGitBase-1.0
"""Internal HTTP API for bare repository lifecycle on storage nodes."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

STORAGE_API_TOKEN = os.environ.get("STORAGE_API_TOKEN", "")
STORAGE_TOKEN_FILE = os.environ.get("STORAGE_TOKEN_FILE", "/var/lib/opengitbase/api-token")
STORAGE_HTTP_PORT = int(os.environ.get("STORAGE_HTTP_PORT", "8081"))
REPOS_ROOT = Path("/srv/git")


def _is_valid_physical_path(physical_path: str) -> bool:
    if not physical_path:
        return False

    try:
        resolved = Path(physical_path).resolve()
        repos_root = REPOS_ROOT.resolve()
    except OSError:
        return False

    return resolved == repos_root or repos_root in resolved.parents


class StorageHttpHandler(BaseHTTPRequestHandler):
    server_version = "OpenGitBaseStorage/1.0"

    def log_message(self, format: str, *args: Any) -> None:
        print(f"storage-http: {self.address_string()} - {format % args}")

    def _send_json(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _get_api_token(self) -> str:
        try:
            token = Path(STORAGE_TOKEN_FILE).read_text(encoding="utf-8").strip()
            if token:
                return token
        except OSError:
            pass
        return STORAGE_API_TOKEN

    def _check_auth(self) -> bool:
        auth = self.headers.get("Authorization", "")
        if not auth.startswith("Bearer "):
            return False
        token = auth[7:].strip()
        expected = self._get_api_token()
        return bool(expected) and token == expected

    def _read_body(self) -> bytes:
        content_length = self.headers.get("Content-Length")
        if content_length is not None:
            length = int(content_length)
            if length > 0:
                return self.rfile.read(length)

        # HttpClient may send chunked bodies without Content-Length.
        return self.rfile.read()

    def _read_json(self) -> dict[str, Any]:
        raw = self._read_body()
        if not raw:
            return {}
        return json.loads(raw.decode("utf-8"))

    def do_POST(self) -> None:
        if self.path != "/internal/repos":
            self.send_error(404)
            return

        if not self._check_auth():
            self.send_error(401)
            return

        data = self._read_json()
        physical_path = data.get("physicalPath", "")
        if not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return

        if os.path.exists(physical_path):
            self._send_json(409, {"error": "Repository already exists."})
            return

        os.makedirs(os.path.dirname(physical_path), exist_ok=True)
        subprocess.run(
            ["git", "init", "--bare", "--initial-branch=main", physical_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        subprocess.run(
            ["chown", "-R", "git:git", physical_path],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        receive_max_bytes = int(data.get("receiveMaxBytes") or 0)
        git_config_env = {**os.environ, "GIT_DIR": physical_path}
        subprocess.run(
            ["git", "config", "http.receivepack", "true"],
            env=git_config_env,
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )
        if receive_max_bytes > 0:
            subprocess.run(
                ["git", "config", "receive.maxSize", str(receive_max_bytes)],
                env=git_config_env,
                check=True,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        self._send_json(201, {"physicalPath": physical_path})

    def do_DELETE(self) -> None:
        if self.path != "/internal/repos":
            self.send_error(404)
            return

        if not self._check_auth():
            self.send_error(401)
            return

        data = self._read_json()
        physical_path = data.get("physicalPath", "")
        if not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return

        if not os.path.exists(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return

        shutil.rmtree(physical_path)
        self._send_json(200, {"physicalPath": physical_path})


def main() -> None:
    if not STORAGE_API_TOKEN and not Path(STORAGE_TOKEN_FILE).is_file():
        raise SystemExit("STORAGE_API_TOKEN is required")

    server = ThreadingHTTPServer(("0.0.0.0", STORAGE_HTTP_PORT), StorageHttpHandler)
    print(f"storage-http: listening on 0.0.0.0:{STORAGE_HTTP_PORT}")
    server.serve_forever()


if __name__ == "__main__":
    main()
