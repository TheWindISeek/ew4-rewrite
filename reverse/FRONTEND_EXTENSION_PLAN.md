# Frontend preservation and requested extensions

Status: design checkpoint, not implemented or accepted. Original non-payment
appearance and interaction remain the baseline. Source display code does not
need to be reproduced line for line; resource use and visible behaviour do.

## Implementation order

1. Audit source art, layouts, animation and display-call conditions; account for
   every resource, including alternatives, duplicates, unused and payment-only art.
2. Restore battlefield and non-payment screens from that evidence. Use runtime
   screenshots to validate conclusions, rather than as the only implementation source.
3. Apply the approved roster navigation and classification extensions.
4. Add troop dismissal with configurable resource refunds as a new game rule.

## Commander classification

- Use the original one/two/three-star recruitment sections as the initial
  classification vocabulary, preserving original tab artwork and card style.
- Include an All section. Star groups come from data and are extensible, not a
  fixed three-element array or an overall commander count limit. Verify the
  source star mapping; do not infer it from portrait, military rank or title.
- Add optional troop-specialty filtering and name search for large rosters.
  Keep sorting separate from classification; default ordering must be stable.
- Headquarters and battlefield deployment share classification and card data,
  with horizontal touch scrolling/paging. No fixed twelve-commander ceiling.
- Preserve current category, page and selected commander when returning from
  details. Clamp the page after roster changes, and handle empty categories.
- Deployment availability remains a rule check: show assigned, incompatible or
  unavailable commanders appropriately rather than hiding their existence.

## Troop dismissal

- Player-owned troops may be dismissed during the player's active turn. Show a
  distinct command in troop actions and a confirmation with the exact refund.
- Proposed initial refund fraction: 50%, configurable by scenario/rules. This
  value is a planning default, not a user-specified or implemented balance rule.
- Proposed refund base is the troop's attributable recruitment/merge investment,
  adjusted for remaining strength. Use integer rounding down per resource; apply
  the same calculation to preview and execution. Do not calculate from a newly
  changed catalogue price. Initial scenario troops need an explicit salvage base.
- Define the cost basis for each stack/merge so splitting, merging, damaged
  troops and repeated commands cannot create resources. Construction in progress
  needs an explicit refund policy before enabling dismissal of those units.
- Refund eligible in-battle resources only. Do not refund commander medals,
  training, equipment, transport conversion or other unrelated expenditure.
- Preserve owned commanders and their progression/equipment; detach them safely
  for later deployment under the session's rules. A temporary tavern commander
  must not silently become a permanently owned commander.
- Remove the unit without removing its city, land ownership or installations.
  Update selection, command availability, occupancy, objectives and victory
  consistently. Reject stale/double dismissal commands.
- Dismissal is not undoable. Invalidate movement checkpoints that refer to the
  removed unit; do not expand the user's movement-only undo scope.
- Validate ownership/turn, preview-execution parity, fractional rounding,
  wounded/merged units, commander handling and save/load before delivery.

## Current calibration checkpoint

The latest interrupted implementation added backend tile-info/facility fields,
scaled tile-panel positioning, selection-bound measurements and facility/ship
markers. Web compilation passed, but those changes have not received browser
acceptance. The previous Web process was stopped for that build; do not assume
port 5184 is currently serving the new build. Border rendering remains open.
