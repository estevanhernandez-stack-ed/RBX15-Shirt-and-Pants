"""Pins what the CI and release workflows must keep doing.

Plain text parsing, no PyYAML: the workflows use one layout (two-space indents, block lists), and the point is to fail
loudly when a line that matters is edited away. Each check_* function raises AssertionError naming what is wrong; the
TestCase classes run them on the real files, then again on a deliberately broken copy to prove they can fail.

Run: python -m unittest discover -s tools/ci -v
"""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOWS = ROOT / ".github" / "workflows"


def read(name):
    return (WORKFLOWS / name).read_text(encoding="utf-8")


def trigger_list(text, event, key):
    """The items under `on: <event>: <key>:` (paths, branches or tags), or [] when absent."""
    lines = text.splitlines()
    if "on:" not in lines:
        raise AssertionError("no top-level `on:` block")
    j = lines.index("on:") + 1
    while j < len(lines) and (lines[j].startswith(" ") or not lines[j].strip()):
        if lines[j] == f"  {event}:":
            k = j + 1
            while k < len(lines) and (lines[k].startswith("    ") or not lines[k].strip()):
                if lines[k] == f"    {key}:":
                    items = []
                    m = k + 1
                    while m < len(lines) and re.match(r"^      - ", lines[m]):
                        items.append(lines[m][8:].strip().strip('"').strip("'"))
                        m += 1
                    return items
                k += 1
            return []
        j += 1
    return []


def trigger_keys(text, event):
    """The filter keys (branches, tags, paths ...) set under `on: <event>:`, in either list style."""
    lines = text.splitlines()
    out, inside = [], False
    for line in lines[lines.index("on:") + 1:]:
        if line and not line.startswith(" "):
            break
        if re.match(r"^  [a-z_]+:", line):
            inside = line.strip() == f"{event}:"
        elif inside and (m := re.match(r"^    ([a-z_-]+):", line)):
            out.append(m.group(1))
    return out


def events(text):
    lines = text.splitlines()
    out = []
    for line in lines[lines.index("on:") + 1:]:
        if line and not line.startswith(" "):
            break
        m = re.match(r"^  ([a-z_]+):", line)
        if m:
            out.append(m.group(1))
    return out


def steps(text):
    """Each step of the (single) job as its raw text, in order."""
    out, cur, in_steps = [], None, False
    for line in text.splitlines():
        if line == "    steps:":
            in_steps = True
            continue
        if not in_steps:
            continue
        if re.match(r"^      - ", line):
            cur = [line]
            out.append(cur)
        elif cur is not None and (line.startswith("        ") or not line.strip()):
            cur.append(line)
        elif line and not line.startswith(" "):
            break
    return ["\n".join(s) for s in out]


def step_index(text, needle):
    for i, s in enumerate(steps(text)):
        if needle in s:
            return i
    raise AssertionError(f"no step contains {needle!r}")


# ---- dotnet-ci.yml ---------------------------------------------------------------------------------------------------

REQUIRED_CI_PATHS = ["src/**", "packs/**", "tests/goldens/**", "tools/**"]
DOCS_ONLY_PATHS = ("docs/**", "**/*.md", "*.md", "**.md", "README.md", "windows/**", "**")


def check_dotnet_ci(text):
    for event in ("push", "pull_request"):
        paths = trigger_list(text, event, "paths")
        for p in REQUIRED_CI_PATHS:
            assert p in paths, f"dotnet-ci.yml {event} does not trigger on {p}"
        for p in DOCS_ONLY_PATHS:
            assert p not in paths, f"dotnet-ci.yml {event} triggers on the docs-only or catch-all path {p}"
        assert not any(p.endswith(".md") for p in paths), f"dotnet-ci.yml {event} triggers on a markdown path"
    assert "pull_request" in events(text), "dotnet-ci.yml does not run on pull requests"
    assert "python -m unittest discover -s tools/ci" in text, "dotnet-ci.yml does not run the tools/ci tests"
    assert step_index(text, "dotnet build") < step_index(text, "tools/ci"), "tools/ci tests must run after the build"


# ---- v5-release.yml --------------------------------------------------------------------------------------------------

