#!/usr/bin/env bash
# Vendors the Word Art fonts (editor.html:11) and UI fonts (styles.css:6) into assets/fonts from
# ONE pinned google/fonts commit. Each file is checked against the git blob id GitHub lists for it
# at that commit (--no-filters: five upstream OFL.txt files are CRLF), so a download that is not
# byte-identical to the pinned tree fails loudly. Re-pinning is a controller decision: see
# assets/fonts/README.md.
#   bash tools/fonts/fetch-google-fonts.sh
set -euo pipefail
COMMIT=66928797f271fed5ee428544e7229685e2d90127
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DEST="$ROOT/assets/fonts"
# Paths below are relative to DEST: Git Bash hands Windows curl an unconverted /c/... path when it
# contains [ ], and the write fails (curl 23).
mkdir -p "$DEST"
cd "$DEST"
# <path in google/fonts> <path under assets/fonts> <git blob id at COMMIT>
FILES=(
  "ofl/anton/Anton-Regular.ttf anton/Anton-Regular.ttf 4d65707db9d8663ccfa99cd28cd3a4e0025be178"
  "ofl/anton/OFL.txt anton/OFL.txt 93feddee37145696e123ea65a6772cab09663f9e"
  "ofl/bebasneue/BebasNeue-Regular.ttf bebasneue/BebasNeue-Regular.ttf c328c6e08b20a20a1de47d823e007ee73812a438"
  "ofl/bebasneue/OFL.txt bebasneue/OFL.txt da9571488f44176ef90d7f10c0f402c8be74db67"
  "ofl/dancingscript/DancingScript[wght].ttf dancingscript/DancingScript[wght].ttf 9f521e7fb357ec9ea9820ebd36a9d7b1f72f2935"
  "ofl/dancingscript/OFL.txt dancingscript/OFL.txt 07ff8df965f089a2b89071b3d46cfd15777634a6"
  "ofl/greatvibes/GreatVibes-Regular.ttf greatvibes/GreatVibes-Regular.ttf a1327ff3b862246f1f253639040edcc473e5f3a9"
  "ofl/greatvibes/OFL.txt greatvibes/OFL.txt 70025926f5684ebed9d119424b178faa4e0a5cc6"
  "ofl/pacifico/OFL.txt pacifico/OFL.txt 33864cc8de4dd3ea8f3daab1154be28577918bae"
  "ofl/pacifico/Pacifico-Regular.ttf pacifico/Pacifico-Regular.ttf 27765d02ac13f241b1e207dbda7b2dd1b68280da"
  "ofl/creepster/Creepster-Regular.ttf creepster/Creepster-Regular.ttf 668c59cf43b545294b1fd778ee0cd0f4e18cd02c"
  "ofl/creepster/OFL.txt creepster/OFL.txt 92bc9bee87bafcb1a3ccc6854ba98683411ae7e0"
  "ofl/poppins/OFL.txt poppins/OFL.txt 76df3b565672e3248a5715339d092d4cb6c75019"
  "ofl/poppins/Poppins-Black.ttf poppins/Poppins-Black.ttf 6612e5228fcdeabd840f4ca17de889ca447e22f4"
  "ofl/poppins/Poppins-Bold.ttf poppins/Poppins-Bold.ttf 1982f38ab21303459aa1155265052ca599fa58d1"
  "ofl/lato/Lato-Black.ttf lato/Lato-Black.ttf 53a31ac8f56e9e3f67d35844f2b41ef9ade0e156"
  "ofl/lato/OFL.txt lato/OFL.txt 98383e3d835a5c5aea988a74b569193a30dcbcec"
  "ofl/bangers/Bangers-Regular.ttf bangers/Bangers-Regular.ttf 9b0f8c1f1b45e7bb4971b7d6a8604f9aacacdd32"
  "ofl/bangers/OFL.txt bangers/OFL.txt bf717d4be410cf7616b9bd345b40ab4817a26e6f"
  "apache/permanentmarker/LICENSE.txt permanentmarker/LICENSE.txt d645695673349e3947e8e5ae42332d0ac3164cd7"
  "apache/permanentmarker/PermanentMarker-Regular.ttf permanentmarker/PermanentMarker-Regular.ttf 3218fc5b3d395c3a3fc207fdf17a27bb32af3242"
  "ofl/bungeeshade/BungeeShade-Regular.ttf bungeeshade/BungeeShade-Regular.ttf 80ada80a0404713e7cd31a6b7982ff7a549acdfe"
  "ofl/bungeeshade/OFL.txt bungeeshade/OFL.txt 6f47072f01b8698301896b247b7087c023a4da2d"
  "ofl/rubikmonoone/OFL.txt rubikmonoone/OFL.txt 7e7e709034ebad08173011538f60ccbd9ce3ce07"
  "ofl/rubikmonoone/RubikMonoOne-Regular.ttf rubikmonoone/RubikMonoOne-Regular.ttf 1923947b912685017b6f8ddcca5766f322d89c29"
  "ofl/pressstart2p/OFL.txt pressstart2p/OFL.txt 22796dfe6e3784d69d255b0bf253e2b16f3281de"
  "ofl/pressstart2p/PressStart2P-Regular.ttf pressstart2p/PressStart2P-Regular.ttf 39adf42efa597906e53be689474ac82214112124"
  "ofl/righteous/OFL.txt righteous/OFL.txt 22f3d202eb01975fbcd48a25f3ad82ab624986cb"
  "ofl/righteous/Righteous-Regular.ttf righteous/Righteous-Regular.ttf 07fc0b45132b1d7039c202e6d09c91b1a137ec26"
  "ofl/orbitron/OFL.txt orbitron/OFL.txt 91ede459e18c22d3f2ba7c27d925b49cc945abe4"
  "ofl/orbitron/Orbitron[wght].ttf orbitron/Orbitron[wght].ttf 1fc54ce7116a6ec549f37358466bcc92f9a72c33"
  "ofl/spacegrotesk/OFL.txt spacegrotesk/OFL.txt cb512b9af44ff61e75e1aad387b7424cdfab36a3"
  "ofl/spacegrotesk/SpaceGrotesk[wght].ttf spacegrotesk/SpaceGrotesk[wght].ttf a1b2e6c26093066510a31147e7aec9abdc8d6c5e"
  "ofl/inter/Inter[opsz,wght].ttf inter/Inter[opsz,wght].ttf 047c92f6e2212473dc436020afed689527076d44"
  "ofl/inter/OFL.txt inter/OFL.txt 21f6aff961064c2e429f570995e446bcdd555422"
  "ofl/jetbrainsmono/JetBrainsMono[wght].ttf jetbrainsmono/JetBrainsMono[wght].ttf aa310be8b717fe3774f9444dd89d5f4101cc6d10"
  "ofl/jetbrainsmono/OFL.txt jetbrainsmono/OFL.txt 821a3dac22aff15a1f1c9689a1d79c45bb58ca39"
)
for row in "${FILES[@]}"; do
  read -r src dst blob <<< "$row"
  mkdir -p "$(dirname "$dst")"
  # Brackets and commas are percent-encoded: curl treats [] as a glob.
  enc="$(printf '%s' "$src" | sed 's/\[/%5B/g; s/\]/%5D/g; s/,/%2C/g')"
  curl -sSf -o "$dst" "https://raw.githubusercontent.com/google/fonts/$COMMIT/$enc"
  got="$(git hash-object --no-filters "$dst")"
  if [ "$got" != "$blob" ]; then
    echo "MISMATCH $dst: got $got, want $blob" >&2
    exit 1
  fi
  echo "ok $dst"
done
echo "fetched ${#FILES[@]} files at google/fonts@$COMMIT"
