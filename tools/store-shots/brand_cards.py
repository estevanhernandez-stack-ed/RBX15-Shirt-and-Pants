"""The 626 Labs intro and outro cards, in the RoRoRo launch video's layout: an icon tile, a mono eyebrow, a gradient
wordmark, a gradient rule, and the "626 LABS / IMAGINE SOMETHING ELSE." footer. The outro carries a QR code to the
product page. Imported by make_assets.py.
"""
from pathlib import Path
import qrcode
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
TEAL, PLUM, INK = (16, 64, 88), (58, 24, 62), (14, 22, 40)
CYAN, MAGENTA, WHITE, GREY, SOFT = (23, 212, 250), (242, 47, 137), (240, 244, 250), (150, 162, 182), (196, 206, 222)


def background():
    """A diagonal teal-to-plum wash over ink, like the RoRoRo cards."""
    small = Image.new("RGB", (64, 36))
    px = small.load()
    for y in range(36):
        for x in range(64):
            t = (x / 63 * 0.55 + y / 35 * 0.45)
            a = TEAL if t < 0.5 else PLUM
            b = INK
            k = abs(t - 0.5) * 2
            mid = tuple(int(INK[i] * (1 - 0.35) + (TEAL[i] if t < 0.5 else PLUM[i]) * 0.35) for i in range(3))
            px[x, y] = tuple(int(mid[i] * (1 - k) + a[i] * k) for i in range(3))
    return small.resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(30))


def gradient_text(draw_on, xy, text, font, c1=CYAN, c2=MAGENTA):
    """Text filled with a left-to-right cyan-to-magenta gradient."""
    mask = Image.new("L", draw_on.size, 0)
    ImageDraw.Draw(mask).text(xy, text, font=font, fill=255)
    box = mask.getbbox()
    grad = Image.new("RGB", draw_on.size)
    gd = ImageDraw.Draw(grad)
    for x in range(box[0], box[2] + 1):
        t = (x - box[0]) / max(box[2] - box[0], 1)
        gd.line([(x, box[1]), (x, box[3])], fill=tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3)))
    draw_on.paste(grad, (0, 0), mask)


def rule(d, cx, y, w=162):
    for x in range(w):
        t = x / (w - 1)
        d.line([(cx - w // 2 + x, y), (cx - w // 2 + x, y + 2)], fill=tuple(int(CYAN[i] + (MAGENTA[i] - CYAN[i]) * t) for i in range(3)))


def centered(d, cx, y, text, font, fill):
    d.text((cx - d.textlength(text, font=font) / 2, y), text, font=font, fill=fill)


def tracked(d, cx, y, text, font, fill, track=0.32):
    """Centered text with letter-spacing (track is a fraction of the font size), as the RoRoRo eyebrows set it."""
    gap = font.size * track
    widths = [d.textlength(ch, font=font) for ch in text]
    total = sum(widths) + gap * (len(text) - 1)
    x = cx - total / 2
    for ch, w in zip(text, widths):
        d.text((x, y), ch, font=font, fill=fill); x += w + gap


def footer(d, fonts):
    tracked(d, W / 2, 1010, "626 LABS  /  IMAGINE SOMETHING ELSE.", fonts["mono_s"], GREY)


def tile(img, box_art, x, y, size=520):
    """The template glyph from the box art (no name under it), in a rounded navy tile with a soft shadow."""
    glyph = Image.open(box_art).convert("RGB").crop((150, 180, 930, 715))
    t = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(t).rounded_rectangle([0, 0, size - 1, size - 1], 56, fill=(20, 38, 62, 255))
    gw = int(size * 0.82); gh = int(glyph.height * gw / glyph.width)
    g = glyph.resize((gw, gh), Image.LANCZOS)
    # the box art's own navy is close to the tile's; key it out so the glyph sits on the tile
    ga = Image.new("L", g.size, 0); gp, ap = g.load(), ga.load()
    for yy in range(g.height):
        for xx in range(g.width):
            r, gg, b = gp[xx, yy]
            ap[xx, yy] = 0 if (r < 40 and gg < 50 and b < 75) else 255
    t.paste(g, ((size - gw) // 2, (size - gh) // 2), ga)
    sh = Image.new("RGBA", (size + 120, size + 120), (0, 0, 0, 0))
    ImageDraw.Draw(sh).rounded_rectangle([60, 70, 60 + size, 70 + size], 56, fill=(0, 0, 0, 120))
    img.paste(sh.filter(ImageFilter.GaussianBlur(28)), (x - 60, y - 60), sh.filter(ImageFilter.GaussianBlur(28)))
    img.paste(t, (x, y), t)


def intro(dst, box_art, fonts):
    img = background(); d = ImageDraw.Draw(img)
    tile(img, box_art, 210, 205)
    cx = 1290
    tracked(d, cx, 250, "626 LABS  /  RBX15", fonts["mono"], SOFT)
    wm = fonts["wordmark"]
    tw = d.textlength("RBX15", font=wm)
    gradient_text(img, (cx - tw / 2, 300), "RBX15", wm)
    centered(d, cx, 590, "Paint your clothes, don't just place them.", fonts["tag"], WHITE)
    rule(d, cx, 690)
    tracked(d, cx, 730, "WINDOWS  ·  MICROSOFT STORE", fonts["mono"], SOFT)
    footer(d, fonts)
    img.save(dst)


def outro(dst, fonts, url="626labs.dev/rbx15.html"):
    img = background(); d = ImageDraw.Draw(img)
    qr = qrcode.QRCode(border=1, box_size=20, error_correction=qrcode.constants.ERROR_CORRECT_M)
    qr.add_data("https://" + url); qr.make(fit=True)
    q = qr.make_image(fill_color=(14, 22, 40), back_color="white").convert("RGB").resize((520, 520), Image.NEAREST)
    card = Image.new("RGBA", (580, 580), (0, 0, 0, 0))
    ImageDraw.Draw(card).rounded_rectangle([0, 0, 579, 579], 28, fill=(255, 255, 255, 255))
    card.paste(q, (30, 30))
    img.paste(card, (170, 245), card)
    cx = 1330
    tracked(d, cx, 255, "WINDOWS  ·  MICROSOFT STORE", fonts["mono"], SOFT)
    centered(d, cx, 330, "Get it on the Microsoft Store.", fonts["head"], WHITE)
    rule(d, cx, 470)
    centered(d, cx, 525, url, fonts["url"], CYAN)
    centered(d, cx, 640, "Works offline. No account. No ads.", fonts["body"], SOFT)
    centered(d, cx, 700, "Independent tool. Not affiliated with Roblox Corporation.", fonts["body"], SOFT)
    footer(d, fonts)
    img.save(dst)


def load_fonts(font_dir: Path):
    def f(family, size, weight):
        path = next((font_dir / family).glob("*.ttf"))
        ft = ImageFont.truetype(str(path), size)
        axes = [a["name"] for a in ft.get_variation_axes()]
        ft.set_variation_by_axes([min(max(size, 14), 32) if n in (b"Optical size", "Optical size") else weight for n in axes])
        return ft
    return {"mono": f("jetbrainsmono", 30, 500), "mono_s": f("jetbrainsmono", 22, 500),
            "wordmark": f("spacegrotesk", 230, 700), "tag": f("spacegrotesk", 50, 500),
            "head": f("spacegrotesk", 62, 600), "url": f("spacegrotesk", 64, 500), "body": f("inter", 34, 400)}
