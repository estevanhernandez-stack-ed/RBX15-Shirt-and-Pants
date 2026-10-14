# Vendored fonts

The 15 Word Art families 4.1 loads from Google Fonts (`editor.html:11`) and the 3 UI families from `styles.css:6`, vendored as TTF from one pinned commit: **google/fonts@66928797f271fed5ee428544e7229685e2d90127** (main, 2026-10-12).

Goldens v2 (`tests/goldens/app`) are captured with exactly these files injected as `@font-face` by `tools/parity-ref/fonts.js`, and the v5 bundle ships the same files. Google Fonts' served files drift over time; these do not.

## Rules

- Files change only on purpose. That means re-pinning the commit in `tools/fonts/fetch-google-fonts.sh`, re-running it, regenerating every row below, and re-freezing every Word Art golden. `VendoredFontsTests` fails on any byte that does not match this table.
- Files are stored byte-for-byte (`.gitattributes`: `assets/fonts/** -text`). Five upstream `OFL.txt` files use CRLF line endings.
- 4.1 always draws Word Art at weight 900 (`editor.js:2131`). Chromium synthesizes bold for faces below 600. Dancing Script's 700 face and the 900 faces of Poppins, Lato and Orbitron are real weights. A variable file serves each requested weight with its `wght` axis set to that weight.
- Permanent Marker is licensed Apache-2.0 (google/fonts keeps it under `apache/`), so its directory carries `LICENSE.txt`. Every other family is SIL OFL 1.1, with `OFL.txt`.

## Manifest

