"""Generate Microsoft Store / social listing assets for ItsjustEste's RBX15 Maker.

Two brand marks end up in Partner Center:

  1. Store logo / publisher identity — the 626Labs company logo on a navy
     canvas. Used for the publisher mark in store listings.
  2. App tile / product identity — the RBX15 Maker app icon, rendered here
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


def render_gradient_text(text: str, font: ImageFont.FreeTypeFont,
                         start_color: tuple, end_color: tuple,
                         draw_size: int = 0) -> Image.Image:
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
    """RBX15 Maker app tile — solid navy, gradient-text wordmark, tracked eyebrow.

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
    text_img = render_gradient_text("RBX", font, CYAN, MAGENTA, size)

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


def corner_glow(w: int, h: int, which: str = "both") -> Image.Image:
    """Soft radial glows used as depth accents on bigger canvases.
    which: 'cyan-tl', 'magenta-br', or 'both'.
    """
    g = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(g)
    short = min(w, h)

    if which in ("cyan-tl", "both"):
        glow_r = int(short * 0.65)
        for step in range(12):
            r = int(glow_r * (1 - step / 12))
            alpha = int(22 * (step / 12))
            gdraw.ellipse([-r // 3, -r // 3, r, r],
                          fill=(CYAN[0], CYAN[1], CYAN[2], alpha))

    if which in ("magenta-br", "both"):
        glow_r = int(short * 0.65)
        for step in range(12):
            r = int(glow_r * (1 - step / 12))
            alpha = int(20 * (step / 12))
            gdraw.ellipse([w - r, h - r, w + r // 3, h + r // 3],
                          fill=(MAGENTA[0], MAGENTA[1], MAGENTA[2], alpha))

    return g


def _fit_font_to_width(text: str, target_w: int, min_px: int, max_px: int,
                       black: bool = True) -> ImageFont.FreeTypeFont:
    """Binary-search the largest font size whose text fits target_w."""
    lo, hi, best = min_px, max_px, min_px
    while lo <= hi:
        mid = (lo + hi) // 2
        f = find_font(mid, black=black)
        bb = f.getbbox(text)
        if bb[2] - bb[0] <= target_w:
            best = mid
            lo = mid + 1
        else:
            hi = mid - 1
    return find_font(best, black=black)


def draw_box_art(size: int) -> Image.Image:
    """1:1 Store box art — the product mark, centered composition."""
    img = Image.new("RGBA", (size, size), NAVY_DEEP)
    img = Image.alpha_composite(img, corner_glow(size, size, "both"))
    draw = ImageDraw.Draw(img)

    # Hairline inner stroke
    inset = max(8, size // 28)
    stroke_w = max(1, size // 200)
    draw.rounded_rectangle(
        [(inset, inset), (size - inset - 1, size - inset - 1)],
        radius=max(12, size // 12),
        outline=(255, 255, 255, 36),
        width=stroke_w,
    )

    # Primary mark: "RBX" gradient, sized to ~58% of width
    font = _fit_font_to_width("RBX", int(size * 0.58), int(size * 0.25), int(size * 0.75))
    wordmark = render_gradient_text("RBX", font, CYAN, MAGENTA)
    tx = (size - wordmark.width) // 2
    ty = int(size * 0.30)
    img.alpha_composite(wordmark, (tx, ty))

    # Primary eyebrow: CLASSIC R15
    efont = find_font(max(14, int(size * 0.055)), black=False)
    eyebrow = "C L A S S I C   R 1 5"
    ebbox = efont.getbbox(eyebrow)
    ew = ebbox[2] - ebbox[0]
    ex = (size - ew) // 2 - ebbox[0]
    ey = ty + wordmark.height + int(size * 0.04) - ebbox[1]
    ImageDraw.Draw(img).text((ex, ey), eyebrow, font=efont,
                             fill=(CYAN[0], CYAN[1], CYAN[2], 230))

    # Sub-eyebrow: SHIRT & PANTS MAKER
    sfont = find_font(max(11, int(size * 0.038)), black=False)
    sub = "S H I R T   &   P A N T S   M A K E R"
    sbbox = sfont.getbbox(sub)
    sw = sbbox[2] - sbbox[0]
    sx = (size - sw) // 2 - sbbox[0]
    sy = ey + (ebbox[3] - ebbox[1]) + int(size * 0.03) - sbbox[1]
    ImageDraw.Draw(img).text((sx, sy), sub, font=sfont,
                             fill=(200, 200, 220, 180))

    # Publisher chip bottom-right
    pfont = find_font(max(10, int(size * 0.025)), black=False)
    pub = "626 LABS LLC"
    pbbox = pfont.getbbox(pub)
    pw = pbbox[2] - pbbox[0]
    px = size - pw - int(size * 0.04) - pbbox[0]
    py = size - int(size * 0.06) - pbbox[1]
    ImageDraw.Draw(img).text((px, py), pub, font=pfont,
                             fill=(180, 180, 200, 160))

    return img.convert("RGB")


def draw_poster(w: int, h: int) -> Image.Image:
    """9:16 Store poster — vertical stack with tagline + feature chips."""
    img = Image.new("RGBA", (w, h), NAVY_DEEP)
    img = Image.alpha_composite(img, corner_glow(w, h, "both"))
    draw = ImageDraw.Draw(img)

    inset = max(10, w // 28)
    stroke_w = max(1, w // 200)
    draw.rounded_rectangle(
        [(inset, inset), (w - inset - 1, h - inset - 1)],
        radius=max(16, w // 12),
        outline=(255, 255, 255, 36),
        width=stroke_w,
    )

    # Primary mark — "RBX", sized for poster width
    font = _fit_font_to_width("RBX", int(w * 0.7), int(w * 0.3), int(w * 0.95))
    wordmark = render_gradient_text("RBX", font, CYAN, MAGENTA)
    tx = (w - wordmark.width) // 2
    ty = int(h * 0.18)
    img.alpha_composite(wordmark, (tx, ty))

    # Primary eyebrow
    efont = find_font(max(16, int(w * 0.055)), black=False)
    eyebrow = "C L A S S I C   R 1 5"
    ebbox = efont.getbbox(eyebrow)
    ex = (w - (ebbox[2] - ebbox[0])) // 2 - ebbox[0]
    ey = ty + wordmark.height + int(h * 0.02) - ebbox[1]
    ImageDraw.Draw(img).text((ex, ey), eyebrow, font=efont,
                             fill=(CYAN[0], CYAN[1], CYAN[2], 230))

    # Sub-eyebrow
    sfont = find_font(max(13, int(w * 0.038)), black=False)
    sub = "S H I R T   &   P A N T S   M A K E R"
    sbbox = sfont.getbbox(sub)
    sx = (w - (sbbox[2] - sbbox[0])) // 2 - sbbox[0]
    sy = ey + (ebbox[3] - ebbox[1]) + int(h * 0.015) - sbbox[1]
    ImageDraw.Draw(img).text((sx, sy), sub, font=sfont,
                             fill=(200, 200, 220, 180))

    # Tagline in middle
    tfont = find_font(max(14, int(w * 0.045)), black=False)
    tagline_lines = ["Design Roblox templates", "without fighting your tools."]
    lh = int((tfont.getbbox("M")[3] - tfont.getbbox("M")[1]) * 1.4)
    tag_y = int(h * 0.50)
    for line in tagline_lines:
        tbbox = tfont.getbbox(line)
        tlx = (w - (tbbox[2] - tbbox[0])) // 2 - tbbox[0]
        ImageDraw.Draw(img).text((tlx, tag_y), line, font=tfont,
                                 fill=(235, 240, 250, 255))
        tag_y += lh

    # Feature chips near bottom
    chip_font = find_font(max(10, int(w * 0.028)), black=False)
    chips = ["LAYERS", "ADJUSTMENTS", "WORD ART", "ASSET PACKS"]
    chip_gap = int(w * 0.02)
    chip_widths = [chip_font.getbbox(c)[2] - chip_font.getbbox(c)[0] for c in chips]
    total_chips_w = sum(chip_widths) + chip_gap * (len(chips) - 1) + int(w * 0.06) * len(chips)
    # If chips don't fit on one line, drop to two rows
    if total_chips_w > w * 0.9:
        rows = [chips[:2], chips[2:]]
    else:
        rows = [chips]
    chip_y = int(h * 0.80)
    chip_lh = int((chip_font.getbbox("M")[3] - chip_font.getbbox("M")[1]) * 2.2)
    for row in rows:
        row_widths = [chip_font.getbbox(c)[2] - chip_font.getbbox(c)[0] + int(w * 0.04) for c in row]
        row_total = sum(row_widths) + chip_gap * (len(row) - 1)
        x = (w - row_total) // 2
        for c, cw in zip(row, row_widths):
            cbbox = chip_font.getbbox(c)
            ctx = x + (cw - (cbbox[2] - cbbox[0])) // 2 - cbbox[0]
            cty = chip_y + int(w * 0.008) - cbbox[1]
            # Chip pill
            draw.rounded_rectangle(
                [(x, chip_y), (x + cw, chip_y + chip_lh - int(w * 0.01))],
                radius=chip_lh // 2,
                outline=(CYAN[0], CYAN[1], CYAN[2], 100),
                width=1,
            )
            ImageDraw.Draw(img).text((ctx, cty), c, font=chip_font,
                                     fill=(CYAN[0], CYAN[1], CYAN[2], 220))
            x += cw + chip_gap
        chip_y += chip_lh + int(w * 0.01)

    # Publisher chip at the very bottom
    pfont = find_font(max(10, int(w * 0.025)), black=False)
    pub = "626 LABS LLC"
    pbbox = pfont.getbbox(pub)
    pw = pbbox[2] - pbbox[0]
    px = (w - pw) // 2 - pbbox[0]
    py = h - int(h * 0.045) - pbbox[1]
    ImageDraw.Draw(img).text((px, py), pub, font=pfont,
                             fill=(180, 180, 200, 160))

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

    # --- App tiles: RBX15 Maker product identity ---------------------------
    for size in (71, 150, 300):
        tile = draw_app_tile(size)
        out = HERE / f"app-tile-{size}x{size}.png"
        tile.save(out, "PNG")
        print(f"wrote {out.name} ({size}x{size})")

    # --- Store box art (1:1) — RBX15 product branding ---------------------
    for size in (1080, 2160):
        box = draw_box_art(size)
        out = HERE / f"store-box-{size}x{size}.png"
        box.save(out, "PNG")
        print(f"wrote {out.name} ({size}x{size})")

    # --- Store poster (9:16) — RBX15 product branding ---------------------
    for (w, h) in ((720, 1080), (1440, 2160)):
        poster = draw_poster(w, h)
        out = HERE / f"store-poster-{w}x{h}.png"
        poster.save(out, "PNG")
        print(f"wrote {out.name} ({w}x{h})")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
