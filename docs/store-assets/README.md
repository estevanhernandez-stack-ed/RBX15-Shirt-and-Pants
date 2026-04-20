# Store assets

Microsoft Store Partner Center submission assets for RBX15 Classic Shirt and Pants Maker.

## What's here

### Product art (upload to the app's Store Listing → Store logos step)

| File | Dimensions | Partner Center slot |
|---|---|---|
| `store-box-1080x1080.png` | 1080 x 1080 | 1:1 Box art |
| `store-box-2160x2160.png` | 2160 x 2160 | 1:1 Box art (hi-res) |
| `store-poster-720x1080.png` | 720 x 1080 | 9:16 Poster art |
| `store-poster-1440x2160.png` | 1440 x 2160 | 9:16 Poster art (hi-res) |

These carry the **RBX15** product mark — brand-navy base, cyan + magenta gradient "RBX" wordmark, "CLASSIC R15 / SHIRT & PANTS MAKER" eyebrows, feature chips on the poster, 626 Labs LLC publisher chip. Upload to the *individual app's* Store logos step, not to publisher-level branding.

### App tiles (Store display images step, optional — Store falls back to MSIX tiles if omitted)

| File | Dimensions | Partner Center slot |
|---|---|---|
| `app-tile-71x71.png` | 71 x 71 | 1:1 71x71 |
| `app-tile-150x150.png` | 150 x 150 | 1:1 150x150 |
| `app-tile-300x300.png` | 300 x 300 | 1:1 App tile icon (300x300) |

### Publisher logos (reference only — use at the publisher/account level, not per-app)

| File | Dimensions | Notes |
|---|---|---|
| `logo-square-1080x1080.png` | 1080 x 1080 | 626 Labs company logo on navy |
| `logo-portrait-720x1080.png` | 720 x 1080 | 626 Labs company logo on navy |

⚠️ Don't upload the publisher logos to an app's Store Listing. Partner Center's "Store logos" on the app submission is for the **product** (use `store-box-*` and `store-poster-*` instead).

## Regenerating

```bash
cd docs/store-assets
python make-store-assets.py
```

Requires **Python 3.8+** and **Pillow**. The script uses Windows system fonts (Segoe UI Black / Bold) on Windows, DejaVu Sans Bold on Linux, and SF on macOS.

### Pointing at a different company logo

The script defaults to `~/.claude/skills/626labs-design/assets/626Labs-logo.png` (installed with the 626labs-design Claude Code skill). If you don't have the skill installed:

```bash
python make-store-assets.py --company /path/to/626Labs-logo.png
```

## App tile design rationale

- **Gradient background:** navy-deep base with a soft cyan radial glow top-left and magenta radial glow bottom-right. Matches the app's in-window background pattern.
- **Primary mark "RBX":** Space Grotesk / Segoe UI Black at 42% of tile height, centered, white.
- **Secondary mark "15":** cyan, 16% of tile height, sitting just below the RBX. Anchors the "Classic R15" identity without being literal.
- **Inner hairline stroke:** 3% inset, 3% opacity white — keeps the tile from feeling edge-bled when placed against Microsoft's own tile backgrounds.

## Design system reference

All color values come from `~/.claude/skills/626labs-design/colors_and_type.css`:

```
--brand-navy-deep:   #0f1f31
--brand-navy:        #192e44
--brand-cyan:        #17d4fa
--brand-magenta:     #f22f89
```
