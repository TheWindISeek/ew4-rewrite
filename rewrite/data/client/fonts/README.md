# Offline UI Fonts

All six WOFF2 faces are bundled, without glyph subsetting or system font aliases.
Noto Serif SC Regular 400 is the primary face. Jigmo, Jigmo2 and Jigmo3 cover rare
Han characters; GNU Unifont and Unifont Upper 17.0.05 supplement other scripts.
`coverage.json` records exact Unicode ranges, sizes, SHA-256 and upstream URLs.
UTF-8 is the text encoding, not a guarantee that every Unicode code point has ink.

`original-ui.js::TextRenderer.prepare` loads the coverage index and all six faces
before measurement or Canvas drawing. `displayText` normalizes NFC and replaces
unsupported code points with bundled U+FFFD, with a console warning. City labels
use separate white fill and black outline. Imported FNT/PNG styling is ignored.

Licenses: NotoSerifSC-OFL.txt (SIL OFL 1.1), Jigmo-LICENSE.txt (CC0 1.0),
Unifont-LICENSE.txt (compiled font dual license; distributed under SIL OFL 1.1).
Keep these licenses when redistributing. Detailed author guidance is in
`rewrite/mod-kit/data/client/fonts/README.md`; build/audit tools are
`tools/build_bundled_fonts.py` and `tools/audit_bundled_fonts.py`.
