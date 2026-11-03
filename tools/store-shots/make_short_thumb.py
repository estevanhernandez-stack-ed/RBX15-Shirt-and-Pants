"""The 1080x1920 cover for the YouTube Short and the TikTok post: the Neon Tide shirt and pants side by side under
the RBX15 wordmark, from the two hero frames the Store screenshots use.

    python make_short_thumb.py [--shots out/shots]     # out/tiktok/short-thumbnail-1080x1920.png
"""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import brand_cards
import make_vertical as mv

TOOL = Path(__file__).parent


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--shots", type=Path, default=TOOL / "out" / "shots")
    args = ap.parse_args()
    # no single capture wears both pieces, so the matching set stands side by side
    shirt = Image.open(args.shots / "shirt-spin" / "f0116.jpg").convert("RGB")
    pants = Image.open(args.shots / "pants-spin" / "f0121.jpg").convert("RGB")
    h, top = 960, 640
    w = round((mv.AVATAR[2] - mv.AVATAR[0]) * h / (mv.AVATAR[3] - mv.AVATAR[1]))
    img = mv.panel_fill(shirt, h / (mv.AVATAR[3] - mv.AVATAR[1]), top)
    for frame, x in ((shirt, mv.W // 2 - w + 20), (pants, mv.W // 2 - 20)):
        body = frame.crop(mv.AVATAR).resize((w, h), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 60, 2))
        mv.soft_paste(img, body, x, top)

    fonts = brand_cards.load_fonts(mv.FONTS)
    def face(family, size, weight):
        f = ImageFont.truetype(str(next((mv.FONTS / family).glob("*.ttf"))), size)
        f.set_variation_by_axes([weight]); return f
    d = ImageDraw.Draw(img); cx = mv.W / 2
    brand_cards.tracked(d, cx, 200, "626 LABS  /  NEON TIDE", fonts["mono"], brand_cards.SOFT)
    wm = face("spacegrotesk", 230, 700)
    brand_cards.gradient_text(img, (cx - d.textlength("RBX15", font=wm) / 2, 240), "RBX15", wm)
    hook = face("spacegrotesk", 64, 600)
    brand_cards.centered(d, cx, 520, "Painted, not placed.", hook, brand_cards.WHITE)
    dst = mv.OUT / "short-thumbnail-1080x1920.png"; mv.OUT.mkdir(parents=True, exist_ok=True)
    img.save(dst); print("wrote", dst)


if __name__ == "__main__":
    main()
