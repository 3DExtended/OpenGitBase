#!/usr/bin/env python3
"""Merge coverlet cobertura reports and print per-assembly line coverage."""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

PATH_PREFIXES = (
    "Developer/projects/opengitbase/",
    "/Users/peteresser/Developer/projects/opengitbase/",
)

ASSEMBLY_PREFIXES = {
    "OpenGitBase.Cqrs/": "OpenGitBase.Cqrs",
    "OpenGitBase.Cqrs.EfCore/": "OpenGitBase.Cqrs.EfCore",
    "OpenGitBase.Common/": "OpenGitBase.Common",
    "OpenGitBase.Common.SendGrid/": "OpenGitBase.Common.SendGrid",
    "OpenGitBase.Api/": "OpenGitBase.Api",
    "OpenGitBase.Features.Organization/": "OpenGitBase.Features.Organization",
    "OpenGitBase.Features.Organization.Contracts/": "OpenGitBase.Features.Organization.Contracts",
    "OpenGitBase.Features.PublicGitSshKey/": "OpenGitBase.Features.PublicGitSshKey",
    "OpenGitBase.Features.PublicGitSshKey.Contracts/": "OpenGitBase.Features.PublicGitSshKey.Contracts",
    "OpenGitBase.Features.Repository/": "OpenGitBase.Features.Repository",
    "OpenGitBase.Features.Repository.Contracts/": "OpenGitBase.Features.Repository.Contracts",
    "OpenGitBase.Features.RepositoryMember/": "OpenGitBase.Features.RepositoryMember",
    "OpenGitBase.Features.RepositoryMember.Contracts/": "OpenGitBase.Features.RepositoryMember.Contracts",
    "OpenGitBase.Features.Users/": "OpenGitBase.Features.Users",
    "OpenGitBase.Features.Users.Contracts/": "OpenGitBase.Features.Users.Contracts",
}


def normalize_filename(filename: str) -> str:
    normalized = filename.replace("\\", "/")
    for prefix in PATH_PREFIXES:
        if normalized.startswith(prefix):
            normalized = normalized[len(prefix) :]
            break
    return normalized


def assembly_name(filename: str) -> str | None:
    normalized = normalize_filename(filename)
    if "/Migrations/" in normalized or ".Tests/" in normalized:
        return None

    for prefix, assembly in ASSEMBLY_PREFIXES.items():
        if normalized.startswith(("common/", "features/", "applications/")):
            parts = normalized.split("/")
            if parts[0] == "common" and len(parts) >= 2:
                return f"OpenGitBase.{parts[1].removeprefix('OpenGitBase.')}"
            if parts[0] == "features" and len(parts) >= 3:
                return f"OpenGitBase.{parts[2].removeprefix('OpenGitBase.')}"
            if parts[0] == "applications" and len(parts) >= 2:
                return f"OpenGitBase.{parts[1].removeprefix('OpenGitBase.')}"
        if normalized.startswith(prefix):
            return assembly

    return None


def main() -> int:
    coverage_dir = Path(sys.argv[1] if len(sys.argv) > 1 else "coverage")
    line_hits: dict[tuple[str, str], int] = {}

    for xml in coverage_dir.rglob("coverage.cobertura.xml"):
        root = ET.parse(xml).getroot()
        for cls in root.findall(".//classes/class"):
            filename = cls.get("filename") or ""
            assembly = assembly_name(filename)
            if assembly is None:
                continue

            for line in cls.findall("lines/line"):
                line_number = line.get("number") or ""
                key = (assembly, line_number)
                hits = int(line.get("hits", "0"))
                line_hits[key] = max(line_hits.get(key, 0), hits)

    pkg_stats: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for (assembly, _), hits in line_hits.items():
        pkg_stats[assembly][1] += 1
        if hits > 0:
            pkg_stats[assembly][0] += 1

    total_c = total_v = 0
    print("Production assembly line coverage (merged, excl. migrations):")
    for name in sorted(pkg_stats):
        covered, valid = pkg_stats[name]
        pct = 100 * covered / valid if valid else 0
        total_c += covered
        total_v += valid
        flag = "" if pct >= 99.5 else "  <-- gap"
        print(f"  {pct:5.1f}%  {name} ({covered}/{valid}){flag}")

    overall = 100 * total_c / total_v if total_v else 0
    print(f"\nOverall: {overall:.1f}% ({total_c}/{total_v})")
    return 0 if overall >= 99.5 else 1


if __name__ == "__main__":
    raise SystemExit(main())
