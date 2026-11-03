"""The 9:16 TikTok cut of the Neon Tide trailer, from the same captures and narration clips as the Store trailer.

The landscape window is never shrunk into the frame. Each scene crops the part that matters and fills the phone
with it: the template canvas for the build steps, the 3D preview for the spins (the avatar is about 230 px wide in
the window, so it is scaled up roughly 2.5x). Content stays between y 150 and 1450, clear of TikTok's top bar,
right rail and caption. It is cut to loop: no fade in, and the opening "R B X fifteen" lands right after the
closing "Imagine something else." Overlay text is left to TikTok's editor; the lines are in docs/store-assets/tiktok/.

    python make_vertical.py [--shots out/shots]     # out/tiktok/rbx15-tiktok.mp4
"""
import argparse, json, shutil, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont
import brand_cards

TOOL = Path(__file__).parent
FONTS = TOOL.parent.parent / "assets" / "fonts"
VOICE = TOOL / "voiceover"
OUT = TOOL / "out" / "tiktok"
W, H, FPS = 1080, 1920, 30
TEMPLATE = (603, 298, 1336, 998)    # the canvas, in every 1920x1080 capture
AVATAR = (1674, 420, 1906, 850)     # the 3D preview, avatar and turntable ring

# (kind, source, first frame, last frame or None, narration clip, seconds)
SCENES = [
    ("spin", "shirt-spin", 99, 159, "slide_01", 5.1),
    ("template", "build-s1.png", 0, None, "slide_03", 4.2),
    ("template", "build-s2.png", 0, None, "slide_04", 2.2),
    ("template", "build-s3.png", 0, None, "slide_05", 2.6),
    ("template", "build-s4.png", 0, None, "slide_06", 2.8),
    ("template", "07-pants-background.png", 0, None, "slide_08", 3.5),
    ("spin", "pants-spin", 109, 164, "slide_09", 4.5),
    ("outro", None, 0, None, "slide_10", 6.1),
]
VOICE_AT = 0.05            # narration starts this far into its scene; kept tight so the loop seam lands
LUFS, TRUE_PEAK, LRA = -17, -1.5, 11


def run(*args):
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", *map(str, args)], check=True)


