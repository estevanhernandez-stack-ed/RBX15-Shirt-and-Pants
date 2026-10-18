"""Freezes the 15-font Word Art capture (M4b item 8, decision W1) into tests/goldens/app/wordart-fonts.

The controller runs wordart-ref.js twice over tools/parity-ref/wordart-fonts-scenes.json, then this script
from the repo root:

  for run in a b; do
    env -u ELECTRON_RUN_AS_NODE ./node_modules/electron/dist/electron.exe tools/parity-ref/wordart-ref.js \
      --scenes tools/parity-ref/wordart-fonts-scenes.json --out .superpowers/m4b-measure/wordart-fonts-$run 2>&1 | grep -E "^ok|Error"
  done
  python tools/parity-ref/freeze-wordart-fonts.py

Expected: "ok 15" twice, then "reproducible: 15", then "froze 15 bitmaps ...". It refuses to overwrite
tests/goldens/app/wordart-fonts (frozen goldens are never recaptured), and refuses a capture whose Electron,
editor.js, editor.html or font manifest differ from the app goldens' index.json.
"""
import hashlib, json, pathlib, shutil, subprocess

a = pathlib.Path(".superpowers/m4b-measure/wordart-fonts-a")
b = pathlib.Path(".superpowers/m4b-measure/wordart-fonts-b")
out = pathlib.Path("tests/goldens/app/wordart-fonts")
scenes_file = pathlib.Path("tools/parity-ref/wordart-fonts-scenes.json")
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

expected = sorted(s["name"] for s in json.loads(scenes_file.read_text(encoding="utf-8")))
names = sorted(p.stem for p in a.glob("*.png"))
assert len(expected) == 15, "wordart-fonts-scenes.json must hold the 15 fonts"
assert names == expected and names == sorted(p.stem for p in b.glob("*.png")), "each run must render exactly the 15 font scenes"
bad = [n for n in names if sha(a / f"{n}.png") != sha(b / f"{n}.png")]
assert not bad, f"MISMATCH {bad}: 4.1's Word Art bitmaps are not deterministic; never freeze them"
assert json.loads((a / "metrics.json").read_text()) == json.loads((b / "metrics.json").read_text()), "metrics differ between runs"
print(f"reproducible: {len(names)}")
assert not out.exists(), f"{out} already exists: the fixtures are frozen, never recaptured"

golden = json.load(open("tests/goldens/app/index.json", encoding="utf-8"))
blob = lambda f: subprocess.run(["git", "hash-object", f], capture_output=True, text=True, check=True).stdout.strip()
index = {
    "tool": "tools/parity-ref/wordart-ref.js",
    "scenesFile": "tools/parity-ref/wordart-fonts-scenes.json",
    "electron": json.load(open("node_modules/electron/package.json", encoding="utf-8"))["version"],
    "editorJsBlob": blob("editor.js"),
    "editorHtmlBlob": blob("editor.html"),
    "fontsManifestSha256": sha(pathlib.Path("assets/fonts/README.md")),
    "scenes": [{"name": n, "sha256": sha(a / f"{n}.png")} for n in names],
}
for k in ("electron", "editorJsBlob", "editorHtmlBlob", "fontsManifestSha256"):
    assert index[k] == golden[k], f"{k} differs from the app goldens' index: {index[k]} vs {golden[k]}"

out.mkdir()
for n in names:
    shutil.copyfile(a / f"{n}.png", out / f"{n}.png")
shutil.copyfile(a / "metrics.json", out / "metrics.json")
(out / "index.json").write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8", newline="\n")
print(f"froze {len(names)} bitmaps, metrics.json and index.json in {out.as_posix()}")
