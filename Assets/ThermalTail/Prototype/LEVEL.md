# Continuous level blockout

Open **Tools → Thermal Tail → 3D Prototype → Open Continuous Level**, then Play.
The scene is `Scenes/ThermalTailLevel.unity`. All three sections are in that scene;
crossing between them never loads another scene or teleports the player.

This is a playable blockout based on the section sketches. Geometry, patrol routes,
camera timing and thermal shelters are editable scene objects. The original
`PrototypeExample` remains a separate integration test scene.

## Layout and intended play

| Section | Layout | Intended choices |
| --- | --- | --- |
| Thermal concourse | 40 × 32 units; entrance west, vent exit east; central divider, cover islands and a climbable maintenance table | Three wardens follow two straight shuttles and an exit triangle. Two civilians follow a rectangular circuit and a divider shuttle. Heat to 36 to travel in civilian rings, stay clear of personal space, then cool to ambient 22 before crossing exposed space. Cover and alternate routes allow experimentation. |
| Service ventilation | Connected maze, 8-unit grid passages (about 7.6 units clear between walls), 7-unit headroom, roof throughout | Search five note locations, time four sweeping cameras and climb two full-width low obstructions. Side branches reconnect or end in notes. Blue shelters replenish thermal concealment along the long routes. |
| Vault descent | Octagonal shaft, 28 units across opposite wall centres, floor 24 units below the entry; central camera column | Walk over the entry rim, descend and traverse the outer walls. Eight sheltered wall stations suggest a spiral route past twelve cameras. Ambient is 22 above the orange boundary, 30 in the middle band, and 18 below the blue boundary. Prepare at the heater/cooler straddling each boundary. Reach the keypad at the bottom. |

The wall shelters are rotated instances of the same heating/cooling prefabs used
on floors. Their canopies block the central cameras; approach behind the canopy
and wait for the HUD temperature to settle before leaving. Safe zones prevent new
visual detection, but do not erase an existing warden chase or prevent contact.
The vault offers route choice; following every station is not a progression gate.

Movement still raises temperature. This level uses its own `LevelSettings.asset`
with `HeatPerMetre = 0.16` to accommodate its larger distances. The integration
example's settings and the user's shared prefab tuning are preserved.

Checkpoints are at the vent entrance and vault entrance. Their triggers cover the
passage height, including ceiling travel. Camera alarms reset to the active
checkpoint. Notes and their journal entries survive resets. Reading stops player
input but does not pause the world.

## Notes, lock and tuning

The edited first section contains **Keys, exit door and paired patrol** in its
hierarchy. Its eight linked `KeyCollectible` instances are distributed randomly
across clear floor positions. Move them freely in the scene; keep each instance's
`Collection Id` unique. They collect on contact and persist through checkpoints.
The linked `KeyDoor` instance fits the edited concourse exit and opens automatically
after all eight keys; its sign shows progress. Its barrier blocks movement and
navigation while locked. Tune **Required Keys** on the Key Door component. Reusing
the key prefab elsewhere requires assigning a new collection ID.

**Warden · opposite square civilian** starts at the opposite corner of the west
civilian circuit. Its route references the same waypoint transforms, shifted by
two positions, so waypoint edits update both paths. It matches the civilian's
current 1.1 patrol speed and 1.4-second corner pauses. Normal suspicion/chase behavior
can interrupt its patrol; checkpoints restore the starting offset. The original
civilian, existing wardens and both other sections were preserved.

Civilian prefab instances now have **Civilian Suspicion**: a wrong temperature
inside their core ring fills a visible meter in **0.75 seconds** and calls nearby
wardens to the player's position. **Detection Time** and **Recovery Time** (default
0.5 seconds) are editable on the civilian prefab. Each civilian uses its own
Crowd Zone target and the shared Match Tolerance. The outer transition band does
not build suspicion. Cover, safe shelters, matching temperature and leaving the
core ring drain the meter; checkpoints clear it. The ring turns yellow/red during
buildup, and the overhead meter is hidden behind walls. Physical personal-space
contact still alerts guards immediately. Guard response distance remains the
session's **Civilian Alert Radius**.

