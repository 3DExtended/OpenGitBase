#!/usr/bin/env python3
"""Publish local docs/prd, docs/adr, and docs/issues mirror files as forge Discussions."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path

FORGE_RE = re.compile(r"^\s*<!--\s*forge:\s*#(\d+)\s*-->\s*$", re.IGNORECASE)
H1_RE = re.compile(r"^#\s+(.+)\s*$")
ID_RE = re.compile(r"^- ID:\s*(.+)\s*$", re.MULTILINE)
PARENT_LINK_RE = re.compile(
    r"^\[PRD:[^\]]+\]\((?P<path>\.\./\.\./prd/[^)]+)\)\s*$", re.MULTILINE
)
ADR_H1_RE = re.compile(r"^#\s+ADR\s+(\d{4}):\s*(.+)\s*$", re.IGNORECASE)
PRD_H1_RE = re.compile(r"^#\s+PRD:\s*(.+)\s*$", re.IGNORECASE)
MAX_CREATE_BODY = 7900
MAX_COMMENT_BODY = 15900


@dataclass
class DocFile:
    path: Path
    rel_path: str
    title: str
    body: str
    forge_number: int | None
    parent_rel: str | None


def normalize_host(host: str) -> str:
    trimmed = host.strip().rstrip("/")
    if not trimmed.startswith(("http://", "https://")):
        trimmed = "https://" + trimmed
    return trimmed


def keychain_token(host: str) -> str | None:
    if sys.platform != "darwin":
        return None
    service = f"opengitbase-cli/{normalize_host(host)}"
    try:
        result = subprocess.run(
            ["security", "find-generic-password", "-s", service, "-w"],
            check=True,
            capture_output=True,
            text=True,
        )
        token = result.stdout.strip()
        return token or None
    except subprocess.CalledProcessError:
        return None


def resolve_token(host: str) -> str:
    token = os.environ.get("OGB_TOKEN", "").strip()
    if token:
        return token

    token = keychain_token(host)
    if token:
        return token

    username = os.environ.get("OGB_USERNAME", "").strip()
    password = os.environ.get("OGB_PASSWORD", "").strip()
    if username and password:
        return login(host, username, password)

    raise SystemExit(
        "Not authenticated. Run:\n"
        f"  ogb --hostname {host} auth login\n"
        "or set OGB_TOKEN, or OGB_USERNAME + OGB_PASSWORD."
    )


def login(host: str, username: str, password: str) -> str:
    api_base = normalize_host(host) + "/api"
    payload = json.dumps({"username": username, "password": password}).encode()
    request = urllib.request.Request(
        f"{api_base}/signin/login",
        data=payload,
        headers={"Content-Type": "application/json", "User-Agent": "OpenGitBase-CLI/1.0"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request) as response:
            token = response.read().decode().strip().strip('"')
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode(errors="replace")
        raise SystemExit(f"Login failed ({exc.code}): {detail}") from exc

    if not token or token.startswith("{"):
        raise SystemExit("Login failed: empty or invalid token response.")
    return token


class ApiClient:
    def __init__(self, host: str, owner: str, slug: str, token: str) -> None:
        self.base = normalize_host(host) + "/api"
        self.discussions_path = (
            f"repository/by-slug/{owner}/{slug}/discussions"
        )
        self.token = token

    def _request(self, method: str, path: str, body: dict | None = None) -> object:
        data = None if body is None else json.dumps(body).encode()
        request = urllib.request.Request(
            f"{self.base}/{path.lstrip('/')}",
            data=data,
            headers={
                "Authorization": f"Bearer {self.token}",
                "Content-Type": "application/json",
                "Accept": "application/json",
                "User-Agent": "OpenGitBase-CLI/1.0",
            },
            method=method,
        )
        try:
            with urllib.request.urlopen(request) as response:
                raw = response.read().decode()
                if not raw:
                    return None
                return json.loads(raw)
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode(errors="replace")
            raise RuntimeError(f"{method} {path} failed ({exc.code}): {detail}") from exc

    def list_discussions(self) -> list[dict]:
        result = self._request("GET", self.discussions_path)
        return result if isinstance(result, list) else []

    def create_discussion(self, title: str, body: str) -> dict:
        result = self._request(
            "POST",
            self.discussions_path,
            {"title": title, "body": body},
        )
        if not isinstance(result, dict):
            raise RuntimeError(f"Unexpected create response for {title!r}")
        return result

    def add_comment(self, number: int, body_markdown: str) -> None:
        self._request(
            "POST",
            f"{self.discussions_path}/{number}/comments",
            {"bodyMarkdown": body_markdown},
        )

    def link_parent(self, child_number: int, parent_number: int) -> None:
        self._request(
            "POST",
            f"{self.discussions_path}/{child_number}/links",
            {
                "targetDiscussionNumber": parent_number,
                "relationshipType": "parent",
            },
        )


def strip_forge_header(content: str) -> tuple[int | None, str]:
    lines = content.splitlines()
    if not lines:
        return None, content
    match = FORGE_RE.match(lines[0])
    if not match:
        return None, content
    body = "\n".join(lines[1:]).lstrip("\n")
    return int(match.group(1)), body


def chunk_body(body: str, max_size: int) -> list[str]:
    if len(body) <= max_size:
        return [body]
    chunks: list[str] = []
    start = 0
    while start < len(body):
        end = min(start + max_size, len(body))
        if end < len(body):
            split_at = body.rfind("\n\n", start, end)
            if split_at <= start:
                split_at = body.rfind("\n", start, end)
            if split_at <= start:
                split_at = end
            end = split_at
        chunks.append(body[start:end].rstrip())
        start = end
        while start < len(body) and body[start] == "\n":
            start += 1
    return [chunk for chunk in chunks if chunk]


def derive_title(rel_path: str, body: str) -> str:
    first_line = body.splitlines()[0] if body.splitlines() else rel_path

    if rel_path.startswith("docs/prd/"):
        match = PRD_H1_RE.match(first_line)
        remainder = match.group(1).strip() if match else Path(rel_path).stem.replace("-", " ")
        return f"[PRD] {remainder}"

    if rel_path.startswith("docs/adr/"):
        match = ADR_H1_RE.match(first_line)
        if match:
            return f"[ADR] {match.group(1)} — {match.group(2).strip()}"
        stem = Path(rel_path).stem
        parts = stem.split("-", 1)
        number = parts[0] if parts else "0000"
        slug = parts[1].replace("-", " ") if len(parts) > 1 else stem
        return f"[ADR] {number} — {slug.title()}"

    if rel_path.startswith("docs/issues/"):
        id_match = ID_RE.search(body)
        slice_id = id_match.group(1).strip() if id_match else Path(rel_path).stem
        h1_match = H1_RE.match(first_line)
        summary = h1_match.group(1).strip() if h1_match else Path(rel_path).stem.replace("-", " ")
        return f"[slice] {slice_id} — {summary}"

    raise ValueError(f"Unsupported doc path: {rel_path}")


def parse_parent_rel(body: str) -> str | None:
    match = PARENT_LINK_RE.search(body)
    if not match:
        return None
    return "docs/" + match.group("path").removeprefix("../../")


def collect_docs(root: Path) -> list[DocFile]:
    patterns = [
        root / "docs" / "prd" / "*.md",
        root / "docs" / "adr" / "*.md",
        root / "docs" / "issues" / "**" / "*.md",
    ]
    docs: list[DocFile] = []
    for pattern in patterns:
        for path in sorted(root.glob(str(pattern.relative_to(root)))):
            if path.name == "README.md":
                continue
            rel_path = path.relative_to(root).as_posix()
            raw = path.read_text(encoding="utf-8")
            forge_number, body = strip_forge_header(raw)
            title = derive_title(rel_path, body)
            docs.append(
                DocFile(
                    path=path,
                    rel_path=rel_path,
                    title=title,
                    body=body.strip() + "\n",
                    forge_number=forge_number,
                    parent_rel=parse_parent_rel(body),
                )
            )
    return docs


def split_for_create_and_comments(body: str) -> tuple[str, list[str]]:
    create_chunks = chunk_body(body, MAX_CREATE_BODY)
    first = create_chunks[0]
    consumed = len(first)
    while consumed < len(body) and body[consumed] == "\n":
        consumed += 1
    remainder = body[consumed:]
    comment_chunks = chunk_body(remainder, MAX_COMMENT_BODY) if remainder else []
    return first, comment_chunks


def publish_doc(client: ApiClient, doc: DocFile, dry_run: bool) -> int:
    first, comment_chunks = split_for_create_and_comments(doc.body)
    if dry_run:
        print(
            f"[dry-run] create {doc.title!r} "
            f"(create={len(first)} chars, +{len(comment_chunks)} comment chunk(s))"
        )
        return doc.forge_number or -1

    created = client.create_discussion(doc.title, first)
    number = int(created["number"])
    for chunk in comment_chunks:
        client.add_comment(number, chunk)
        time.sleep(0.05)
    print(f"created #{number} {doc.title}")
    return number


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default=os.environ.get("API_URL", "https://api.opengitbase.com"))
    parser.add_argument("--owner", default=os.environ.get("OWNER", "opengitbase"))
    parser.add_argument("--repo", default=os.environ.get("REPO_SLUG", "open-git-base"))
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--write-back", action="store_true", help="Write <!-- forge: #N --> headers to source files")
    args = parser.parse_args()

    token = resolve_token(args.host) if not args.dry_run else os.environ.get("OGB_TOKEN", "").strip()
    client = None
    existing_by_title: dict[str, int] = {}
    if args.dry_run and not token:
        print("dry-run: skipping auth and API calls")
    else:
        if not token:
            token = resolve_token(args.host)
        client = ApiClient(args.host, args.owner, args.repo, token)
        existing_by_title = {
            item["title"]: int(item["number"])
            for item in client.list_discussions()
            if isinstance(item.get("title"), str)
        }
    docs = collect_docs(args.root.resolve())
    path_to_number: dict[str, int] = {}

    prds = [doc for doc in docs if doc.rel_path.startswith("docs/prd/")]
    adrs = [doc for doc in docs if doc.rel_path.startswith("docs/adr/")]
    slices = [doc for doc in docs if doc.rel_path.startswith("docs/issues/")]

    for group_name, group in ("PRD", prds), ("ADR", adrs), ("slice", slices):
        print(f"==> {group_name}: {len(group)} file(s)")
        for doc in group:
            if doc.forge_number and doc.forge_number in existing_by_title.values():
                number = doc.forge_number
                print(f"skip (forge header) #{number} {doc.title}")
            elif doc.title in existing_by_title:
                number = existing_by_title[doc.title]
                print(f"skip (title exists) #{number} {doc.title}")
            else:
                if client is None:
                    print(f"[dry-run] create {doc.title!r}")
                    number = -1
                else:
                    number = publish_doc(client, doc, args.dry_run)
                if not args.dry_run and number > 0:
                    existing_by_title[doc.title] = number
                    time.sleep(0.1)
            path_to_number[doc.rel_path] = number

    print("==> linking slice parents")
    for doc in slices:
        child = path_to_number.get(doc.rel_path)
        if not child or child < 1 or not doc.parent_rel:
            continue
        parent = path_to_number.get(doc.parent_rel)
        if not parent or parent < 1:
            print(f"warn: no parent for {doc.rel_path} -> {doc.parent_rel}")
            continue
        if args.dry_run or client is None:
            print(f"[dry-run] link #{child} parent #{parent}")
            continue
        try:
            client.link_parent(child, parent)
            print(f"linked #{child} -> parent #{parent}")
        except RuntimeError as exc:
            if "409" in str(exc) or "already" in str(exc).lower():
                print(f"skip link #{child} -> #{parent} (exists)")
            else:
                raise
        time.sleep(0.05)

    if args.write_back and not args.dry_run:
        for doc in docs:
            number = path_to_number.get(doc.rel_path)
            if not number or number < 1:
                continue
            raw = doc.path.read_text(encoding="utf-8")
            _, body = strip_forge_header(raw)
            updated = f"<!-- forge: #{number} -->\n\n{body.lstrip()}"
            if updated != raw:
                doc.path.write_text(updated, encoding="utf-8")
                print(f"updated header {doc.rel_path} -> #{number}")


if __name__ == "__main__":
    main()
