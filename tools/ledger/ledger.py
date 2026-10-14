"""The v5 parity ledger: validate it and render its Markdown views.

    python tools/ledger/ledger.py check    # exit 1 and list problems if anything is off
    python tools/ledger/ledger.py render   # rewrite docs/port/*.md from parity-ledger.json

parity-ledger.json is the source of truth. The two Markdown files are generated
views; editing them by hand is a check failure.
"""
from __future__ import annotations

import collections
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PORT = ROOT / "docs" / "port"

OWNERS = {
    "Engine.Template", "Engine.Document", "Engine.Render", "Engine.Fills", "Engine.Adjust",
    "Engine.WordArt", "Engine.Details", "Engine.Tools", "Engine.History", "Engine.ProjectIO",
    "Engine.Export", "App.Shell", "App.Ribbon.Home", "App.Ribbon.Layout", "App.Ribbon.Details",
    "App.Ribbon.Text", "App.Layers", "App.LayerProperties", "App.AssetLibrary", "App.Canvas",
    "App.Keyboard", "App.Dialogs", "App.Storage", "App.Clipboard", "Package", "Docs", "Parity",
    "Retired",
}
CHECKS = {"unit", "golden", "ui-auto", "manual", "doc", "none-retired"}
DISPOSITIONS = {"keep", "fix", "drop", "new"}
STATUSES = {"open", "done"}


def load(path: Path) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def cell(s) -> str:
    return " ".join(str(s).split()).replace("|", "\\|")


def render_ledger(data: dict) -> str:
    rows = data["rows"]
    by_owner = collections.defaultdict(list)
    for r in rows:
        by_owner[r["owner"]].append(r)
    disp = collections.Counter(r["disposition"] for r in rows)
    checks = collections.Counter(r["check"] for r in rows)
    status = collections.Counter(r["status"] for r in rows)
    out = [
        "# Parity ledger", "",
        "Generated from `parity-ledger.json` by `tools/ledger/ledger.py render` (do not edit by hand). "
        "Every user-visible behavior of RBX15 4.1, inventoried 2026-10-11, with the module that owns it in v5, "
        "how parity is proven, and whether v5 keeps it, fixes it, drops it, or adds it. "
        "**v5 does not ship with a row whose status is `open`.** Design: "
        "[native v5 port design](../superpowers/specs/2026-10-11-native-v5-port-design.md). "
        "Deliberate changes: [divergences](divergences.md).", "",
        f"**{len(rows)} rows.** Status: " + ", ".join(f"{k} {v}" for k, v in sorted(status.items())) +
        ". Disposition: " + ", ".join(f"{k} {v}" for k, v in disp.most_common()) +
        ". Checks: " + ", ".join(f"{k} {v}" for k, v in checks.most_common()) + ".", "",
    ]
    for owner in sorted(by_owner):
        out += [f"## {owner}", "", "| Status | ID | Behavior | Check | Disposition | Risk |",
                "|---|---|---|---|---|---|"]
        out += [f"| {r['status']} | `{r['id']}` | {cell(r['name'])} | {r['check']} | {r['disposition']} | {r['risk']} |"
                for r in by_owner[owner]]
        out.append("")
    return "\n".join(out)


def render_divergences(data: dict) -> str:
    rows = data["rows"]
    out = [
        "# Divergences from 4.1", "",
        "Every deliberate behavior change in v5, generated from `parity-ledger.json` rows whose disposition is "
        "`fix` or `drop`. The bug policy: fix anything clearly unintended, keep anything a user could reasonably "
        "rely on. A change that is not in this table is a regression. Rows with a visual effect get a new golden "
        "image with a note.", "",
    ]
    for kind, title in (("fix", "Fixed in v5"), ("drop", "Not carried into v5")):
        rs = sorted((r for r in rows if r["disposition"] == kind), key=lambda r: (r["owner"], r["id"]))
        out += [f"## {title} ({len(rs)})", "", "| ID | Owner | Change |", "|---|---|---|"]
        out += [f"| `{r['id']}` | {r['owner']} | {cell(r['divergence'])} |" for r in rs]
        out.append("")
    new = [r for r in rows if r["disposition"] == "new"]
    if new:
        out += [f"## New in v5 ({len(new)})", "", "| ID | Owner | Behavior |", "|---|---|---|"]
        out += [f"| `{r['id']}` | {r['owner']} | {cell(r['name'])} |" for r in new]
        out.append("")
    return "\n".join(out)


def problems(data: dict, md_dir: Path) -> list[str]:
    found: list[str] = []
    seen = collections.Counter(r["id"] for r in data["rows"])
    found += [f"duplicate id {i}" for i, n in seen.items() if n > 1]
    for r in data["rows"]:
        rid = r["id"]
        if r["owner"] not in OWNERS:
            found.append(f"{rid}: unknown owner {r['owner']!r}")
        if r["check"] not in CHECKS:
            found.append(f"{rid}: unknown check {r['check']!r}")
        if r["disposition"] not in DISPOSITIONS:
            found.append(f"{rid}: unknown disposition {r['disposition']!r}")
        if r["status"] not in STATUSES:
            found.append(f"{rid}: unknown status {r['status']!r}")
        if r["disposition"] in ("fix", "drop") and not str(r["divergence"]).startswith("4.1:"):
            found.append(f"{rid}: {r['disposition']} row needs a divergence sentence starting '4.1:'")
    md_dir = Path(md_dir)
    for name, text in (("parity-ledger.md", render_ledger(data)), ("divergences.md", render_divergences(data))):
        path = md_dir / name
        if not path.exists() or path.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            found.append(f"{name} is stale; run: python tools/ledger/ledger.py render")
    return found


def mark_done(data: dict, ids: list[str]) -> dict:
    known = {r["id"] for r in data["rows"]}
    unknown = [i for i in ids if i not in known]
    if unknown:
        raise KeyError(f"unknown ledger ids: {', '.join(unknown)}")
    wanted = set(ids)
    for r in data["rows"]:
        if r["id"] in wanted:
            r["status"] = "done"
    return data


def main(argv: list[str]) -> int:
    cmd = argv[1] if len(argv) > 1 else "check"
    data = load(PORT / "parity-ledger.json")
    if cmd == "done":
        try:
            mark_done(data, argv[2:])
        except KeyError as e:
            print(e.args[0])
            return 1
        (PORT / "parity-ledger.json").write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n",
                                                 encoding="utf-8", newline="\n")
        cmd = "render"
    if cmd == "render":
        (PORT / "parity-ledger.md").write_text(render_ledger(data), encoding="utf-8", newline="\n")
        (PORT / "divergences.md").write_text(render_divergences(data), encoding="utf-8", newline="\n")
        print("rendered docs/port/parity-ledger.md and docs/port/divergences.md")
        return 0
    if cmd == "check":
        found = problems(data, PORT)
        for p in found:
            print(p)
        print(f"{len(data['rows'])} rows, {len(found)} problem(s)")
        return 1 if found else 0
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
