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
from urllib.parse import parse_qs, urlparse
from storage_content import (
    GitContentError,
    get_blob,
    get_raw_bytes,
    is_empty_repository,
    list_branches,
    list_tags,
    list_tree,
    resolve_readme,
)

STORAGE_API_TOKEN = os.environ.get("STORAGE_API_TOKEN", "")
STORAGE_TOKEN_FILE = os.environ.get("STORAGE_TOKEN_FILE", "/var/lib/opengitbase/api-token")
STORAGE_HTTP_PORT = int(os.environ.get("STORAGE_HTTP_PORT", "8081"))
STORAGE_MTLS_GIT_HTTP_PORT = int(os.environ.get("STORAGE_MTLS_GIT_HTTP_PORT", "8443"))
WATERMARK_DIR = Path(os.environ.get("STORAGE_WATERMARK_DIR", "/var/lib/opengitbase/watermarks"))
REPOS_ROOT = Path("/srv/git")
CA_CERT = Path("/etc/opengitbase/ca.crt")
NODE_CERT = Path("/etc/opengitbase/node.crt")
NODE_KEY = Path("/etc/opengitbase/node.key")


def _is_valid_physical_path(physical_path: str) -> bool:
    if not physical_path:
        return False

    try:
        resolved = Path(physical_path).resolve()
        repos_root = REPOS_ROOT.resolve()
    except OSError:
        return False

    return resolved == repos_root or repos_root in resolved.parents


def _git_mtls_env() -> dict[str, str]:
    env = os.environ.copy()
    if CA_CERT.is_file():
        env["GIT_SSL_CAINFO"] = str(CA_CERT)
    if NODE_CERT.is_file():
        env["GIT_SSL_CERT"] = str(NODE_CERT)
    if NODE_KEY.is_file():
        env["GIT_SSL_KEY"] = str(NODE_KEY)
    return env


def _repository_id_from_path(physical_path: str) -> str:
    return Path(physical_path).name.removesuffix(".git")


