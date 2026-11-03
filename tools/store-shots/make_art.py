"""Neon Tide: original art layers for the RBX15 Store outfit (shirt + pants), drawn at 2x for placement at scale 0.5.

Writes PNGs to out/outfit/art and base projects (fills + art layers) to out/outfit/base-*.r15proj, including the
build-up stages the trailer shows (base-step0: empty, base-step1: fills only).
The builder (OutfitBuilder) then adds the engine's own Word Art and clothing details and saves the final projects.
"""
import base64, io, json, math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).parent / "out" / "outfit"
HERE.mkdir(parents=True, exist_ok=True)
ART = HERE / "art"; ART.mkdir(exist_ok=True)

SUN_TOP, SUN_BOT = (255, 210, 63), (255, 46, 136)
CYAN, MAGENTA, PURPLE, NAVY = (23, 212, 250), (255, 46, 136), (42, 15, 92), (11, 20, 48)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def sun(d):
    """A synthwave sun: a vertical yellow-to-magenta disc with bands cut from its lower half, and a soft glow."""
    pad = d // 6
    w = d + 2 * pad
    img = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    disc = Image.new("RGBA", (d, d), (0, 0, 0, 0))
    px = disc.load()
    r = d / 2
    for y in range(d):
        t = y / (d - 1)
        c = lerp(SUN_TOP, SUN_BOT, t)
        # bands: thicker toward the bottom, only below the middle
        cut = False
        if t > 0.52:
            k = (t - 0.52) / 0.48
            period = d * 0.085
            gap = period * (0.18 + 0.5 * k)
            cut = ((y - d * 0.52) % period) < gap
        for x in range(d):
            if (x + 0.5 - r) ** 2 + (y + 0.5 - r) ** 2 <= r * r and not cut:
                px[x, y] = c + (255,)
    glow = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([pad, pad, pad + d, pad + d], fill=MAGENTA + (150,))
    glow = glow.filter(ImageFilter.GaussianBlur(d * 0.09))
    img.alpha_composite(glow)
    img.alpha_composite(disc, (pad, pad))
    return img


def waves(w, h, lines=3, amp=None, color=CYAN, width=4, phase=0.0):
    """Stacked sine waves across a strip."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    amp = amp or h / (lines * 3.2)
    for i in range(lines):
        y0 = h * (i + 0.5) / lines
        a = 255 - i * 55
        pts = [(x, y0 + amp * math.sin((x / w) * math.tau * 3 + phase + i * 0.9)) for x in range(0, w + 1, 2)]
        dr.line(pts, fill=color + (a,), width=width, joint="curve")
    return img


def tide_stripe(w, h):
    """A vertical stripe for a leg: a zigzag tide line, magenta at the top fading to cyan, with a thin cyan twin."""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dr = ImageDraw.Draw(img)
    steps = 9
    for off, col, width in ((0, None, 10), (w * 0.28, CYAN, 3)):
        pts = []
        for i in range(steps + 1):
            y = h * i / steps
            x = w * 0.5 + off * 0.5 + (w * 0.22 if i % 2 else -w * 0.22)
            pts.append((x, y))
        if col is None:
            for i in range(len(pts) - 1):
                c = lerp(MAGENTA, CYAN, i / (len(pts) - 2))
                dr.line([pts[i], pts[i + 1]], fill=c + (255,), width=width)
        else:
            dr.line(pts, fill=col + (200,), width=width, joint="curve")
    return img


def b64(img):
    buf = io.BytesIO(); img.save(buf, "PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def layer(lid, name, img, x, y, scale=0.5):
    """A layer at template position (x, y) = the scaled image's top-left."""
    return {"id": lid, "name": name, "visible": True, "opacity": 1, "x": x, "y": y, "scale": scale, "rotation": 0,
            "flipH": False, "flipV": False, "brightness": 100, "contrast": 100, "saturate": 100, "hueRotate": 0,
            "rShift": 0, "gShift": 0, "bShift": 0, "imgSrc": b64(img)}


def fills(prefixes, torso_fill, limb_fill):
    faces = ("up", "right", "front", "left", "back", "down")
    out = {}
    body, r, l = prefixes
    for f in faces:
        out[f"{body}_{f}"] = dict(torso_fill)
        out[f"{r}_{f}"] = dict(limb_fill)
        out[f"{l}_{f}"] = dict(limb_fill)
    return out


def project(mode, region_colors, layers):
    return {"version": "5.0", "app": "RBX15 Classic Shirt and Pants Maker", "savedAt": "2026-11-03T00:00:00.000Z",
            "mode": mode, "bgColor": "transparent", "bgOpacity": 1, "bgImageSrc": None,
            "regionColors": region_colors, "showMask": True, "showGrid": False, "layers": layers}


# ---- art -------------------------------------------------------------------------------------------------------------
big_sun = sun(180)                       # 2x; placed at 0.5 -> 120 px incl. glow
big_sun.save(ART / "sun.png")
front_waves = waves(240, 60, lines=3, width=5)   # under the sun, across the front panel
front_waves.save(ART / "front-waves.png")
sleeve_waves = waves(524, 40, lines=2, width=5)  # across one limb's four side faces (262 px wide at 0.5)
sleeve_waves.save(ART / "sleeve-waves.png")
leg_stripe = tide_stripe(64, 240)
leg_stripe.save(ART / "leg-stripe.png")
small_sun = sun(90)
small_sun.save(ART / "small-sun.png")

TORSO = {"type": "linear", "c1": "#2a0f5c", "c2": "#0b1430", "angle": 180}
ARMS = {"type": "linear", "c1": "#17d4fa", "c2": "#2a0f5c", "angle": 180}
CAMO = {"type": "camo", "c1": "#0b1430", "c2": "#2a0f5c", "scale": 1}

# Shirt: front torso is (231, 74, 128x128); the arms' side faces span x 19-281 and 308-570 at y 355-483.
sw, sh = big_sun.size
shirt_layers = [
    layer(1, "Neon Sun", big_sun, 231 + (128 - sw / 2) / 2, 92),
    layer(2, "Tide Waves", front_waves, 231 + (128 - 120) / 2, 164),
    layer(3, "Right Sleeve Waves", sleeve_waves, 19, 448),
    layer(4, "Left Sleeve Waves", sleeve_waves, 308, 448),
]
json.dump(project("Shirt", fills(("torso", "rarm", "larm"), TORSO, ARMS), shirt_layers),
          open(HERE / "base-shirt.r15proj", "w", encoding="utf-8"))

# Pants: waist faces share the torso geometry; leg fronts are (217, 355) and (308, 355), 64x128.
ssw, _ = small_sun.size
pants_layers = [
    layer(1, "Right Leg Tide", leg_stripe, 217, 361),
    layer(2, "Left Leg Tide", leg_stripe, 308, 361),
    layer(3, "Back Sun", small_sun, 427 + (128 - ssw / 2) / 2, 120),
    layer(4, "Right Cuff Waves", sleeve_waves, 19, 452),
    layer(5, "Left Cuff Waves", sleeve_waves, 308, 452),
]
json.dump(project("Pants", fills(("waist", "rleg", "lleg"), TORSO, CAMO), pants_layers),
          open(HERE / "base-pants.r15proj", "w", encoding="utf-8"))
json.dump(project("Shirt", {}, []), open(HERE / "base-step0.r15proj", "w", encoding="utf-8"))
json.dump(project("Shirt", fills(("torso", "rarm", "larm"), TORSO, ARMS), []), open(HERE / "base-step1.r15proj", "w", encoding="utf-8"))
print("art and base projects written")