| File | Family | 4.1 requests | Source at the pinned commit | Kind | Git blob | SHA-256 |
| --- | --- | --- | --- | --- | --- | --- |
| `anton/Anton-Regular.ttf` | Anton | 400 | `ofl/anton/Anton-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `4d65707db9d8663ccfa99cd28cd3a4e0025be178` | a4ba3a92350ebb031da0cb47630ac49eb265082ca1bc0450442f4a83ab947cab |
| `anton/OFL.txt` | Anton | license | `ofl/anton/OFL.txt` | SIL OFL 1.1 | `93feddee37145696e123ea65a6772cab09663f9e` | ee67e6ee22790b7929f1a3769ca2801d565c64b5a9096942c1adf5596de9c9e4 |
| `bebasneue/BebasNeue-Regular.ttf` | Bebas Neue | 400 | `ofl/bebasneue/BebasNeue-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `c328c6e08b20a20a1de47d823e007ee73812a438` | 08e4623805102d819f58601e46e345648846075e363b2ceb23313c2d1c83ec73 |
| `bebasneue/OFL.txt` | Bebas Neue | license | `ofl/bebasneue/OFL.txt` | SIL OFL 1.1 | `da9571488f44176ef90d7f10c0f402c8be74db67` | 72082f6cb4d04be2ecf7cc7d9e1e7d73787f0af8a5a278a47cade70c16b78341 |
| `dancingscript/DancingScript[wght].ttf` | Dancing Script | 700 | `ofl/dancingscript/DancingScript[wght].ttf` | variable, wght 400-700; served as the 700 face | `9f521e7fb357ec9ea9820ebd36a9d7b1f72f2935` | 21808625578fe8d8cd10cb684be546dca077b27cd03a53a2f1ec11dc743c924c |
| `dancingscript/OFL.txt` | Dancing Script | license | `ofl/dancingscript/OFL.txt` | SIL OFL 1.1 | `07ff8df965f089a2b89071b3d46cfd15777634a6` | 6f090277c00af96651ce6dbcc38ff1591047a3bffef486e80b6a32e8276a8201 |
| `greatvibes/GreatVibes-Regular.ttf` | Great Vibes | 400 | `ofl/greatvibes/GreatVibes-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `a1327ff3b862246f1f253639040edcc473e5f3a9` | 8d509802186f1b51572531ecf313e8098f9a5bfdfaca93f0c9b34467f9982d15 |
| `greatvibes/OFL.txt` | Great Vibes | license | `ofl/greatvibes/OFL.txt` | SIL OFL 1.1 | `70025926f5684ebed9d119424b178faa4e0a5cc6` | 61093a21f5e63dedf54222b3c09997e54c0fe43e3851d21386e02ddcbc246d49 |
| `pacifico/OFL.txt` | Pacifico | license | `ofl/pacifico/OFL.txt` | SIL OFL 1.1 | `33864cc8de4dd3ea8f3daab1154be28577918bae` | a47e5daeda73568969395c656823102678f2eefb0d7d7ecb47aac4cc17e42204 |
| `pacifico/Pacifico-Regular.ttf` | Pacifico | 400 | `ofl/pacifico/Pacifico-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `27765d02ac13f241b1e207dbda7b2dd1b68280da` | 5b6c0d5334a7bf77dea52b975c5a0c408878c0f7115ed5b6fb151f634b7bf701 |
| `creepster/Creepster-Regular.ttf` | Creepster | 400 | `ofl/creepster/Creepster-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `668c59cf43b545294b1fd778ee0cd0f4e18cd02c` | 402aeb734586c74aecd3dbdc454589b1fb12e2e1c71f782fd019ae68066d9f44 |
| `creepster/OFL.txt` | Creepster | license | `ofl/creepster/OFL.txt` | SIL OFL 1.1 | `92bc9bee87bafcb1a3ccc6854ba98683411ae7e0` | c09e3b81d9cfeec46bc29a3374000e8ad013d694bdf24a288088491dd4cd5c69 |
| `poppins/OFL.txt` | Poppins | license | `ofl/poppins/OFL.txt` | SIL OFL 1.1 | `76df3b565672e3248a5715339d092d4cb6c75019` | 6be04893d770899a015649c7aa3b582f871b272f8747a92b78b17c3e5c8b2573 |
| `poppins/Poppins-Black.ttf` | Poppins | 900 | `ofl/poppins/Poppins-Black.ttf` | static, wght 900 | `6612e5228fcdeabd840f4ca17de889ca447e22f4` | d82aaaf98a9283f9a8edd24e51173337d8eaf09e25cd3d98831f8ec8461748a1 |
| `poppins/Poppins-Bold.ttf` | Poppins | 700 | `ofl/poppins/Poppins-Bold.ttf` | static, wght 700 | `1982f38ab21303459aa1155265052ca599fa58d1` | 983676516167748b74de6f4771fb384c664fd913acb8b471122ecacf5da5ea6c |
| `lato/Lato-Black.ttf` | Lato | 900 | `ofl/lato/Lato-Black.ttf` | static, wght 900 | `53a31ac8f56e9e3f67d35844f2b41ef9ade0e156` | 808c62839c62dbce7de689af7603666fc7f8b81e0df537d8a5212c87580d4337 |
| `lato/OFL.txt` | Lato | license | `ofl/lato/OFL.txt` | SIL OFL 1.1 | `98383e3d835a5c5aea988a74b569193a30dcbcec` | 74ba064d03f1f1c4a952da936c3eb71866c34404916734de3cae73b34357e59e |
| `bangers/Bangers-Regular.ttf` | Bangers | 400 | `ofl/bangers/Bangers-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `9b0f8c1f1b45e7bb4971b7d6a8604f9aacacdd32` | 4160a7311de9342674cce9160cde9fcbb30f48190397d86ff1b70b455af65824 |
| `bangers/OFL.txt` | Bangers | license | `ofl/bangers/OFL.txt` | SIL OFL 1.1 | `bf717d4be410cf7616b9bd345b40ab4817a26e6f` | 630dd5a307c0657b094d324e069b390edf90182fe5003639e06c02a0e5769af7 |
| `permanentmarker/LICENSE.txt` | Permanent Marker | license | `apache/permanentmarker/LICENSE.txt` | Apache-2.0 | `d645695673349e3947e8e5ae42332d0ac3164cd7` | cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30 |
| `permanentmarker/PermanentMarker-Regular.ttf` | Permanent Marker | 400 | `apache/permanentmarker/PermanentMarker-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `3218fc5b3d395c3a3fc207fdf17a27bb32af3242` | 28f82c8a7943cb8e9d599f8554da1d4fc75dbcf69b9885ad6c0611d20c6946c5 |
| `bungeeshade/BungeeShade-Regular.ttf` | Bungee Shade | 400 | `ofl/bungeeshade/BungeeShade-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `80ada80a0404713e7cd31a6b7982ff7a549acdfe` | 665c626082682b462f4e696308eafa7071c114da8f8ff3fb73a289c7ff7df482 |
| `bungeeshade/OFL.txt` | Bungee Shade | license | `ofl/bungeeshade/OFL.txt` | SIL OFL 1.1 | `6f47072f01b8698301896b247b7087c023a4da2d` | d5787a50dde5be6c6daecab9ed459939b1bc37cff0d0e00257eaf1ec2cf4c16c |
| `rubikmonoone/OFL.txt` | Rubik Mono One | license | `ofl/rubikmonoone/OFL.txt` | SIL OFL 1.1 | `7e7e709034ebad08173011538f60ccbd9ce3ce07` | 43d530580461a574f6dfed9e15af6a74e95f7c04d9bfa1174a63ff036e8eee07 |
| `rubikmonoone/RubikMonoOne-Regular.ttf` | Rubik Mono One | 400 | `ofl/rubikmonoone/RubikMonoOne-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `1923947b912685017b6f8ddcca5766f322d89c29` | b22624b6a36e543e942106edfccd198c50b57b65b924bfe02a6f0a5699429e6b |
| `pressstart2p/OFL.txt` | Press Start 2P | license | `ofl/pressstart2p/OFL.txt` | SIL OFL 1.1 | `22796dfe6e3784d69d255b0bf253e2b16f3281de` | 705960c3281a5765ecc0b59bd4ed7ca59eed165748076bc2fc3e8fdbfeb944b0 |
| `pressstart2p/PressStart2P-Regular.ttf` | Press Start 2P | 400 | `ofl/pressstart2p/PressStart2P-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `39adf42efa597906e53be689474ac82214112124` | 034c77f1f05ec89421e4a63f0e3a4ca1ecf852cc6d2bf611f126f275728e017d |
| `righteous/OFL.txt` | Righteous | license | `ofl/righteous/OFL.txt` | SIL OFL 1.1 | `22f3d202eb01975fbcd48a25f3ad82ab624986cb` | 1c5bc055869d5e9151ec7774082727db720ec4293c65be67f91a4bdcaeb02998 |
| `righteous/Righteous-Regular.ttf` | Righteous | 400 | `ofl/righteous/Righteous-Regular.ttf` | static, wght 400 (synthetic bold at 900) | `07fc0b45132b1d7039c202e6d09c91b1a137ec26` | 2ffb3fe5c27d7e6571210b800448c4e234e651b46c6b4426c1bb567e5341348a |
| `orbitron/OFL.txt` | Orbitron | license | `ofl/orbitron/OFL.txt` | SIL OFL 1.1 | `91ede459e18c22d3f2ba7c27d925b49cc945abe4` | ab609b0e110d622435ff337cdf233288556e011bbf9bd0550be98846c0630819 |
| `orbitron/Orbitron[wght].ttf` | Orbitron | 700, 900 | `ofl/orbitron/Orbitron[wght].ttf` | variable, wght 400-900; served as the 700 and 900 faces | `1fc54ce7116a6ec549f37358466bcc92f9a72c33` | f42db2dd16e642258e35782916eceb1dcdbea06fb958d77ad71dc5963587e8fd |
| `spacegrotesk/OFL.txt` | Space Grotesk | license | `ofl/spacegrotesk/OFL.txt` | SIL OFL 1.1 | `cb512b9af44ff61e75e1aad387b7424cdfab36a3` | 564ce565c371c5e5bbf286006565a7c9aa55a9f56e7ca58d56e05d649dd61a72 |
| `spacegrotesk/SpaceGrotesk[wght].ttf` | Space Grotesk | 400, 500, 600, 700 | `ofl/spacegrotesk/SpaceGrotesk[wght].ttf` | variable, wght 300-700 (UI) | `a1b2e6c26093066510a31147e7aec9abdc8d6c5e` | acad6de1fc93436f5c0f1f4137751ef04f1aea3063e7036535970ffcfbd79f72 |
| `inter/Inter[opsz,wght].ttf` | Inter | 400, 500, 600, 700 | `ofl/inter/Inter[opsz,wght].ttf` | variable, opsz 14-32, wght 100-900 (UI) | `047c92f6e2212473dc436020afed689527076d44` | 29160a80ff49ddcab2c97711247e08b1fab27a484a329ce8b813d820dc559031 |
| `inter/OFL.txt` | Inter | license | `ofl/inter/OFL.txt` | SIL OFL 1.1 | `21f6aff961064c2e429f570995e446bcdd555422` | 5b9321a4298cfeb6b34354164a1c3afc3db114569984c502b9b35d988fd58c57 |
| `jetbrainsmono/JetBrainsMono[wght].ttf` | JetBrains Mono | 400, 500, 600 | `ofl/jetbrainsmono/JetBrainsMono[wght].ttf` | variable, wght 100-800 (UI) | `aa310be8b717fe3774f9444dd89d5f4101cc6d10` | 48715a42ec242c21e9f02692891e147d022299a52e48d5e413e1a942193ffeda |
| `jetbrainsmono/OFL.txt` | JetBrains Mono | license | `ofl/jetbrainsmono/OFL.txt` | SIL OFL 1.1 | `821a3dac22aff15a1f1c9689a1d79c45bb58ca39` | b2fe5e8987594e9ffd1d2ca52a2f5d73eb8335243893c5d6254b5ad69269591d |
