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


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def draw_gradient_bg(size: int) -> Image.Image:
    """Navy-deep background with a diagonal cyan→magenta radial glow."""
    img = Image.new("RGBA", (size, size), NAVY_DEEP)
    draw = ImageDraw.Draw(img)

    # Fill solid navy first
    draw.rectangle([(0, 0), (size, size)], fill=NAVY_DEEP)

    # Soft radial cyan glow top-left
    for r in range(int(size * 0.7), 0, -2):
        t = r / (size * 0.7)
        alpha = int(60 * (1 - t))
        color = (CYAN[0], CYAN[1], CYAN[2], alpha)
        overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        cx, cy = int(size * 0.25), int(size * 0.25)
        odraw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)
        img = Image.alpha_composite(img, overlay)

    # Soft radial magenta glow bottom-right
    for r in range(int(size * 0.7), 0, -2):
        t = r / (size * 0.7)
        alpha = int(50 * (1 - t))
        color = (MAGENTA[0], MAGENTA[1], MAGENTA[2], alpha)
        overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
        odraw = ImageDraw.Draw(overlay)
        cx, cy = int(size * 0.75), int(size * 0.75)
        odraw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color)
        img = Image.alpha_composite(img, overlay)

    return img


def find_bold_font(target_size: int) -> ImageFont.FreeTypeFont:
    """Return the best available bold sans font, in preference order."""
    candidates = [
        "C:/Windows/Fonts/SegoeUIB.ttf",
        "C:/Windows/Fonts/seguibl.ttf",
        "C:/Windows/Fonts/arialbd.ttf",
        "/System/Library/Fonts/SFNS.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVu-Sans-Bold.ttf",
    ]
    for p in candidates:
        if pathlib.Path(p).exists():
            return ImageFont.truetype(p, target_size)
    return ImageFont.load_default()


def draw_app_tile(size: int) -> Image.Image:
    """RBX Maker app tile: gradient bg + 'RBX' wordmark in white + '15' small."""
    img = draw_gradient_bg(size)
    draw = ImageDraw.Draw(img)

    # Rounded-rect inner stroke to anchor the composition
    inset = max(2, size // 30)
    stroke_w = max(1, size // 80)
    draw.rounded_rectangle(
        [(inset, inset), (size - inset - 1, size - inset - 1)],
        radius=max(6, size // 10),
        outline=(255, 255, 255, 24),
        width=stroke_w,
    )

    # Primary mark: "RBX" in big bold white
    mark_size = int(size * 0.42)
    font = find_bold_font(mark_size)
    text = "RBX"
    bbox = draw.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = (size - tw) // 2 - bbox[0]
    ty = (size - th) // 2 - bbox[1] - int(size * 0.06)
    draw.text((tx, ty), text, font=font, fill=INK_0)

    # Secondary mark: "15" tagline below in cyan, smaller
    tag_size = max(10, int(size * 0.16))
    tag_font = find_bold_font(tag_size)
    tag = "15"
    tbbox = draw.textbbox((0, 0), tag, font=tag_font)
    tag_w = tbbox[2] - tbbox[0]
    tag_h = tbbox[3] - tbbox[1]
    tag_x = (size - tag_w) // 2 - tbbox[0]
    tag_y = ty + th + int(size * 0.04) - tbbox[1]
    draw.text((tag_x, tag_y), tag, font=tag_font, fill=CYAN)

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
