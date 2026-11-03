"""Neon Tide trailer and Store set: captioned stills, title cards and clip overlays from the captured frames.

Inputs: out/shots (PrintWindow stills, screen-copy clip frames). Outputs: out/trailer (trailer scene sources) and
out/store (the 1920x1080 Store screenshots).
"""
import shutil, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import brand_cards

TOOL = Path(__file__).parent
SHOTS = TOOL / "out" / "shots"
OUT = TOOL / "out" / "trailer"; OUT.mkdir(parents=True, exist_ok=True)
STORE = TOOL / "out" / "store"; STORE.mkdir(parents=True, exist_ok=True)
FONTS = TOOL.parent.parent / "assets" / "fonts"
NAVY, INK, CYAN, MAGENTA, WHITE = (10, 21, 36), (6, 12, 24), (23, 212, 250), (242, 47, 137), (240, 244, 250)


def font(family, file, size, weight=None):
    f = ImageFont.truetype(str(FONTS / family / file), size)
    if weight is not None:
        f.set_variation_by_axes([weight] if "opsz" not in file else [min(max(size, 14), 32), weight])
    return f


def first(family, *names):
    for n in names:
        p = FONTS / family / n
        if p.exists():
            return n
    return sorted(x.name for x in (FONTS / family).glob("*.ttf"))[0]


DISPLAY = first("spacegrotesk", "SpaceGrotesk.ttf", "SpaceGrotesk[wght].ttf")
BODY = first("inter", "Inter.ttf", "Inter[opsz,wght].ttf")
MONO = first("jetbrainsmono", "JetBrainsMono.ttf", "JetBrainsMono[wght].ttf")


def caption_band(eyebrow, text, w=1920, h=1080):
    """A lower-third: a dark band with a cyan eyebrow and a white line, on a transparent frame."""
    band = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(band)
    title = font("spacegrotesk", DISPLAY, 54, 600)
    eb = font("jetbrainsmono", MONO, 24, 600)
    tw = d.textlength(text, font=title)
    bw, bh = int(max(tw, d.textlength(eyebrow.upper(), font=eb)) + 120), 150
    x0, y0 = 80, h - bh - 70
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([x0, y0 + 8, x0 + bw, y0 + bh + 8], 22, fill=(0, 0, 0, 150))
    band.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)))
    d.rounded_rectangle([x0, y0, x0 + bw, y0 + bh], 22, fill=NAVY + (238,), outline=CYAN + (90,), width=2)
    d.rectangle([x0, y0 + 22, x0 + 6, y0 + bh - 22], fill=MAGENTA)
    d.text((x0 + 50, y0 + 26), eyebrow.upper(), font=eb, fill=CYAN)
    d.text((x0 + 50, y0 + 62), text, font=title, fill=WHITE)
    return band


def captioned(src, eyebrow, text, dst):
    im = Image.open(src).convert("RGBA")
    im.alpha_composite(caption_band(eyebrow, text))
    im.convert("RGB").save(dst)


def card(eyebrow, title, lines, dst, w=1920, h=1080):
    im = Image.new("RGB", (w, h), INK)
    glow = Image.new("RGB", (w, h), INK)
    g = ImageDraw.Draw(glow)
    g.ellipse([w * 0.55, -h * 0.4, w * 1.3, h * 0.7], fill=(60, 20, 90))
    g.ellipse([-w * 0.3, h * 0.45, w * 0.45, h * 1.4], fill=(8, 70, 100))
    im = Image.blend(im, glow.filter(ImageFilter.GaussianBlur(160)), 0.85)
    d = ImageDraw.Draw(im)
    d.text((140, 330), eyebrow.upper(), font=font("jetbrainsmono", MONO, 34, 600), fill=CYAN)
    tf = font("spacegrotesk", DISPLAY, 104, 700)
    y = 390
    for t in title.split("\n"):
        d.text((132, y), t, font=tf, fill=WHITE); y += 118
    lf = font("inter", BODY, 40)
    y += 30
    for ln in lines:
        d.text((140, y), ln, font=lf, fill=(176, 190, 210)); y += 58
    d.rectangle([140, 300, 260, 306], fill=MAGENTA)
    im.save(dst)


def fix_caption(frame, donor):
    """Copy the caption buttons (window chrome only) from an active-window frame onto one where focus had moved."""
    im = Image.open(frame).convert("RGB")
    im.paste(Image.open(donor).convert("RGB").crop((1760, 0, 1920, 40)), (1760, 0))
    return im


def clip(frames, start, end, eyebrow, text, dst):
    """Frames start..end (12 fps) -> a 30 fps H.264 clip with motion interpolation and the caption overlaid."""
    tmp = OUT / (dst.stem + "-frames"); shutil.rmtree(tmp, ignore_errors=True); tmp.mkdir()
    for i, n in enumerate(range(start, end + 1)):
        shutil.copy(SHOTS / frames / f"f{n:04d}.jpg", tmp / f"f{i:04d}.jpg")
    band = OUT / (dst.stem + "-band.png"); caption_band(eyebrow, text).save(band)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-framerate", "12", "-i", str(tmp / "f%04d.jpg"), "-i", str(band),
                    "-filter_complex", "[0:v]minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:vsbmc=1[v];[v][1:v]overlay=0:0,format=yuv420p",
                    "-c:v", "libx264", "-crf", "18", "-preset", "slow", str(dst)], check=True)
    shutil.rmtree(tmp)


# ---- Store set ------------------------------------------------------------------------------------------------------
fix_caption(SHOTS / "shirt-spin" / "f0116.jpg", SHOTS / "shirt-spin" / "f0090.jpg").save(STORE / "01-shirt-3d-preview.png")
Image.open(SHOTS / "pants-spin" / "f0121.jpg").convert("RGB").save(STORE / "02-pants-3d-preview.png")
for src, dst in (("02-layers.png", "03-layers.png"), ("03-background.png", "04-fills.png"), ("04-details.png", "05-details.png"),
                 ("05-word-art.png", "06-word-art.png"), ("07-pants-background.png", "07-pants-fills.png")):
    shutil.copy(SHOTS / src, STORE / dst)

# ---- trailer --------------------------------------------------------------------------------------------------------
BRAND = brand_cards.load_fonts(FONTS)
brand_cards.intro(OUT / "00-intro.png", TOOL.parent.parent / "docs" / "store-assets" / "store-box-1080x1080.png", BRAND)
captioned(SHOTS / "build-s0.png", "Start", "The exact 585×559 template", OUT / "02-empty.png")
captioned(SHOTS / "build-s1.png", "Fills", "Gradients that wrap the body", OUT / "03-fills.png")
captioned(SHOTS / "build-s2.png", "Layers", "Stack your own art", OUT / "04-art.png")
captioned(SHOTS / "build-s3.png", "Details", "Collars, cuffs and seams in one click", OUT / "05-details.png")
captioned(SHOTS / "build-s4.png", "Word Art", "15 fonts, six styles", OUT / "06-wordart.png")
clip("shirt-spin", 99, 171, "3D preview", "See it on an avatar while you work", OUT / "07-shirt-spin.mp4")
captioned(SHOTS / "07-pants-background.png", "Pants too", "Same tools, matching set", OUT / "08-pants.png")
clip("pants-spin", 109, 181, "Neon Tide", "Shirt and pants, made in RBX15", OUT / "09-pants-spin.mp4")
brand_cards.outro(OUT / "11-outro.png", BRAND)
print("assets written")
