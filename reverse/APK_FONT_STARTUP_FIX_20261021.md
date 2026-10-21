# APK font startup compatibility fix

## Scope

The user authorized the font loading, fallback, retry, and initialization gating changes after confirming that logs from the recipient's phone are unavailable. The original device's exact failing font/WebView condition remains unknown. The earlier investigation is in `APK_FONT_STARTUP_DIAGNOSIS_20261021.md`.

## Implementation

- `tools/build_bundled_fonts.py` partitions all seven font families into a common subset plus bounded code point subsets. Every original supported code point remains in the combined inventory. Full previous WOFF2 files are preserved in `rewrite/local/font-sources-20261020/full-woff2`; original TTF/OTF sources are retained. Runtime copies of the superseded full WOFF2 files are removed only after confirming the archive matches.
- 345 subsets cover 192,879 code points, totaling 45,202,480 compressed bytes. The maximum declared decoded SFNT size is 1,794,404 bytes (enforced build limit: 8 MiB). Subsetting adds storage overhead; this change reduces startup decoding and concurrent memory demand, not total APK size.
- The primary WenKai common subset has 2,041 code points, 365,824 compressed bytes, and 750,108 declared decoded bytes. The Noto common fallback is 283,588 / 612,460 bytes; the Unifont common fallback is 50,944 / 187,140 bytes.
- `original-ui.js` registers a subset only after successful `FontFace.load`, serializes loading, applies a 12-second per-file timeout, tries the three common subsets in order, and loads other text on demand. Failed files are recorded by filename. Retrying clears failed entries and a rejected preparation promise. Temporary missing glyphs use the already loaded bundled replacement glyph; deferred completion repaints existing text canvases and invalidates map lettering caches.
- `app.js` assigns ready state after font, menu artwork, and data initialization. Main entries start disabled in HTML and have readiness guards. Fatal errors remain visible with a retry button. Optional font failure names the file, keeps the fallback usable, and can be retried without restarting. Ready views reserve space for this status strip so it does not cover controls.
- Direct event prose and geographic lettering request deferred fonts and update their rendering. The shared behavior applies to Android and Web. No game rules or account migration were introduced by this fix.

## Validation

- Font audit: 192,879 exact code points, hash/cmap/range checks, no overlapping subsets per family, decoded-size bounds, built-in text and six original/Mod APK text inventories: zero missing code points/errors.
- `tools/verify_font_startup.cjs`: 31 checks across six isolated scenarios. Normal deferred loading; primary resource failure with fallback/retry; invalid font bytes rejected by decoder; all common fonts failed then recovered; coverage metadata failed then recovered; pending catalog with entry gating. Recruitment portraits render after fallback. Error layout checked on desktop and mobile orientations. No uncaught browser exceptions.
- `tools/verify_font_regression.cjs`: 42 checks, 10 screenshots, no browser errors or missing resources. Tests explicitly await rare text loading and verify custom font glyphs, rare Han planes, city outlines, control bounds, and menu workflows.
- Web Release build: zero warnings/errors. Android ARM64 Release publish succeeded. JS syntax, Python syntax, and scoped whitespace checks passed.
- Exact final APK: frontend bytes, 345 subset files/hashes/sizes, no stale full font files, runtime index, gameplay assembly-store bytes, ZIP CRC, v2/v3 signatures, and 16 KiB ZIP alignment passed. Package version 0.2.5 / code 7, minimum API 26, ARM64 only.
- No Android device was attached; no installation, phone run, or original-recipient acceptance was performed. Existing preview processes and player saves were not operated. Browser tests use isolated `build/apk-font-fix-20261021/test-saves` and `test-mods`.

## Delivery

- APK: `build/apk-font-fix-20261021/ew4-rewrite-0.2.5-20261021-arm64.apk`
- Bytes: 125,336,978
- SHA-256: `ceab7785e9acabfe924049d886f6b1005cca4b790509f64c002ff89050c6e816`
- Signing certificate SHA-256: `82e8ba69b415d50193f7a6c2fd95d9d67f39d396bc321c1f0b03cf4bfb7c9df1`, identical to the published 0.2.4 APK, allowing an in-place update from that package.
- APK verification: `build/apk-font-fix-20261021/android-verification.json`; browser results/screenshots in `browser` and `font-regression` beneath the same build directory. Font audit: `rewrite/reports/bundled-fonts-20261020/coverage-audit.json`.
- Isolated preview: `http://127.0.0.1:5196`, PID 31712, source frontend/data with isolated test storage. Previous 5192/5193/5194 previews were not restarted or used by the tests.
- Local workspace build only; no commit, remote push, or Release upload was requested or performed. Older release packages remain intact.
