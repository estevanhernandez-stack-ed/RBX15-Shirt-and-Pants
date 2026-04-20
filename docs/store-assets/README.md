# Store assets

Microsoft Store Partner Center submission assets for ItsjustEste's RBX Classic Shirt and Pants Maker.

## What's here

| File | Dimensions | Purpose | Source |
|---|---|---|---|
| `app-tile-71x71.png` | 71 x 71 | Small tile in Store listings | Generated |
| `app-tile-150x150.png` | 150 x 150 | Medium tile / default listing art | Generated |
| `app-tile-300x300.png` | 300 x 300 | Large tile | Generated |
| `logo-square-1080x1080.png` | 1080 x 1080 | Publisher mark (square) | 626 Labs logo on navy |
| `logo-portrait-720x1080.png` | 720 x 1080 | Publisher mark (portrait) | 626 Labs logo on navy |

The app tiles carry *this specific product* identity (RBX Maker). The publisher logos carry *626 Labs LLC*. Partner Center asks for both — don't mix them up.

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
