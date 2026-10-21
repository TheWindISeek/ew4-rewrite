# APK startup and headquarters error diagnosis

## Evidence and Scope

User screenshots show `A network error occurred.` on the main menu and `Cannot read properties of undefined (reading 'atlases')` in headquarters. The installed APK version and Android WebView version are not yet known. Investigation used the local published 0.2.4 ARM64 APK; no connected ADB device was available. No game code, APK, account, or running preview was changed.

## Confirmed Failure Chain

- Packaged `original-ui.js` calls `document.fonts.load` for all six bundled font families using `Promise.all`. A single failed face rejects preparation.
- `app.js` startup awaits `text.prepare()` before assigning global `catalog` and `manifest`. Its catch only displays the error. Main menu controls remain usable.
- Headquarters can complete its own RPC and then call `uiImage` or `portrait`, which reads the still-undefined `manifest.atlases`. Thus the second message does not prove that the atlas files themselves are missing.
- The rejected font preparation promise is retained in `fontPreparation`, so there is no in-page retry.

## Isolated Reproduction

Artifacts: `build/apk-diagnosis-20261021/diagnose.cjs` and `report.json` (ignored build outputs). The script loads exact font binaries, CSS, and renderer extracted from the release APK into desktop Edge/Chromium 155.0.4283.62 using Playwright routes. No game server or account is used.

All six packaged font families loaded successfully. Injecting a 404 only for `Jigmo2.woff2` produced:

1. `NetworkError: A network error occurred.`
2. `manifestUndefined: true`
3. `TypeError: Cannot read properties of undefined (reading 'atlases')`

This validates the failure chain and exact messages, but does not establish which font or request failed on the phone.

## Likely Underlying Causes to Distinguish

1. Android WebView font decoding or resource/memory pressure. The six fonts total 30,512,720 compressed bytes and 99,563,460 declared decoded SFNT bytes. `Jigmo2.woff2` alone declares 35,661,996 decoded bytes. These values justify checking memory and decoder logs, not declaring an out-of-memory cause or a universal 30 MB Android limit.
2. A local WebView resource response/read failure for one of the fonts, including active Mod content differences. Fonts are requested from intercepted `https://ew4.local` URLs, not an external font server.
3. Package/version differences or incomplete/stale installed content. All six font files and coverage metadata exist in the inspected 0.2.4 release APK. Device logs and installed version are required before attributing this to a missing file.

Chromium implementation corroboration: `FontFace::SetError` defaults to `DOMExceptionCode::kNetworkError`, and `FontFaceSet::LoadFontPromiseResolver::NotifyError` rejects the font's error. Sources: https://github.com/chromium/chromium/blob/main/third_party/blink/renderer/core/css/font_face.cc and https://github.com/chromium/chromium/blob/main/third_party/blink/renderer/core/css/font_face_set.cc . A font decoding failure can therefore surface as a network error.

## Next Debugging and Fix Direction

Capture installed APK version, WebView version, and app-specific logcat from startup through headquarters entry. Inspect `EW4`, Chromium font/OTS messages, and local font response errors. Add per-family/font URL diagnostics; initialize UI with an explicit loading/failed state; provide a retry that resets a rejected preparation promise. Consider smaller font subsets and deferred rare-character fonts while preserving bundled glyph coverage. These are recommendations only; no implementation or new release was made during diagnosis.
