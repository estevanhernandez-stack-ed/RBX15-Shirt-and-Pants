"""Super hero art (1920x1080, no product title) and the trailer thumbnail for the Neon Tide listing update."""
import math, shutil
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

TOOL = Path(__file__).parent
OUT = TOOL / "out" / "store"; OUT.mkdir(parents=True, exist_ok=True)
TPL = TOOL / "out" / "outfit" / "final"
W, H = 1920, 1080
TOP, MID, BOT = (12, 8, 40), (58, 14, 92), (8, 20, 44)
SUN_TOP, SUN_BOT, CYAN, MAGENTA = (255, 210, 63), (255, 46, 136), (23, 212, 250), (255, 46, 136)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


img = Image.new("RGB", (W, H))
px = img.load()
for y in range(H):
    t = y / (H - 1)
    c = lerp(TOP, MID, t / 0.62) if t < 0.62 else lerp(MID, BOT, (t - 0.62) / 0.38)
    for x in range(W):
        px[x, y] = c

# the sun, banded in its lower half, sitting on the horizon
horizon = int(H * 0.62)
r = 300
cx, cy = W // 2, horizon - 190
glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
ImageDraw.Draw(glow).ellipse([cx - r - 80, cy - r - 80, cx + r + 80, cy + r + 80], fill=MAGENTA + (120,))
img = Image.alpha_composite(img.convert("RGBA"), glow.filter(ImageFilter.GaussianBlur(110))).convert("RGB")
sun = Image.new("RGBA", (W, H), (0, 0, 0, 0))
sp = sun.load()
for y in range(cy - r, min(cy + r, horizon)):
    t = (y - (cy - r)) / (2 * r)
    col = lerp(SUN_TOP, SUN_BOT, t)
    if t > 0.5:
        k = (t - 0.5) / 0.5
        period = 2 * r * 0.075
        if ((y - (cy)) % period) < period * (0.15 + 0.55 * k):
            continue
    half = math.sqrt(max(r * r - (y - cy) ** 2, 0))
    for x in range(int(cx - half), int(cx + half)):
        sp[x, y] = col + (255,)
img.paste(sun, (0, 0), sun)

# a perspective grid and tide lines below the horizon
d = ImageDraw.Draw(img, "RGBA")
for i in range(-14, 15):
    d.line([(cx + i * 30, horizon), (cx + i * 260, H)], fill=CYAN + (70,), width=2)
for j in range(1, 12):
    y = horizon + int((H - horizon) * (j / 11) ** 1.8)
    d.line([(0, y), (W, y)], fill=CYAN + (60,), width=2)
for k in range(3):
    pts = [(x, horizon + 18 + k * 22 + 9 * math.sin(x / 70 + k)) for x in range(0, W + 1, 4)]
    d.line(pts, fill=CYAN + (230 - k * 60,), width=5)

# the two templates, as tilted cards on either side
for name, x, angle in (("Neon_Tide_Shirt.png", 150, 6), ("Neon_Tide_Pants.png", W - 150 - 585, -6)):
    t = Image.open(TPL / name).convert("RGBA")
    card = Image.new("RGBA", (585 + 40, 559 + 40), (10, 21, 36, 235))
    card.alpha_composite(t, (20, 20))
    ImageDraw.Draw(card).rounded_rectangle([0, 0, card.width - 1, card.height - 1], 18, outline=CYAN + (150,), width=3)
    card = card.rotate(angle, expand=True, resample=Image.BICUBIC)
    shadow = Image.new("RGBA", card.size, (0, 0, 0, 0))
    shadow.paste((0, 0, 0, 120), (0, 0), card.split()[3])
    pad = 60
    sh = Image.new("RGBA", (card.width + 2 * pad, card.height + 2 * pad), (0, 0, 0, 0)); sh.paste(shadow, (pad, pad))
    sh = sh.filter(ImageFilter.GaussianBlur(26))
    base = img.convert("RGBA"); base.alpha_composite(sh, (x + 14 - pad, 230 + 24 - pad)); base.alpha_composite(card, (x, 230)); img = base.convert("RGB")

img.save(OUT / "super-hero-art-1920x1080.png")
shutil.copy(TOOL / "out" / "trailer" / "00-intro.png", OUT / "trailer-thumbnail-1920x1080.png")
print("hero art and thumbnail written")
