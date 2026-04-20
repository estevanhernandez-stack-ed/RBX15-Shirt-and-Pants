"""Generate Microsoft Store / social listing assets for ItsjustEste's RBX Maker.

Two brand marks end up in Partner Center:

  1. Store logo / publisher identity — the 626Labs company logo on a navy
     canvas. Used for the publisher mark in store listings.
  2. App tile / product identity — the RBX Maker app icon, rendered here
     programmatically since we don't have a standalone source PNG.

Store logos carry the publisher; app tiles carry this specific product.

Outputs written beside this script:

    logo-square-1080x1080.png   (publisher square)
    logo-portrait-720x1080.png  (publisher portrait)
    app-tile-71x71.png          (app small tile)
    app-tile-150x150.png        (app medium tile)
    app-tile-300x300.png        (app large tile)

Run:

    python make-store-assets.py
    python make-store-assets.py --company /path/to/626Labs-logo.png

Defaults:
  --company  ~/.claude/skills/626labs-design/assets/626Labs-logo.png
"""

import argparse
import pathlib
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
DEFAULT_COMPANY = pathlib.Path.home() / ".claude" / "skills" / "626labs-design" / "assets" / "626Labs-logo.png"

# Brand tokens — must match colors_and_type.css in the design skill.
NAVY_DEEP = (15, 31, 49, 255)      # --brand-navy-deep
NAVY      = (25, 46, 68, 255)      # --brand-navy
CYAN      = (23, 212, 250, 255)    # --brand-cyan
MAGENTA   = (242, 47, 137, 255)    # --brand-magenta
INK_0     = (255, 255, 255, 255)
INK_950   = (5, 12, 24, 255)


