"""Generate src/RBX15.App/app.ico from the committed MSIX tile art.

Source: windows/msix/Images/Square310x310Logo.png (the brand tile: navy base, template-cross glyph, RBX15 wordmark).
Sizes 256, 128, 64, 48 use the whole tile. Sizes 32, 24, 16 use a square crop of the glyph (the wordmark is
illegible below 48 px), so the taskbar and title-bar icons stay readable. Every entry is a PNG-encoded ICO image.

Run from anywhere: python windows/msix/make-app-icon.py
The .ico is committed; re-run only when the tile art changes.
"""
import io
import struct
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "windows" / "msix" / "Images" / "Square310x310Logo.png"
OUT = ROOT / "src" / "RBX15.App" / "app.ico"

FULL_SIZES = [256, 128, 64, 48]
GLYPH_SIZES = [32, 24, 16]


def glyph_crop(tile: Image.Image) -> Image.Image:
    # Square crop centred on the glyph (cross spans about x 52..255, y 60..195; wordmark starts near y 235).
    cx, cy, half = 153, 128, 104
    return tile.crop((cx - half, cy - half, cx + half, cy + half))


def main() -> None:
    tile = Image.open(SRC).convert("RGBA")
    entries = []
    for size in FULL_SIZES:
        entries.append((size, tile.resize((size, size), Image.LANCZOS)))
    glyph = glyph_crop(tile)
    for size in GLYPH_SIZES:
        entries.append((size, glyph.resize((size, size), Image.LANCZOS)))
    entries.sort(key=lambda e: e[0])

    blobs = []
    for size, img in entries:
        buf = io.BytesIO()
        img.save(buf, format="PNG", optimize=True)
        blobs.append((size, buf.getvalue()))

    header = struct.pack("<HHH", 0, 1, len(blobs))
    offset = 6 + 16 * len(blobs)
    directory = b""
    for size, data in blobs:
        dim = 0 if size >= 256 else size
        directory += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    OUT.write_bytes(header + directory + b"".join(d for _, d in blobs))
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes, sizes {[s for s, _ in blobs]})")


if __name__ == "__main__":
    main()