def _write_initial_watermark(physical_path: str) -> None:
    WATERMARK_DIR.mkdir(parents=True, exist_ok=True)
    repo_id = _repository_id_from_path(physical_path)
    watermark_file = WATERMARK_DIR / f"{repo_id}.txt"
    if not watermark_file.is_file():
        watermark_file.write_text("0", encoding="utf-8")
    subprocess.run(
        ["chown", "-R", "git:git", str(WATERMARK_DIR)],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def _install_replication_hook(physical_path: str) -> None:
    hooks_dir = Path(physical_path) / "hooks"
    hooks_dir.mkdir(parents=True, exist_ok=True)
    hook_path = hooks_dir / "post-receive"
    hook_path.write_text(
        "#!/usr/bin/env bash\n"
        "set -euo pipefail\n"
        f'exec /usr/local/bin/storage-quorum-replicate.sh "{physical_path}"\n',
        encoding="utf-8",
    )
    hook_path.chmod(0o755)
    subprocess.run(
        ["chown", "-R", "git:git", str(hooks_dir)],
        check=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )


def sync_from_peer(
    physical_path: str,
    source_host: str,
    source_physical_path: str | None = None,
    source_port: int | None = None,
) -> None:
    if not _is_valid_physical_path(physical_path):
        raise ValueError("Invalid physicalPath.")

    if not os.path.isdir(physical_path):
        raise FileNotFoundError(f"Repository not found: {physical_path}")

    remote_path = source_physical_path or physical_path
    if not _is_valid_physical_path(remote_path):
        raise ValueError("Invalid sourcePhysicalPath.")

    port = source_port or STORAGE_MTLS_GIT_HTTP_PORT
    repo_name = Path(remote_path).name
    remote_url = f"https://{source_host}:{port}/{repo_name}"
    env = _git_mtls_env()
    git = ["git", "--git-dir", physical_path]

    subprocess.run(
        [*git, "fetch", remote_url, "+refs/*:refs/*"],
        env=env,
        check=True,
    )
    subprocess.run(
        [*git, "update-server-info"],
        env=env,
        check=True,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    _install_replication_hook(physical_path)
    _write_initial_watermark(physical_path)


def _backfill_replication_hooks() -> None:
    if not REPOS_ROOT.is_dir():
        return

    for repo_path in sorted(REPOS_ROOT.glob("*.git")):
        if repo_path.is_dir():
            _install_replication_hook(str(repo_path))
            _write_initial_watermark(str(repo_path))


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

        return self.rfile.read()

    def _read_json(self) -> dict[str, Any]:
        raw = self._read_body()
        if not raw:
            return {}
        return json.loads(raw.decode("utf-8"))

    def _query_params(self) -> dict[str, list[str]]:
        parsed = urlparse(self.path)
        return parse_qs(parsed.query)

    def _physical_path_from_query(self) -> str | None:
        params = self._query_params()
        values = params.get("physicalPath", [])
        return values[0] if values else None

    def _handle_content_error(self, exc: GitContentError) -> None:
        if exc.code in {"not_found", "invalid_ref"}:
            self._send_json(404, {"error": exc.message})
            return
        if exc.code == "invalid_path":
            self._send_json(400, {"error": exc.message})
            return
        self._send_json(500, {"error": exc.message})

    def do_GET(self) -> None:
        if not self._check_auth():
            self.send_error(401)
            return

        parsed = urlparse(self.path)
        if parsed.path == "/internal/repos/content/branches":
            self._handle_list_branches()
            return
        if parsed.path == "/internal/repos/content/tags":
            self._handle_list_tags()
            return
        if parsed.path == "/internal/repos/content/tree":
            self._handle_list_tree()
            return
        if parsed.path == "/internal/repos/content/blob":
            self._handle_get_blob()
            return
        if parsed.path == "/internal/repos/content/blob/raw":
            self._handle_get_blob_raw()
            return
        if parsed.path == "/internal/repos/content/readme":
            self._handle_get_readme()
            return
        if parsed.path == "/internal/repos/content/empty":
            self._handle_is_empty()
            return

        self.send_error(404)

    def _handle_list_branches(self) -> None:
        physical_path = self._physical_path_from_query()
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            branches = list_branches(physical_path)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        self._send_json(200, {"branches": branches})

    def _handle_list_tags(self) -> None:
        physical_path = self._physical_path_from_query()
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            tags = list_tags(physical_path)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        self._send_json(200, {"tags": tags})

    def _handle_list_tree(self) -> None:
        physical_path = self._physical_path_from_query()
        params = self._query_params()
        ref = params.get("ref", [""])[0]
        path = params.get("path", [""])[0]
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not ref:
            self._send_json(400, {"error": "ref is required."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            payload = list_tree(physical_path, ref, path)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        self._send_json(200, payload)

    def _handle_get_blob(self) -> None:
        physical_path = self._physical_path_from_query()
        params = self._query_params()
        ref = params.get("ref", [""])[0]
        path = params.get("path", [""])[0]
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not ref or not path:
            self._send_json(400, {"error": "ref and path are required."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            payload = get_blob(physical_path, ref, path)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        self._send_json(200, payload)

    def _handle_get_blob_raw(self) -> None:
        physical_path = self._physical_path_from_query()
        params = self._query_params()
        ref = params.get("ref", [""])[0]
        path = params.get("path", [""])[0]
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not ref or not path:
            self._send_json(400, {"error": "ref and path are required."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            raw, normalized_path = get_raw_bytes(physical_path, ref, path)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        self.send_response(200)
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Content-Length", str(len(raw)))
        self.send_header(
            "Content-Disposition",
            f'attachment; filename="{Path(normalized_path).name}"',
        )
        self.end_headers()
        self.wfile.write(raw)

    def _handle_get_readme(self) -> None:
        physical_path = self._physical_path_from_query()
        params = self._query_params()
        ref = params.get("ref", [""])[0]
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not ref:
            self._send_json(400, {"error": "ref is required."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        try:
            payload = resolve_readme(physical_path, ref)
        except GitContentError as exc:
            self._handle_content_error(exc)
            return
        if payload is None:
            self._send_json(404, {"error": "README not found."})
            return
        self._send_json(200, payload)

    def _handle_is_empty(self) -> None:
        physical_path = self._physical_path_from_query()
        if not physical_path or not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return
        if not os.path.isdir(physical_path):
            self._send_json(404, {"error": "Repository not found."})
            return
        self._send_json(200, {"isEmpty": is_empty_repository(physical_path)})

    def do_POST(self) -> None:
        if not self._check_auth():
            self.send_error(401)
            return

        if self.path == "/internal/repos/sync-from":
            self._handle_sync_from()
            return

        if self.path != "/internal/repos":
            self.send_error(404)
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
        _write_initial_watermark(physical_path)
        _install_replication_hook(physical_path)
        self._send_json(201, {"physicalPath": physical_path})

    def _handle_sync_from(self) -> None:
        data = self._read_json()
        physical_path = data.get("physicalPath", "")
        source_host = data.get("sourceHost", "")
        source_physical_path = data.get("sourcePhysicalPath")
        source_port = data.get("sourcePort")

        if not source_host:
            self._send_json(400, {"error": "sourceHost is required."})
            return

        if not _is_valid_physical_path(physical_path):
            self._send_json(400, {"error": "Invalid physicalPath."})
            return

        if source_physical_path and not _is_valid_physical_path(source_physical_path):
            self._send_json(400, {"error": "Invalid sourcePhysicalPath."})
            return

        try:
            sync_from_peer(
                physical_path,
                source_host,
                source_physical_path,
                int(source_port) if source_port is not None else None,
            )
        except FileNotFoundError as exc:
            self._send_json(404, {"error": str(exc)})
            return
        except subprocess.CalledProcessError as exc:
            self._send_json(502, {"error": f"git fetch failed with exit code {exc.returncode}."})
            return
        except ValueError as exc:
            self._send_json(400, {"error": str(exc)})
            return

        self._send_json(200, {"physicalPath": physical_path, "sourceHost": source_host})

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

    _backfill_replication_hooks()
    server = ThreadingHTTPServer(("0.0.0.0", STORAGE_HTTP_PORT), StorageHttpHandler)
    print(f"storage-http: listening on 0.0.0.0:{STORAGE_HTTP_PORT}")
    server.serve_forever()


if __name__ == "__main__":
    main()