`Prefabs/PressurePlate.prefab` is a standalone 2 × 2 floor trap. Drag it into your
scene and place its root at the top surface of an existing floor (vault bottom:
Y = -24 in the original blockout). Scale X/Z to cover the desired area. Its thin
box trigger and warning surface scale together; it needs the floor beneath it
for player support. Player contact immediately starts capture and the usual
0.8-second checkpoint-return sequence, regardless of temperature, safe zones or
respawn grace. Non-player objects do not trigger it. Disable its Pressure Plate
component to disarm it. No scene instances are added automatically.

- Edit `Data/ContinuousLevel/VentNote1.asset` through `VentNote5.asset`. Each begins
  with the exact body text **blank text**. These have distinct collection IDs.
  Rebuilding the level preserves changes to these assets.
- The final `LockActor` inherits the shared lock prefab's code, currently **7139**.
  Fingerprints identify the four digits; supplying the order is left to your
  replacement clues. Correct entry immediately completes the prototype.
- Actor roots remain linked prefab instances with unit scale. Edit assets under
  `Prefabs/` for shared appearance/behavior. Routes, scene references, thermal
  station targets and camera placement are scene-specific.
- Camera tuning lives in `Prefabs/ContinuousLevel/DuctCamera.prefab` and
  `VaultCamera.prefab`, variants of the shared SecurityCamera. Ducts use range 11;
  the shaft uses 17. They use FOV 32/30 respectively,
  a 10-second cycle, a 68% active interval and a 48-degree sweep. Phase offsets
  stagger them; only placement, phase and session are overridden per camera.
  Rebuilding preserves edited variants. Warden tuning is inherited from the Warden prefab.
- Under **Security Camera → Detection Timing**, **Base Detection Time** is seconds
  to alarm from zero suspicion before temperature acceleration (default 3.33).
  **Temperature Sensitivity** adds suspicion/second per degree beyond the shared
  Match Tolerance (default 0.045; set to 0 for fixed timing). **Suspicion Recovery
  Time** is seconds to drain a full suspicion meter when detection stops (default
  2.22). These fields inherit from SecurityCamera through both variants; edit a
  variant to tune its whole group independently. Wardens still use LevelSettings.
  Actual continuous exposure time is `1 / (1 / BaseDetectionTime + excessDegrees
  * TemperatureSensitivity)`. Existing suspicion shortens the remaining time;
  matching temperature prevents buildup. Set **Active Fraction = 1** to keep a
  camera on; **Period** controls its sweep cycle, not its detection time.
- The player camera is **Player Camera → Surface Camera**. Distance defaults to 5.
  The vent dimensions accommodate that distance in straight passages; the usual
  obstruction response still pulls it closer at corners, barriers and shelters.
- The hierarchy groups each section into **Architecture**, **Gameplay · prefab
  instances**, and **Signs and lighting**. After moving concourse obstacles or
  changing patrol access, rebake the `NavMeshSurface` on the first section's
  Architecture object. Only ground NPC navigation is baked; climbing uses physics.

**Rebuild Continuous Level** is an explicit editor tool, not a runtime generator.
It replaces that scene's geometry and instance edits after a confirmation dialog;
it preserves shared prefabs, existing materials, note text and level settings.
Use **Open Continuous Level** for normal editing and playtesting. Legacy build
scene selection is unchanged; select this scene when preparing a standalone build.

## Verification and playtest scope

Automated checks cover linked actors/references, connected vent tiles/headroom,
patrol navigation, passage/checkpoint crossings, automatic baffle/rim/wall/bottom
climbing, wall-mounted thermal sources, normal camera distance, note persistence,
and keypad completion. Rendered views were inspected for enclosed geometry and
readability. This does not replace a human difficulty/comfort playtest; patrol
spacing, camera timings and the long descent are starting values for iteration.
