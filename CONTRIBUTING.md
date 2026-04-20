# Contributing to RBX15 Classic Shirt and Pants Maker

Builder-to-builder. Bring a patch, ship a patch. Here's what you need.

## Getting Started

```bash
git clone https://github.com/estevanhernandez-stack-ed/RBX15-Shirt-and-Pants.git
cd RBX15-Shirt-and-Pants
npm install
npm start
```

Requires **Node.js 18+** and **Electron 41**. No build step needed for development — `npm start` launches the Electron shell directly against the source files. Rebuilding the installer for distribution is `npm run build` (produces `dist/`).

## Architecture

Four files. No frameworks, no bundler, no transpilation. See [docs/superpowers/specs/2026-04-02-v4-polish-design.md](docs/superpowers/specs/2026-04-02-v4-polish-design.md) for the rationale.

| File | Responsibility |
|------|----------------|
| [`main.js`](main.js) | Electron main process. Window creation, menu bar handling, ~40 lines. |
| [`editor.html`](editor.html) | Markup only — header, ribbon, panels, canvas, footer. Links external CSS + JS. |
| [`styles.css`](styles.css) | All CSS. Dark-mode base, neon accents, gradient panels. |
| [`editor.js`](editor.js) | All application logic. Render pipeline, layer system, drawing tools, word art, file I/O, asset browser. |

`editor.js` is organized by section with `═══` banner comments. Key sections:

- **R15 template constants** — `SHIRT_REGIONS`, `PANTS_REGION_DATA`, `BG_COLORS`
- **State** — `layers`, `selectedLayerId`, `bgColor`, tool state, drag state
- **Layer factory & helpers** — `makeLayer()`, `autocrop()`
- **Image adjustment helpers** — `applyRGBShift()`, `hasAdjustments()`, `drawLayerWithAdjustments()`
- **Render** — `render()`, `makeCheckerboard()` (cached), cursor updates
- **Tool switching**, **Layers**, **UI**, **BG Swatches**, **File drop**
- **Canvas mouse** — unified mousedown/move/up handler for select + drawing tools
- **Toolbar actions**, **Export**, **Region grid**, **Mode switch**, **Presets**
- **Keyboard shortcuts**, **Detail generators** (collars, cuffs, seams, etc.)
- **Word art**, **Asset library**, **Save / Load project**

## Easy first contributions

### Add a clothing detail

The easiest way to contribute. Open `editor.js`, find `DETAIL_GENERATORS`, and add an entry:

```javascript
'my-detail': (c) => {
  // c is the 2D canvas context, pre-configured with strokeStyle, lineWidth, etc.
  const f = SHIRT_REGIONS.torso_front;
  // draw whatever you want within the template
  c.beginPath();
  c.moveTo(f.x, f.y);
  c.lineTo(f.x + f.w, f.y + f.h);
  c.stroke();
},
```

Then add a button in `editor.html`'s Details tab:

```html
<button class="preset-btn detail-btn" data-detail="my-detail">My Detail</button>
```

Add a friendly label in `editor.js`'s `names` map inside the `detail-btn` click handler. Done.

### Add a word art style

Open `editor.js`, find `renderWordArt()`, and add a branch to the `if (waStyle === ...)` tree for your new style. Add the style button in `editor.html`'s Text tab:

```html
<button class="wordart-style-btn" data-style="mystyle">My Style</button>
```

### Add a placement preset

Find the `preset-btn` click handler in `editor.js`. Add an `else if (preset === 'mypreset')` branch that does the layer manipulation. Add the button in the Layout tab in `editor.html`.

## Pull Request Guidelines

- **One feature per PR.** Easier to review, easier to revert.
- **Update CHANGELOG.md** under the `## [Unreleased]` section with your change.
- **Test it by hand.** This is a UI tool — click through the feature, drop an image, save a project, export a PNG. Confirm it still works end-to-end.
- **Don't introduce a build step.** No TypeScript, no bundler, no SASS. The entire appeal of this codebase is that you can open `editor.js` and read it.
- **Don't restructure beyond the scope of your change.** If the file grows unwieldy, raise it in an issue first.

## Code Style

- **No semicolons-on-next-line drama** — existing code uses semicolons, match it.
- **`const` by default**, `let` when you must, never `var`.
- **`snake_case` for region keys** (`torso_front`, `larm_back`) — they're data, not identifiers.
- **`camelCase` for functions and variables.**
- **Minimal comments.** Name things well enough that comments aren't needed. Keep comments for *why*, not *what*.
- **Event listeners over `onclick` properties** where you need multiple handlers on the same element.
- **`document.createElement` + `appendChild`** is preferred over `innerHTML` when composing DOM for anything that includes dynamic values — safer, more predictable.

## What we're looking for

Check the [roadmap in README.md](README.md#roadmap). High-signal contributions:

- 626 Labs design-system pass (in flight — see [docs/superpowers/specs/](docs/superpowers/specs/))
- Additional word art styles and fonts
- Accessibility: keyboard navigation for the canvas, screen-reader-friendly controls
- Test harness (no test framework currently — Electron + vanilla JS makes this non-trivial, but happy to accept proposals)
- Bug reports with repro steps from real Roblox upload failures

## License

By contributing, you agree your code is released under the [MIT License](LICENSE). Built by [626 Labs LLC](https://626labs.dev).
