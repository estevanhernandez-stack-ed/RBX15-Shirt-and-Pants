"""Generate the full set of MSIX tile images for RBX Maker.

Windows Store / MSIX wants a specific set of tile sizes and scales that
don't match the generic Partner-Center store-listing assets in
docs/store-assets/. This script outputs the MSIX-specific tiles into
windows/msix/Images/ where make-msix.ps1 picks them up.

Design re-uses the same brand-compliant approach as docs/store-assets/
(solid brand-navy base, brand-gradient text fill for "RBX", tracked
"CLASSIC R15" eyebrow, hairline inner stroke, single soft corner glow).

Required files (per Package.appxmanifest.template):
  StoreLogo.png              50x50
  Square44x44Logo.png        44x44
  Square71x71Logo.png        71x71   (SmallTile)
  Square150x150Logo.png      150x150
  Square310x310Logo.png      310x310 (LargeTile)
  Wide310x150Logo.png        310x150
  SplashScreen.png           620x300

MSIX scale targets (scale-100, -125, -150, -200, -400) could be generated
too for high-DPI, but Store ingestion is fine with just the base
resolutions. Add scales later if needed.

Run:
  python make-msix-images.py
"""

import pathlib
from PIL import Image, ImageDraw, ImageFont

HERE = pathlib.Path(__file__).resolve().parent
OUT = HERE / "Images"
OUT.mkdir(exist_ok=True)

NAVY_DEEP = (15, 31, 49, 255)
NAVY      = (25, 46, 68, 255)
CYAN      = (23, 212, 250, 255)
MAGENTA   = (242, 47, 137, 255)
INK_0     = (255, 255, 255, 255)


def find_font(target_size: int, *, black: bool = False) -> ImageFont.FreeTypeFont:
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


