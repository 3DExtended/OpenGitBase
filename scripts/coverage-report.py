#!/usr/bin/env python3
"""Merge coverlet cobertura reports and print per-assembly line coverage."""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def main() -> int:
    coverage_dir = Path(sys.argv[1] if len(sys.argv) > 1 else "coverage")
    lines: dict[tuple[str, str, str], int] = defaultdict(int)

    for xml in coverage_dir.rglob("coverage.cobertura.xml"):
        root = ET.parse(xml).getroot()
        for pkg in root.findall(".//package"):
            pkg_name = pkg.get("name") or ""
            if not pkg_name.startswith("OpenGitBase") or pkg_name.endswith(".Tests"):
                continue
            for cls in pkg.findall("classes/class"):
                fn = (cls.get("filename") or "").replace("\\", "/")
                if "/Migrations/" in fn:
                    continue
                for line in cls.findall("lines/line"):
                    key = (pkg_name, fn, line.get("number") or "")
                    hits = int(line.get("hits", "0"))
                    lines[key] = max(lines[key], hits)

    pkg_stats: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for (pkg_name, _, _), hits in lines.items():
        pkg_stats[pkg_name][1] += 1
        if hits > 0:
            pkg_stats[pkg_name][0] += 1

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