def check_v5_release(text):
    assert events(text) == ["push"], f"v5-release.yml must trigger on push of tags only, found {events(text)}"
    tags = trigger_list(text, "push", "tags")
    assert tags and all(t.startswith("v5.") for t in tags), f"v5-release.yml tag patterns must all be v5.*, found {tags}"
    assert trigger_keys(text, "push") == ["tags"], "v5-release.yml must filter on tags alone (no branches, no paths)"
    assert "RELEASE-NOTES-PLACEHOLDER" in text and "throw" in text.split("RELEASE-NOTES-PLACEHOLDER", 1)[1].split("\n", 1)[0], \
        "v5-release.yml must refuse the placeholder release notes"
    assert "RBX15.App.csproj" in text, "v5-release.yml must check the tag against the csproj version"
    tag = step_index(text, "github.ref_name")
    ver = step_index(text, "RBX15.App.csproj")
    notes = step_index(text, "RELEASE-NOTES-PLACEHOLDER")
    ledger = step_index(text, "tools/ledger/ledger.py check")
    build = step_index(text, "dotnet build")
    test = step_index(text, "dotnet test")
    msix = step_index(text, "build-msix.ps1")
    assert tag < ver, "the csproj version check must come after the tag is parsed"
    assert ver < msix and notes < msix, "the version and notes checks must run before build-msix.ps1"
    assert ledger < msix, "the ledger gate must run before build-msix.ps1"
    assert build < test < msix, "dotnet build and dotnet test must run before build-msix.ps1"
    assert "-SelfSign" not in text, "the release must never self-sign"


# ---- windows-release.yml ---------------------------------------------------------------------------------------------

def check_windows_release(text):
    assert events(text) == ["push"], f"windows-release.yml must trigger on push of tags only, found {events(text)}"
    tags = trigger_list(text, "push", "tags")
    assert tags and all(t.startswith("v4.") for t in tags), f"windows-release.yml must be fenced to v4.*, found {tags}"
    assert trigger_keys(text, "push") == ["tags"], "windows-release.yml must filter on tags alone (no branches, no paths)"


class DotnetCiTests(unittest.TestCase):
    def test_real_file_passes(self):
        check_dotnet_ci(read("dotnet-ci.yml"))

    def test_every_asserted_line_is_load_bearing(self):
        text = read("dotnet-ci.yml")
        for p in REQUIRED_CI_PATHS:
            broken = text.replace(f'      - "{p}"\n', "")
            self.assertNotEqual(broken, text)
            with self.assertRaises(AssertionError, msg=p):
                check_dotnet_ci(broken)
        with self.assertRaises(AssertionError):
            check_dotnet_ci(text.replace('      - "tools/**"\n', '      - "tools/**"\n      - "docs/**"\n', 1))
        with self.assertRaises(AssertionError):
            check_dotnet_ci(text.replace('      - "tools/**"\n', '      - "tools/**"\n      - "**/*.md"\n', 1))
        with self.assertRaises(AssertionError):
            check_dotnet_ci(text.replace("python -m unittest discover -s tools/ci", "echo skipped"))
        with self.assertRaises(AssertionError):
            check_dotnet_ci(text.replace("  pull_request:\n", "  workflow_dispatch:\n"))


class V5ReleaseTests(unittest.TestCase):
    def test_real_file_passes(self):
        check_v5_release(read("v5-release.yml"))

    def test_every_asserted_line_is_load_bearing(self):
        text = read("v5-release.yml")
        cases = {
            "tag pattern": text.replace('      - "v5.*.*"', '      - "*"'),
            "branch trigger": text.replace("  push:\n    tags:", "  push:\n    branches: [main]\n    tags:"),
            "placeholder refusal": text.replace("RELEASE-NOTES-PLACEHOLDER", "SOMETHING-ELSE"),
            "csproj check": text.replace("RBX15.App.csproj", "Other.csproj"),
            "ledger gate": text.replace("tools/ledger/ledger.py check", "echo ledger"),
            "test before msix": text.replace("dotnet test src/RBX15.slnx -c Release --no-build", "echo no tests"),
            "build before msix": text.replace("dotnet build src/RBX15.slnx", "echo no build"),
            "self sign": text.replace("build-msix.ps1 -Version", "build-msix.ps1 -SelfSign -Version"),
        }
        for name, broken in cases.items():
            self.assertNotEqual(broken, text, name)
            with self.assertRaises(AssertionError, msg=name):
                check_v5_release(broken)

    def test_msix_step_moved_ahead_of_tests_fails(self):
        text = read("v5-release.yml")
        s = steps(text)
        a = next(i for i, x in enumerate(s) if "dotnet test" in x)
        b = next(i for i, x in enumerate(s) if "build-msix.ps1" in x)
        s[a], s[b] = s[b], s[a]
        head = text.split("    steps:\n", 1)[0]
        with self.assertRaises(AssertionError):
            check_v5_release(head + "    steps:\n" + "\n".join(s) + "\n")


class WindowsReleaseTests(unittest.TestCase):
    def test_real_file_passes(self):
        check_windows_release(read("windows-release.yml"))

    def test_the_v4_fence_is_load_bearing(self):
        text = read("windows-release.yml")
        for broken in (text.replace('"v4.*.*"', '"v*"'), text.replace('"v4.*.*"', '"v5.*.*"')):
            self.assertNotEqual(broken, text)
            with self.assertRaises(AssertionError):
                check_windows_release(broken)


if __name__ == "__main__":
    unittest.main()
