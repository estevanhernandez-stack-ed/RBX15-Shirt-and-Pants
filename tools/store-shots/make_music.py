"""Score the trailer and the TikTok cut with ElevenLabs Music, then mix it under the narration.

The track is composed to the video's own scene timing (a v2 composition plan enforces each chunk's duration), so the
build-up and the reveal land on the cuts. Instrumental only. The key is ELEVENLABS_API_KEY from the environment.
Generated tracks are cached in music/, so a re-mix never calls the API; delete a file to recompose it. Creator is a
paid plan, which ElevenLabs' music terms clear for commercial use online, the Store listing and social posts included.

    python make_music.py trailer <narrated.mp4> <out.mp4>
    python make_music.py tiktok  <narrated.mp4> <out.mp4>
"""
import json, os, subprocess, sys, urllib.request
from pathlib import Path

TOOL = Path(__file__).parent
CACHE = TOOL / "music"
STYLE = ["synthwave", "retro 80s", "warm analog synth pads", "pulsing bass", "gated drums", "neon", "instrumental"]
AVOID = ["vocals", "singing", "spoken word", "harsh distortion", "abrupt ending"]

PLANS = {
    # scene boundaries of the 48.1 s Store trailer
    "trailer": [
        ("[Intro] soft filtered pads and a slow arpeggio, gentle, leaves room for a voice", 5500, ["ambient", "filtered"]),
        ("[Build] the beat comes in, steady mid-tempo pulse, layers add one at a time", 18800, ["driving", "building"]),
        ("[Reveal] full chorus, bright lead synth, the payoff", 16000, ["uplifting", "full", "bright lead"]),
        ("[Outro] drums drop out, pads and arpeggio resolve and sustain to a clean finish", 7800, ["resolving", "calm"]),
    ],
    # the 30.8 s TikTok cut. It loops, so the end returns to the energy of the start rather than fading out.
    "tiktok": [
        ("[Hook] full energy straight away, bright lead synth over a driving beat", 5100, ["punchy", "bright lead"]),
        ("[Verse] steady driving groove, a little lighter, leaves room for a voice", 15300, ["driving", "groove"]),
        ("[Chorus] lift, full and bright, the payoff", 4400, ["uplifting", "full"]),
        ("[Tag] keeps the groove going at full energy into the next bar, no fade, no ending", 6000, ["driving", "seamless loop"]),
    ],
}


def compose(name):
    dst = CACHE / f"{name}.mp3"
    if dst.exists():
        return dst
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key:
        sys.exit("ELEVENLABS_API_KEY is not set, and there is no cached music/" + dst.name)
    body = {"model_id": "music_v2", "composition_plan": {"chunks": [
        {"text": text, "duration_ms": ms, "positive_styles": STYLE + extra, "negative_styles": AVOID}
        for text, ms, extra in PLANS[name]]}}
    req = urllib.request.Request("https://api.elevenlabs.io/v1/music?output_format=mp3_48000_192",
                                 data=json.dumps(body).encode(), method="POST",
                                 headers={"xi-api-key": key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=600) as r:
            audio = r.read()
    except urllib.error.HTTPError as e:
        sys.exit(f"ElevenLabs Music failed: {e.code} {e.read().decode(errors='replace')}")
    CACHE.mkdir(exist_ok=True)
    dst.write_bytes(audio)
    return dst


def mix(video, music, dst, lufs, loop=False):
    """Music 18 dB under, ducked a further step while the voice speaks, then the whole mix leveled once. A looping cut
    dips the music at the seam: the composed track starts about 6 LU quieter than it ends, which would jump each loop."""
    dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(video)],
                               capture_output=True, text=True).stdout)
    fades = f"afade=t=in:d=0.25,afade=t=out:st={dur - 0.6:.2f}:d=0.6," if loop else ""
    graph = (f"[1:a]{fades}volume=-18dB,aresample=48000[m];[0:a]aresample=48000,asplit[v][sc];"
             "[m][sc]sidechaincompress=threshold=0.03:ratio=6:attack=20:release=400[duck];"
             f"[v][duck]amix=inputs=2:duration=first:normalize=0,loudnorm=I={lufs}:TP=-1.5:LRA=11[a]")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(video), "-i", str(music), "-filter_complex", graph,
                    "-map", "0:v", "-map", "[a]", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
                    "-movflags", "+faststart", str(dst)], check=True)


if __name__ == "__main__":
    name, video, out = sys.argv[1], Path(sys.argv[2]), Path(sys.argv[3])
    mix(video, compose(name), out, -16 if name == "trailer" else -17, loop=name == "tiktok")
    print("wrote", out)
