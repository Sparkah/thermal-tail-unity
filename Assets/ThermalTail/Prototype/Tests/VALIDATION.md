# Validation

Support-placement recovery, 2026-09-25: **40 core Play Mode checks passed**
in the isolated Unity 6000.3.5f2 copy. A player starting at 0.22 above the floor
now recovers to the 0.34 body clearance and can move. A low overhead obstacle
still prevents that correction. All narrow-surface and existing gameplay checks
passed, and all 10 scene files remained byte-for-byte unchanged. This fixes an
initial-overlap sweep regression without changing authored player/spawn transforms.

Narrow-surface traversal, 2026-09-25: **38 core Play Mode checks passed** in
Unity 6000.3.5f2 using an isolated project copy. New coverage includes climbing
past 0.08/0.20/0.28-unit ledges in both directions, rounding a 0.03-unit cap,
per-step body clearance, skip distance and enable controls, unmarked landings,
unsupported gaps, blocked routes, and checkpoint cancellation. Existing ordinary
corner and gameplay checks also passed. All 10 existing scene files were verified
byte-for-byte unchanged. The Player3D prefab enables the fallback with a 0.75-unit
maximum; no scene generation or save was performed.

First-section keys/door/patrol update, 2026-09-24: **3 targeted Play Mode checks
and 1 prefab authoring check passed**. Verified eight unique, floor-supported,
navigation-reachable key placements; actual trigger collection without duplicates;
door collision before eight keys and passage afterward; persistent keys/open state
after checkpoint reset; opposite-corner waypoint order, matching patrol speed and
pauses, and matched initial travel. All additions are linked prefab instances.
The tested scene was merged by adding 25 serialized blocks and updating only the
first-section root's child list. All 2,403 other pre-existing blocks (including
all of sections 2 and 3) and every other scene file remain byte-for-byte unchanged.

Civilian thermal suspicion, 2026-09-24: **34 core Play Mode checks passed**, including
fast buildup, guard alert delivery without direct capture, per-civilian temperature
requirements in overlapping rings, matching/band/cover/shelter/grace suppression,
recovery and checkpoint reset. The scene prefab/reference authoring check passed,
including the civilian component and world-obstruction mask. The separate legacy
generated-vent-layout check fails on the edited scene because its expected active
duct-floor tiles are absent; no scene edits were made to satisfy that old layout
assumption. All 10 scene files were verified unchanged. The civilian prefab diff
only adds the new component and its root component reference.

Pressure plate update, 2026-09-24: **32 core Play Mode checks passed** in Unity
6000.3.5f2. The actual prefab captures a matched player inside a safe zone during
respawn grace, closes the journal, returns to the checkpoint and can trigger again.
Non-player colliders, disabled plates and completed games do not trigger capture.
Ordinary capture sources retain respawn grace. All 10 existing scene files were
verified byte-for-byte unchanged; no scene builder or scene save was run in the
working project.

Camera timing update, 2026-09-21: **30 core gameplay checks and 13 Edit Mode
checks passed** in Unity 6000.3.5f2. Coverage includes independent camera base
timing, optional temperature acceleration, camera-specific recovery, unchanged
ordinary-sensor timing, alarm/checkpoint behavior, and inheritance of all three
new fields through prefab assets and scene instances.

Unity 6000.3.5f2, 2026-09-21: existing **35 Play Mode checks passed**;
the final continuous-level revision passed **8 additional Play Mode checks**.
The final Edit Mode suite passed **10 checks, 0 failed**.

The continuous level was generated and rendered in an isolated validation project,
then its scene, baked navigation, materials, note assets and camera variants were
copied back and verified byte for byte. All nine original shared prefabs remained
unchanged. Final level checks cover patrol navigation; connected vent tiles and
camera headroom; prefab/variant inheritance; full-height checkpoints; automatic
vent-baffle, vault-rim, octagonal-wall and bottom-floor traversal; warm/cold wall
shelters; five note pickups and persistence; and final keypad completion. No
authored transition links are needed in this scene.

The scene builder compiled and generated the example, prefab palette, and baked navigation in an isolated copy of the project. The exact validated source and generated assets were then integrated into the working project.

The existing example was subsequently connected to its prefab assets in place and revalidated. All 15 reusable objects inherit from nine prefab assets. The migration retained layout, scene references, note assignments, and camera phases, adopted the tuned warden prefab, and moved existing player mouse sensitivity into the shared player prefab. Editor checks verify inheritance by changing source prefab values, confirm migration is repeatable, and verify rebuilding preserves existing prefab/material files byte for byte.

Covered by engine tests:

- Civilian core temperature and the ambient/civilian transition band.
- Overlapping crowd zones and deterministic core precedence.
- Cooling to its target and environment detection after a checkpoint warp.
- Physics-based floor-to-wall climbing and heat from movement.
- Blocking against an unmarked solid wall.
- Automatic wall-to-top, top-to-wall, and wall-to-wall turns with body clearance and preserved heading.
- Automatic 45-degree turns around an octagonal mesh wall.
- Rejection of unmarked adjoining faces, obstructed arcs, and unsupported gaps.
- Checkpoint warps cancel automatic traversal.
- All three authored example edge links, including body clearance over the vault rim.
- Baked NPC navigation, patrol progress, and civilians without perception.
- Physical warden capture independently of suspicion.
- Warden alarm pursuit runs faster than the player, accepts multiplier changes, and restores patrol speed afterward.
- Identified players remain tracked behind nearby wardens even after matching temperature; walls prevent memory updates, lost-sight grace leads to search, and resets clear pursuit.
- Suspicious wardens turn gradually with a crossing player before reaching the unchanged chase threshold.
- Cover prevents suspicious wardens from updating their remembered target; suspicion decay returns them to patrol.
- Suspicion pauses baked navigation and releases both movement and automatic rotation for patrol or chase.
- Adjustable close-range field of view includes low/side approaches, respects walls and distance limits, and retains normal thermal suspicion buildup.
- Close-range distance and angle inherit from the Warden prefab without scene overrides.
- Takedowns require reachable rear approaches, reject front/side/distant/modal attempts, and prevent repeat actions and capture by incapacitated wardens.
- Default checkpoint revival restores warden behavior, collisions, baked navigation, and standing pose; alerted takedowns and persistent incapacitation are opt-in settings.
- Camera upward pitch produces an upward view while floor obstruction keeps the camera above ground.
- Wall occlusion and thermal suppression of suspicion.
- Camera checkpoint reset while the journal remains open and player input is blocked.
- Physical civilian contact causing an alert without direct capture.
- Physical note pickup, one-time reward, journal access, and checkpoint persistence.
- Correct and incorrect keypad submissions and immediate completion.
- Four distinct digits required for a valid passcode.

These are automated headless checks. Presentation, control feel, final level routes, stealth timing, and thermal balance still require interactive playtesting.