def find_font(target_size: int, *, black: bool = False) -> ImageFont.FreeTypeFont:
    """Return the best available sans font, in preference order."""
    black_candidates = [
        "C:/Windows/Fonts/seguibl.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/System/Library/Fonts/SFNS.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]
    bold_candidates = [
        "C:/Windows/Fonts/SegoeUIB.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/System/Library/Fonts/SFNS.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
    ]
    for p in (black_candidates if black else bold_candidates):
        if pathlib.Path(p).exists():
            return ImageFont.truetype(p, target_size)
    return ImageFont.load_default()


def render_gradient_text(draw_size: int, text: str, font: ImageFont.FreeTypeFont,
                         start_color: tuple, end_color: tuple) -> Image.Image:
    """Render text with a 135deg cyan→magenta gradient fill. Returns transparent RGBA."""
    # Render mask (white text on transparent, high-res for crisp edges)
    bbox = font.getbbox(text)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    mask = Image.new("L", (tw, th), 0)
    mdraw = ImageDraw.Draw(mask)
    mdraw.text((-bbox[0], -bbox[1]), text, font=font, fill=255)

    # Build a 135deg linear gradient at the text bbox size
    grad = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    gpx = grad.load()
    # 135deg = top-left → bottom-right. Project each pixel onto the axis.
    for y in range(th):
        for x in range(tw):
            t = (x + y) / max(1, tw + th - 2)
            r = int(start_color[0] + (end_color[0] - start_color[0]) * t)
            g = int(start_color[1] + (end_color[1] - start_color[1]) * t)
            b = int(start_color[2] + (end_color[2] - start_color[2]) * t)
            gpx[x, y] = (r, g, b, 255)

    # Use text mask as alpha channel on the gradient
    result = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    result.paste(grad, (0, 0), mask)
    return result


def draw_app_tile(size: int) -> Image.Image:
    """RBX Maker app tile — solid navy, gradient-text wordmark, tracked eyebrow.

    Respects the 626 Labs brand rules:
    - Solid navy baseline (no full-panel gradient background)
    - Brand gradient belongs to text / strokes / accents, not backgrounds
    - Inner hairline stroke for subtle dimension
    - Sentence-case eyebrow treatment with tracking
    """
    # Solid navy base
    img = Image.new("RGB", (size, size), NAVY_DEEP)
    img = img.convert("RGBA")
    draw = ImageDraw.Draw(img)

    # Single soft cyan glow in the top-left corner for depth (small + low)
    # Done with a single radial ellipse, not a layered loop, so it never
    # approaches full opacity.
    glow_r = int(size * 0.55)
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    for step in range(10):
        r = int(glow_r * (1 - step / 10))
        alpha = int(18 * (step / 10))
        gdraw.ellipse([-r // 3, -r // 3, r, r], fill=(CYAN[0], CYAN[1], CYAN[2], alpha))
    img = Image.alpha_composite(img, glow)

    # Inner hairline stroke — anchors composition without dominating
    draw = ImageDraw.Draw(img)
    inset = max(2, size // 24)
    stroke_w = max(1, size // 100)
    draw.rounded_rectangle(
        [(inset, inset), (size - inset - 1, size - inset - 1)],
        radius=max(6, size // 8),
        outline=(255, 255, 255, 30),
        width=stroke_w,
    )

    # Primary mark — "RBX" rendered with the brand gradient (cyan → magenta, 135deg)
    # Sized to take about 50% of tile width so it feels confident without crowding edges.
    mark_target_w = int(size * 0.62)
    # Binary-search a font size that hits that width
    lo, hi = int(size * 0.3), int(size * 0.75)
    best = lo
    while lo <= hi:
        mid = (lo + hi) // 2
        f = find_font(mid, black=True)
        bb = f.getbbox("RBX")
        w = bb[2] - bb[0]
        if w <= mark_target_w:
            best = mid
            lo = mid + 1
        else:
            hi = mid - 1
    font = find_font(best, black=True)
    text_img = render_gradient_text(size, "RBX", font, CYAN, MAGENTA)

    # Center RBX in upper-middle of tile (eyebrow will sit below)
    tx = (size - text_img.width) // 2
    ty = int(size * 0.24)
    img.alpha_composite(text_img, (tx, ty))

    # Eyebrow — "CLASSIC R15" in caps with letter-spacing, small, cyan-dim
    # 71x71 too small for eyebrow; show just on 150+
    if size >= 150:
        eyebrow_size = max(9, int(size * 0.075))
        efont = find_font(eyebrow_size, black=False)
        # Manual tracking via inserted spaces — Pillow doesn't do letter-spacing.
        eyebrow = "C L A S S I C   R 1 5"
        ebbox = efont.getbbox(eyebrow)
        ew = ebbox[2] - ebbox[0]
        eh = ebbox[3] - ebbox[1]
        ex = (size - ew) // 2 - ebbox[0]
        ey = ty + text_img.height + int(size * 0.05) - ebbox[1]
        draw.text((ex, ey), eyebrow, font=efont, fill=(CYAN[0], CYAN[1], CYAN[2], 220))

    return img.convert("RGB")


def center_on_navy(src: Image.Image, w: int, h: int, scale: float) -> Image.Image:
    canvas = Image.new("RGBA", (w, h), NAVY)
    target_h = int(h * scale)
    aspect = src.width / src.height
    target_w = int(target_h * aspect)
    max_w = int(w * 0.88)
    if target_w > max_w:
        target_w = max_w
        target_h = int(target_w / aspect)
    logo = src.resize((target_w, target_h), Image.LANCZOS)
    canvas.paste(logo, ((w - target_w) // 2, (h - target_h) // 2), logo)
    return canvas.convert("RGB")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--company", type=pathlib.Path, default=DEFAULT_COMPANY,
                    help=f"Path to the 626Labs company logo (default: {DEFAULT_COMPANY})")
    args = ap.parse_args()

    # --- Publisher logos: 626 Labs company identity -----------------------
    if args.company.exists():
        company = Image.open(args.company).convert("RGBA")
        print(f"company: {args.company.name} ({company.size[0]}x{company.size[1]})")

        square = center_on_navy(company, 1080, 1080, scale=0.78)
        square.save(HERE / "logo-square-1080x1080.png", "PNG")
        print("wrote logo-square-1080x1080.png")

        portrait = center_on_navy(company, 720, 1080, scale=0.62)
        portrait.save(HERE / "logo-portrait-720x1080.png", "PNG")
        print("wrote logo-portrait-720x1080.png")
    else:
        print(f"[skip] company logo not found at {args.company}. Install the 626labs-design skill or pass --company.")

    # --- App tiles: RBX Maker product identity ---------------------------
    for size in (71, 150, 300):
        tile = draw_app_tile(size)
        out = HERE / f"app-tile-{size}x{size}.png"
        tile.save(out, "PNG")
        print(f"wrote {out.name} ({size}x{size})")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
