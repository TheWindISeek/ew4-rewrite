# 2026-10-18 frontend correction checkpoint

Current user-editable issue and acceptance list: [FRONTEND_ISSUES.md](FRONTEND_ISSUES.md). Read that first. The entries below preserve earlier investigation and are not current acceptance claims.

## User acceptance criteria still open

- Preserve original non-payment UI; no redesign. Prefer real HD assets where available.
- Clamp camera to original map limits; do not show outside-map or inactive hex edges.
- National borders must match original contours, not the current double-stroke hex outline.
- Land ownership and farms must be beneath shore/river masks. Some coast pixels still expose untinted hex wedges.
- Align cities, units and selection corners to the same native cell centre; user reports upper-left visual offsets.
- Cities/ports and troops need native size, pivot and layering. Do not substitute scale for opacity.
- Available player units pulse ONLY their bottom-left health/category badge if movement OR attack remains this turn. Selection does not control this pulse. Exhausted/enemy badges and unit bodies do not pulse. Fixed .5 opacity is incorrect.
- Terrain income/defence panel must not overlap the top-left overall resources.
- Country list flags are STATIC: image_flag_hd contains frame 1 only. Strategic map flags animate from their own library. A runtime frame swap to nonexistent list frames caused broken-image flicker; code removed, browser reload still needs verification.
- AI skip button bottom-left; running faction flag and >>> bottom-right. Skip presentation quickly while executing all AI rules.
- Sea transport uses original ship body and unchanged unit-category badge. Investigate actual original sea movement budget before adjusting rules.
- Movement follows authoritative paths with animation, recruitment needs original appearance effects. Current implementation connected movement/ready events; native recruitment staging is still under investigation (4f01c).
- Forts cannot move or buy transport, both UI and command validation. Recruit cards use explicit original artwork; empty land uses fortress construction icon.
- Backend faction/camp identities and hostility must support arbitrary numbers of camps, not two combatants plus neutral. Existing dynamic IDs alone are insufficient proof; audit relations and AI consumers and add a multi-camp integration case.

## User screenshot references added 2026-10-18

- Reference index: `references/frontend_user_20261018/README.md`; 11 original attachments archived and hash-verified. These show native behaviour, not completed fixes.
- Full supplemental images 10/11 supersede 01/02 for the top-left tile panel: below global totals, translucent slanted background, resource yield or category-specific terrain penalties according to selected tile. Example values are not constants.
- Image 03 anchors fort body, flag, badge and selection corners; 04/05/08 anchor market scales and tavern mug at city lower right, beside level and above name.
- User also requires the already-converted transport marker. Keep facility symbols, transport conversion state, at-sea ship body and unit-category badge distinct; verify native anchor-symbol conditions before assigning it to a troop or tile.
- Images 07/09 are explicit national-border references, as confirmed by the user: a wide dark segmented curved strip between differently tinted territories, not doubled hex outlines. They also show brick/wire markers whose native conditions need checking.

## Movement undo scope (latest user corrections)

- ONLY troop movement is undoable, without a per-turn usage quota.
- A unit that actively attacked this turn cannot undo any of its movement this turn.
- Do not undo recruitment, purchases, construction, combat, or turn advancement.
- Return destination occupancy and conflicting territory changes must be validated.
- Implemented in Ew4.Client/MovementHistory.cs; saved with ClientSlot. Host checks: 226 passed, including 12 move/undo repetitions, path adjacency, save/load, attack lock, and unrelated resource preservation. Native and browser acceptance still required.

## Current implementation state

- Battlefield renderer extracted to battlefield.js and wired from app.js. HD map caches exported; source artwork untouched.
- Native evidence: cell centres y=row*54-18; .85 building argument is alpha; terrain kinds 0/1/11 suppressed; moving effects 52658; badge availability predicate 532e8 and shared alpha 60958.
- 625 camera/interpolation checks passed; includes inset active bounds. This does not prove native boundary behaviour.
- Full core verification rerun was started after repairing a test fixture with fort movement 0 / remaining movement 6 inconsistency; inspect completion before reporting.
- Web preview uses port 5184; server restarted after C# changes. JS changes require browser reload. Existing ARM64 APK is stale until rebuilt.
- Do not claim full original frontend parity, completed reverse engineering, or device acceptance.

## Next actions

Approved extension design and implementation ordering are recorded in
`FRONTEND_EXTENSION_PLAN.md`: original appearance with reorganized display code,
resource-use audit, horizontally browsable commander rosters with star categories,
then new troop dismissal with configurable refunds. These extensions are not
implemented. The latest calibration compiled but remains visually unverified.

1. Fix badge pulse according to availability, including immobile forts with attack available; verify original timing.
2. Finish original movement/recruit effects, fix pivots/borders/coast masking and resource layout.
3. Verify country list flags and AI skip/flag flow in browser; add screenshots as evidence.
4. Audit multi-camp hostility and test more than three camps.
5. Run checks, build ARM64 APK, update short anchors and record remaining gaps honestly.