def render_gradient_text(text: str, font, start, end) -> Image.Image:
    bbox = font.getbbox(text)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    mask = Image.new("L", (tw, th), 0)
    ImageDraw.Draw(mask).text((-bbox[0], -bbox[1]), text, font=font, fill=255)

    grad = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    gpx = grad.load()
    for y in range(th):
        for x in range(tw):
            t = (x + y) / max(1, tw + th - 2)
            r = int(start[0] + (end[0] - start[0]) * t)
            g = int(start[1] + (end[1] - start[1]) * t)
            b = int(start[2] + (end[2] - start[2]) * t)
            gpx[x, y] = (r, g, b, 255)

    out = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def corner_glow(size_w: int, size_h: int) -> Image.Image:
    """Single soft cyan radial glow in the top-left corner, low intensity."""
    g = Image.new("RGBA", (size_w, size_h), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(g)
    glow_r = int(min(size_w, size_h) * 0.55)
    for step in range(10):
        r = int(glow_r * (1 - step / 10))
        alpha = int(18 * (step / 10))
        gdraw.ellipse([-r // 3, -r // 3, r, r], fill=(CYAN[0], CYAN[1], CYAN[2], alpha))
    return g


def inner_stroke(draw: ImageDraw.ImageDraw, w: int, h: int) -> None:
    short = min(w, h)
    inset = max(2, short // 24)
    stroke_w = max(1, short // 100)
    draw.rounded_rectangle(
        [(inset, inset), (w - inset - 1, h - inset - 1)],
        radius=max(6, short // 10),
        outline=(255, 255, 255, 30),
        width=stroke_w,
    )


def draw_square_tile(size: int, show_eyebrow: bool = True) -> Image.Image:
    """Square RBX app tile at any resolution."""
    img = Image.new("RGBA", (size, size), NAVY_DEEP)
    img = Image.alpha_composite(img, corner_glow(size, size))
    draw = ImageDraw.Draw(img)
    inner_stroke(draw, size, size)

    # Binary-search font size to hit 62% tile width with "RBX"
    target_w = int(size * 0.62)
    lo, hi, best = int(size * 0.25), int(size * 0.85), int(size * 0.25)
    while lo <= hi:
        mid = (lo + hi) // 2
        f = find_font(mid, black=True)
        bb = f.getbbox("RBX")
        if bb[2] - bb[0] <= target_w:
            best = mid
            lo = mid + 1
        else:
            hi = mid - 1
    font = find_font(best, black=True)
    wordmark = render_gradient_text("RBX", font, CYAN, MAGENTA)
    tx = (size - wordmark.width) // 2
    ty = int(size * 0.22) if show_eyebrow and size >= 150 else (size - wordmark.height) // 2
    img.alpha_composite(wordmark, (tx, ty))

    if show_eyebrow and size >= 150:
        eyebrow_size = max(9, int(size * 0.075))
        efont = find_font(eyebrow_size, black=False)
        eyebrow = "C L A S S I C   R 1 5"
        ebbox = efont.getbbox(eyebrow)
        ew = ebbox[2] - ebbox[0]
        ex = (size - ew) // 2 - ebbox[0]
        ey = ty + wordmark.height + int(size * 0.05) - ebbox[1]
        ImageDraw.Draw(img).text((ex, ey), eyebrow, font=efont,
                                 fill=(CYAN[0], CYAN[1], CYAN[2], 220))

    return img.convert("RGB")


def draw_wide_tile(w: int, h: int) -> Image.Image:
    """Wide 310x150 tile — wordmark + eyebrow, left-aligned."""
    img = Image.new("RGBA", (w, h), NAVY_DEEP)
    img = Image.alpha_composite(img, corner_glow(w, h))
    draw = ImageDraw.Draw(img)
    inner_stroke(draw, w, h)

    # Wordmark takes ~50% of width, left-padded
    target_w = int(w * 0.38)
    lo, hi, best = int(h * 0.3), int(h * 0.9), int(h * 0.3)
    while lo <= hi:
        mid = (lo + hi) // 2
        f = find_font(mid, black=True)
        bb = f.getbbox("RBX")
        if bb[2] - bb[0] <= target_w:
            best = mid
            lo = mid + 1
        else:
            hi = mid - 1
    font = find_font(best, black=True)
    wordmark = render_gradient_text("RBX", font, CYAN, MAGENTA)
    tx = int(w * 0.08)
    ty = int(h * 0.2)
    img.alpha_composite(wordmark, (tx, ty))

    # Eyebrow on right, right-aligned
    eyebrow_size = max(11, int(h * 0.14))
    efont = find_font(eyebrow_size, black=False)
    eyebrow = "C L A S S I C   R 1 5"
    ebbox = efont.getbbox(eyebrow)
    ew = ebbox[2] - ebbox[0]
    ex = tx + wordmark.width + int(w * 0.05) - ebbox[0]
    ey = ty + (wordmark.height - (ebbox[3] - ebbox[1])) // 2 - ebbox[1]
    ImageDraw.Draw(img).text((ex, ey), eyebrow, font=efont,
                             fill=(CYAN[0], CYAN[1], CYAN[2], 220))

    return img.convert("RGB")


def draw_splash(w: int, h: int) -> Image.Image:
    """Splash 620x300 — centered wordmark + eyebrow + subtle publisher chip."""
    img = Image.new("RGBA", (w, h), NAVY_DEEP)
    img = Image.alpha_composite(img, corner_glow(w, h))
    draw = ImageDraw.Draw(img)

    # Wordmark target ~35% of width (splash is wider, don't scream)
    target_w = int(w * 0.32)
    lo, hi, best = int(h * 0.25), int(h * 0.7), int(h * 0.25)
    while lo <= hi:
        mid = (lo + hi) // 2
        f = find_font(mid, black=True)
        bb = f.getbbox("RBX")
        if bb[2] - bb[0] <= target_w:
            best = mid
            lo = mid + 1
        else:
            hi = mid - 1
    font = find_font(best, black=True)
    wordmark = render_gradient_text("RBX", font, CYAN, MAGENTA)
    tx = (w - wordmark.width) // 2
    ty = int(h * 0.27)
    img.alpha_composite(wordmark, (tx, ty))

    # Eyebrow centered below
    eyebrow_size = max(10, int(h * 0.06))
    efont = find_font(eyebrow_size, black=False)
    eyebrow = "C L A S S I C   R 1 5   T E M P L A T E   M A K E R"
    ebbox = efont.getbbox(eyebrow)
    ew = ebbox[2] - ebbox[0]
    ex = (w - ew) // 2 - ebbox[0]
    ey = ty + wordmark.height + int(h * 0.04) - ebbox[1]
    ImageDraw.Draw(img).text((ex, ey), eyebrow, font=efont,
                             fill=(CYAN[0], CYAN[1], CYAN[2], 220))

    # Publisher chip bottom-right
    pub_size = max(8, int(h * 0.038))
    pfont = find_font(pub_size, black=False)
    pub = "626 LABS LLC"
    pbbox = pfont.getbbox(pub)
    pw = pbbox[2] - pbbox[0]
    px = w - pw - int(w * 0.03) - pbbox[0]
    py = h - int(h * 0.08) - pbbox[1]
    ImageDraw.Draw(img).text((px, py), pub, font=pfont, fill=(200, 200, 220, 140))

    return img.convert("RGB")


def main() -> None:
    targets = [
        ("StoreLogo.png",         50,  50,  "square", True),
        ("Square44x44Logo.png",   44,  44,  "square", False),
        ("Square71x71Logo.png",   71,  71,  "square", False),
        ("Square150x150Logo.png", 150, 150, "square", True),
        ("Square310x310Logo.png", 310, 310, "square", True),
        ("Wide310x150Logo.png",   310, 150, "wide",   None),
        ("SplashScreen.png",      620, 300, "splash", None),
    ]
    for name, w, h, kind, show_eyebrow in targets:
        if kind == "square":
            img = draw_square_tile(w, show_eyebrow=show_eyebrow)
        elif kind == "wide":
            img = draw_wide_tile(w, h)
        else:
            img = draw_splash(w, h)
        out = OUT / name
        img.save(out, "PNG")
        print(f"wrote {out.name} ({w}x{h})")


if __name__ == "__main__":
    main()
