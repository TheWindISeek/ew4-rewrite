# Global Countries, CNTR IDs And Unit Visuals

Implemented at the user's request: independent global country definitions, editable unit visuals, and full country IDs embedded in BTL files. No original BTL, texture or player save was overwritten. Existing concurrent dialogue, item and display changes were preserved.

## Country Registry

- Editable base: `rewrite/data/client/countries.json`, 43 identities; author package path `client/countries.json`.
- `CountryCatalog` validates unique exact case-sensitive string IDs, names, flag references/PNGs, colors and combat variants. Country IDs support Unicode, numbers, punctuation, spaces and strings longer than 128 characters. Empty/whitespace-only IDs and control characters are rejected; JSON has a 2 MiB resource budget. IDs are never truncated or normalized.
- `unitVariant` selects the actual combat catalog, `visualVariant` selects model appearance, and `flag/flagImage/color` select presentation. Existing BTL participants retain camps, resources, territory and order.
- Optional aliases retain old BTL/XML references without conflating identity and resource names. `faction_create.country` reuses registry defaults; explicit event fields override defaults. Runtime faction instances keep their existing separate naming contract.
- Source-hash guards keep an inherited original registry from overriding changed legacy Mod sources. Original source-compatible defaults preserve old startup fingerprints. Explicit registry changes participate in Mod logic revisions and save isolation.
- APIs: `/api/country-registry`, `/api/catalog`, scenario-specific `/api/countries`, and existing creation events. Chapter/alternate selection, saves, local-player lists, HUD and battle flags use configured identity/resources.

## CNTR v1

- Original BTL records contain 16-byte terminated UTF-8 fields, not variable-length IDs. Their maximum is 15 payload bytes. Missing terminators are rejected, not silently truncated.
- New required `CNTR` block uses the existing `EW4XBTL\0` container, version 1 / flags 1. Its UTF-8 JSON stores schema, body SHA256, the complete ordered faction-ID array and optional event-row country overrides.
- Reading applies complete IDs by source faction index. Native event short-country references follow the mapping; ambiguous duplicate source names require explicit event-row overrides. CNTR IDs must exist in the global registry when starting through the client.
- Native body bytes, ownership indices and record order stay identical. CNTR source hashes exclude the full extension container. JURI, VICT and unknown optional blocks survive replacement/removal.
- ModTool exposes `country-export`, `country-apply`, `country-strip`. New outputs only; apply validates the extension, global references and real single-player startup before writing. No bulk migration of original assets.
- Old original APKs and old rewrite hosts do not support the required block. This work compiled Web/Android sources but did not generate a new APK.

## Original Model Audit And Fix

- Native evidence: `reverse/native/decompiled/00059c20.c` copies BTL variant at record offset 40 to native faction +0x40; `000520f8.c` passes that visual variant to `00070554.c`, which searches kind + variant + formation, then kind + formation, then generic kind.
- Inherited combat-profile IDs previously hid dedicated models and sometimes borrowed another country's skin. HRE has 33 dedicated motion definitions despite using the generic combat table. The corrected resolver keeps stats and appearance independent, including preload, recruitment, map bodies and left/right battle actions.
- `client/unit-visuals.json` provides faction, visual-variant, profile, default and custom-definition hooks. Original XML imports generate mappings from that pack's own motion definitions and convert every referenced army library.
- Audit: 369 combat profiles, 877 original motion definitions, 12 libraries, 1,435 variant/profile cases. 26 variants had 33 different bindings each. Imported motion/item/element data and army texture bytes match preserved originals; no missing motion references.
- Previously documented original atlas leaf gaps remain unchanged: 12 referenced leaf crops cannot be guessed from similar images. See `FRONTEND_RESTORATION_20261019.md` and `frontend_resources.json.animationGaps`.

## Verification

| Check | Result |
| --- | --- |
| Country registry + CNTR | 196 passed: 95 original BTL combat defaults, Unicode/punctuation/>128-character IDs, exact uniqueness, aliases, selection, independent flags/models/stats, save/load, event creation, ordering, ordinary vs batched AI, malformed data, JURI/VICT coexistence, package import/export and revision isolation |
| Unit visual selection | 25,845 passed across 1,435 cases; geometry, actions/directions, custom definitions, reserved JS object property names and unchanged stats |
| Visual Mod integration | 14 passed: validation, pack/import/export, actual startup and legacy XML conversion |
| Real browser models | 581 passed, 576 nonblank poses, 252 actual initial units including 31 HRE, four screenshots, no errors or missing resources |
| Real browser countries | 24 passed, six screenshots at 1280x720, 844x390, 390x844; custom PNG selection/battle flag, full embedded ID and save/load; no errors or missing resources |
| Client regression | 2,266 passed |
| Settlement/victory regression | 459 + 32 passed |
| Full Mod regression | 102 passed; report `rewrite/reports/mod-import-20261019/bc8463a7c302471cb09bcd679b26fe2d` |
| Native frontend regression | 13,623 passed |
| CLI roundtrip | Export/apply/strip of 19 countries with long Unicode ID; startup succeeded; stripped BTL SHA256 equals original `bef704254c55997c1c61912f97c8c1726d9688d39ed13ce6b105236e0ba2b759` |
| Builds | Web, ModTool and Android ARM64 Compile passed with zero warnings/errors; no APK generated |

Evidence and screenshots: `rewrite/reports/unit-visuals-20261021/`. Unit/country check projects use isolated `build/unit-visuals-20261021/` outputs and profiles. Initial system dotnet lacked Android workload; the configured `.codex/tools/dotnet-android` runtime with its SDK/JDK compiled successfully. One build collided with this task's running test DLL; final builds used an isolated output and passed.

Final independent preview: http://127.0.0.1:5251, PID 33936, assembly `build/unit-visuals-20261021/country-web-final/Ew4.Web.dll`, static snapshot/profile `build/unit-visuals-20261021/country-preview`. Browser package checks restore builtin content after completion. The prior model-only preview at 5250 is older. No other service, player profile, device or publication was operated.

Author guide: `rewrite/mod-kit/docs/porting/GLOBAL_COUNTRIES.md`; reusable example: `rewrite/mod-kit/examples/global-countries/`; model guide: `rewrite/mod-kit/docs/porting/UNIT_VISUALS.md`.
