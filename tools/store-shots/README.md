# Store screenshots, art and trailer

Everything in the 5.0 Store listing update: the Neon Tide outfit, the seven screenshots in `docs/screenshots/5.0/`, the super hero art and trailer thumbnail in `docs/store-assets/`, and the trailer in `docs/store-assets/trailer/`. Outputs go to `out/`, which git ignores. Run from this folder.

Neon Tide is an original matching shirt and pants made for the listing. The art layers (the banded sun, the tide waves and stripes) are drawn by `make_art.py`. Everything else is the app's own engine: the region fills, the Word Art (Bungee Shade gradient, Orbitron neon) and the clothing details. The final projects are `docs/store-assets/5.0/Neon_Tide_Shirt.r15proj` and `Neon_Tide_Pants.r15proj`.

## 1. Build the outfit

Needs Python with Pillow, and the .NET SDK from `global.json`.

```powershell
python make_art.py
dotnet run --project OutfitBuilder -c Release -- recipe.json out/outfit/final
dotnet run --project OutfitBuilder -c Release -- recipe-steps.json out/outfit/steps
```

`OutfitBuilder` loads a base project, runs `wordart` and `detail` ops through `RBX15.Engine`, places the results, and writes `<name>.r15proj` with `ProjectIO.Write` plus `<name>.png` with `Renderer.ExportPng`. The output is deterministic: the first command reproduces the two committed projects byte for byte. `recipe-steps.json` writes the four build-up stages the trailer shows (empty, fills, art, details). Before capturing, put each stage in its own folder as `out/outfit/steps/s<n>/Neon_Tide_Shirt.r15proj`, so the window title stays the same.

## 2. Capture

`capture-steps.ps1` launches the Release app on a project, sizes its visible frame to exactly 1920x1080, and runs steps. It uses UI Automation patterns and PrintWindow only, never synthetic input, and it backs up and restores `settings.json`. Steps: `select:<layer>`, `tab:<Home|Background|Details|Text>`, `r15`, `classic`, `wait:<ms>`, `shot:<name>` (PrintWindow, PNG), `screen:<name>` and `clip:<name>:<seconds>:<fps>` (screen copy, JPEG).

```powershell
$exe = "..\..\src\RBX15.App\bin\Release\net10.0-windows\RBX15.App.exe"
.\capture-steps.ps1 -Exe $exe -Project out\outfit\final\Neon_Tide_Shirt.r15proj -Out out\shots `
  -Steps 'select:Neon Sun','shot:02-layers','tab:Background','shot:03-background','tab:Details','shot:04-details','tab:Text','shot:05-word-art'
.\capture-steps.ps1 -Exe $exe -Project out\outfit\final\Neon_Tide_Shirt.r15proj -Out out\shots -Steps 'select:Neon Sun','r15','clip:shirt-spin:19:12'
.\capture-steps.ps1 -Exe $exe -Project out\outfit\final\Neon_Tide_Pants.r15proj -Out out\shots -Steps 'select:Right Leg*','r15','clip:pants-spin:19:12','tab:Background','shot:07-pants-background'
0..3 | ForEach-Object { .\capture-steps.ps1 -Exe $exe -Project "out\outfit\steps\s$_\Neon_Tide_Shirt.r15proj" -Out out\shots -Steps "shot:build-s$_" }
.\capture-steps.ps1 -Exe $exe -Project out\outfit\final\Neon_Tide_Shirt.r15proj -Out out\shots -Steps 'shot:build-s4'
```

Three things this needed:

- **PrintWindow returns a stale 3D preview.** The viewport only updates in a capture when the rest of the window repaints, so stills show the avatar at whatever angle it last redrew. The spin clips copy the window from the screen instead, with the window made topmost for those seconds.
- **A topmost window takes the mouse.** Keep your hands off the machine during a `screen` or `clip` step, or real hovers and clicks land in the app. Clicking another window also dims the caption buttons mid-clip.
- **The spin can't be reset without input.** The preview turns at 20 degrees a second from the front at load, so record a full 19-second turn and pick the segment where the front comes round. `make_assets.py` uses shirt frames 99 to 171 and pants frames 109 to 181. The heroes are shirt frame 116 and pants frame 121. Shirt frame 116 had dimmed caption buttons, so `fix_caption` copies the active ones over from frame 90 (window chrome only).

## 3. Store set, trailer and hero art

Needs ffmpeg, Node 18+, and Python with Pillow and `qrcode`. `demo-video/` is vendored from the demo-video skill and uses Node built-ins only. The narration is Este's ElevenLabs voice (ItsJustEste, voice `8gpP3pZU3NERlkDEdxub`, adapter `demo-video/adapters/elevenlabs.mjs`). The ten clips are committed in `voiceover/`, and because the pipeline is cache-first, a render with those files present never calls ElevenLabs. To re-voice a line, delete its `slide_NN.mp3`, edit the line in `manifest.json`, and render with `ELEVENLABS_API_KEY` set.

```powershell
python make_assets.py                      # out/store (7 screenshots), out/trailer (captioned stills, brand cards, clips)
node demo-video/build.mjs manifest.json    # out/rbx15-trailer.mp4, 48 s, 1920x1080, 30 fps, narrated
ffmpeg -i out/rbx15-trailer.mp4 -c:v copy -af "loudnorm=I=-16:TP=-1.5:LRA=11" -c:a aac -b:a 192k -ar 48000 -ac 2 -movflags +faststart out/rbx15-trailer-final.mp4
node demo-video/contact-sheet.mjs out/rbx15-trailer.mp4 --frames 2,8,16,23,31
python make_hero.py                        # out/store/super-hero-art-1920x1080.png and the trailer thumbnail
```

The trailer opens and closes on the 626 Labs brand cards from `brand_cards.py`, in the RoRoRo launch video's layout. The intro has the app icon tile, the RBX15 wordmark and Este saying the name. The outro has a QR code to 626labs.dev/rbx15.html and the "RBX15, from six-two-six Labs. Imagine something else." sign-off, with the "626 LABS / IMAGINE SOMETHING ELSE." footer on both. The ffmpeg step normalizes the voice to -16 LUFS. Without it the narration sits around -25 dB, quiet enough to sound silent on laptop speakers.

Partner Center refuses desktop screenshots under 1366x768. `StoreAssetsTests.Every_versioned_screenshot_meets_the_store_minimum` checks every `docs/screenshots/<version>/` image. The super hero art must not carry the product title. Screenshots, the hero art and the trailer are uploaded by hand in Partner Center, because the submission API can't upload media.
