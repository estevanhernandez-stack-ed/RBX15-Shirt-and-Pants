# Frozen 4.1 source

These four files are byte-for-byte copies of `editor.js`, `editor.html`, `styles.css` and `main.js` at the `v4.1.0` tag. The cutover deleted the Electron app, but a few tests derive their expected values from 4.1's own text, so they read these copies instead:

- `DialogInventoryTests`: the six dialog sites in `editor.js`, and none in `editor.html` or `main.js`.
- `ThemeTokenTests`: every `:root` token in `styles.css`.
- `ShellTests`, `ProjectFileServiceTests`, `LayersReorderTests`, `WordArtHardeningTests`: titles, dialog text, filters and the Word Art font list.

They are references, not an app: there is no `lib/` and no `package.json`, so they do not run. Like the golden images, they never change. `.gitattributes` marks `tests/goldens/**` as `-text`, which keeps the line numbers the tests cite. Read one with `RepoRoot.Source41(AppContext.BaseDirectory, "editor.js")`.