def backdrop(crop, dim=0.45):
    """The crop itself, zoomed to fill, blurred and darkened: the TikTok-native fill."""
    s = max(W / crop.width, H / crop.height) * 1.15
    bg = crop.resize((int(crop.width * s), int(crop.height * s)), Image.BICUBIC)
    bg = bg.crop(((bg.width - W) // 2, (bg.height - H) // 2, (bg.width - W) // 2 + W, (bg.height - H) // 2 + H))
    return ImageEnhance.Brightness(bg.filter(ImageFilter.GaussianBlur(48))).enhance(dim)


def panel_fill(frame, scale, top):
    """The empty 3D preview panel's own gradient, stretched wide and scaled to line up with the avatar crop, so the
    crop's edge disappears. A blurred copy of the crop would ghost the avatar's arms into the background."""
    px, py0, py1 = 1684, 300, 985                      # an empty strip down the panel's left edge
    strip = frame.crop((px, py0, px + 6, py1)).resize((W, round((py1 - py0) * scale)), Image.BICUBIC)
    y = round(top - (AVATAR[1] - py0) * scale)
    canvas = Image.new("RGB", (W, H), strip.getpixel((W // 2, strip.height - 1)))
    canvas.paste(strip, (0, y))
    return canvas.filter(ImageFilter.GaussianBlur(12))


def soft_paste(canvas, img, x, y, feather=30):
    """Paste with a feathered edge, so a crop of the dark preview panel melts into its own backdrop."""
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).rectangle([feather, feather, img.width - feather, img.height - feather], fill=255)
    canvas.paste(img, (x, y), m.filter(ImageFilter.GaussianBlur(feather / 2)))


def header(canvas, fonts):
    d = ImageDraw.Draw(canvas)
    wm = fonts["header"]
    tw = d.textlength("RBX15", font=wm)
    brand_cards.gradient_text(canvas, (W / 2 - tw / 2, 160), "RBX15", wm)


def compose(kind, frame, fonts):
    if kind == "template":
        crop = frame.crop(TEMPLATE)
        w = 960; h = round(crop.height * w / crop.width)
        canvas = backdrop(crop)
        body = crop.resize((w, h), Image.LANCZOS)
        sh = Image.new("RGBA", (w + 80, h + 80), (0, 0, 0, 0))
        ImageDraw.Draw(sh).rounded_rectangle([40, 52, 40 + w, 52 + h], 24, fill=(0, 0, 0, 150))
        sh = sh.filter(ImageFilter.GaussianBlur(20))
        x, y = (W - w) // 2, 360
        canvas.paste(sh, (x - 40, y - 40), sh)
        canvas.paste(body, (x, y))
    else:
        crop = frame.crop(AVATAR)
        h = 1110; w = round(crop.width * h / crop.height)
        canvas = panel_fill(frame, h / crop.height, 320)
        body = crop.resize((w, h), Image.LANCZOS).filter(ImageFilter.UnsharpMask(2, 60, 2))
        soft_paste(canvas, body, (W - w) // 2, 320)
    header(canvas, fonts)
    return canvas


def zoomed(img, z):
    """Sub-pixel zoom about the centre, so a slow push never steps."""
    cx, cy = W / 2, H / 2
    return img.transform((W, H), Image.AFFINE, (1 / z, 0, cx - cx / z, 0, 1 / z, cy - cy / z), Image.BICUBIC)


def still_segment(img, seconds, dst, fade_in=False):
    tmp = dst.with_suffix(""); shutil.rmtree(tmp, ignore_errors=True); tmp.mkdir()
    n = round(seconds * FPS)
    for i in range(n):
        u = i / max(n - 1, 1)
        zoomed(img, 1.0 + 0.035 * (1 - (1 - u) ** 2)).save(tmp / f"f{i:04d}.png")
    vf = "format=yuv420p" + (",fade=t=in:st=0:d=0.3" if fade_in else "")
    run("-framerate", FPS, "-i", tmp / "f%04d.png", "-vf", vf, "-c:v", "libx264", "-crf", 18, "-preset", "slow",
        "-r", FPS, dst)
    shutil.rmtree(tmp)


def spin_segment(shots, folder, first, last, fonts, seconds, dst, fade_in=False):
    """12 fps screen-copy frames, composed per frame, then motion-interpolated to 30 fps like the Store trailer."""
    tmp = dst.with_suffix(""); shutil.rmtree(tmp, ignore_errors=True); tmp.mkdir()
    for i, n in enumerate(range(first, last + 1)):
        compose("spin", Image.open(shots / folder / f"f{n:04d}.jpg").convert("RGB"), fonts).save(tmp / f"f{i:04d}.png")
    vf = "minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:vsbmc=1,tpad=stop_mode=clone:stop_duration=1," \
         f"trim=duration={seconds},format=yuv420p" + (",fade=t=in:st=0:d=0.3" if fade_in else "")
    run("-framerate", 12, "-i", tmp / "f%04d.png", "-vf", vf, "-c:v", "libx264", "-crf", 18, "-preset", "slow", dst)
    shutil.rmtree(tmp)


def narration(dst):
    """One continuous track: each clip padded to its scene, then a measured two-pass loudnorm."""
    parts = []
    for i, (*_, clip, seconds) in enumerate(SCENES):
        p = OUT / f"a{i:02d}.wav"
        run("-i", VOICE / f"{clip}.mp3", "-af", f"adelay={int(VOICE_AT * 1000)}:all=1,apad,atrim=0:{seconds}",
            "-ar", 48000, "-ac", 2, p)
        parts.append(p)
    lst = OUT / "audio.txt"; lst.write_text("".join(f"file '{p.name}'\n" for p in parts))
    raw = OUT / "narration-raw.wav"
    run("-f", "concat", "-safe", 0, "-i", lst, "-c", "copy", raw)
    probe = subprocess.run(["ffmpeg", "-hide_banner", "-i", str(raw), "-af",
                            f"loudnorm=I={LUFS}:TP={TRUE_PEAK}:LRA={LRA}:print_format=json", "-f", "null", "-"],
                           capture_output=True, text=True).stderr
    m = json.loads(probe[probe.rindex("{"):probe.rindex("}") + 1])
    run("-i", raw, "-af", f"loudnorm=I={LUFS}:TP={TRUE_PEAK}:LRA={LRA}:measured_I={m['input_i']}:"
        f"measured_TP={m['input_tp']}:measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:"
        f"offset={m['target_offset']}:linear=true", "-ar", 48000, dst)
    for p in parts: p.unlink()
    lst.unlink(); raw.unlink()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--shots", type=Path, default=TOOL / "out" / "shots")
    args = ap.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    fonts = brand_cards.load_fonts(FONTS)
    fonts["header"] = ImageFont.truetype(str(next((FONTS / "spacegrotesk").glob("*.ttf"))), 96)
    fonts["header"].set_variation_by_axes([700])
    outro = OUT / "outro.png"; brand_cards.outro_vertical(outro, fonts)

    segs = []
    for i, (kind, src, first, last, _, seconds) in enumerate(SCENES):
        dst = OUT / f"seg{i:02d}.mp4"
        if kind == "spin":
            spin_segment(args.shots, src, first, last, fonts, seconds, dst)
        elif kind == "template":
            still_segment(compose("template", Image.open(args.shots / src).convert("RGB"), fonts), seconds, dst)
        else:
            still_segment(Image.open(outro).convert("RGB"), seconds, dst)
        segs.append(dst)
    lst = OUT / "video.txt"; lst.write_text("".join(f"file '{s.name}'\n" for s in segs))
    video = OUT / "video.mp4"
    run("-f", "concat", "-safe", 0, "-i", lst, "-c", "copy", video)
    audio = OUT / "narration.wav"; narration(audio)
    run("-i", video, "-i", audio, "-map", "0:v", "-map", "1:a", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
        "-shortest", "-movflags", "+faststart", OUT / "rbx15-tiktok.mp4")
    for s in segs: s.unlink()
    lst.unlink(); video.unlink(); audio.unlink()
    print(f"wrote {OUT / 'rbx15-tiktok.mp4'} ({sum(s[-1] for s in SCENES):.1f} s)")


if __name__ == "__main__":
    main()
