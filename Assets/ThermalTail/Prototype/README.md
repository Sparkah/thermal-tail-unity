# Thermal Tail 3D prototype

This is the new component-based gameplay foundation. The original pixel simulation and old levels are left intact. Do not add `ThermalDirector` or `TTObject` to a new prototype scene: those scripts own a different movement/coordinate system.

The playable three-section blockout is **Tools → Thermal Tail → 3D Prototype → Open Continuous Level** (`Scenes/ThermalTailLevel.unity`). See [LEVEL.md](LEVEL.md) for its layout, note assets and tuning. The example below remains a smaller integration test scene.

## Open the example

Use **Tools → Thermal Tail → 3D Prototype → Open Example**, then Play. If the generated example does not exist, the menu builds it. **Rebuild Example** replaces the generated scene using the existing prefab palette; use a different scene for your authored level.

The example has a lobby, elevated vent walkway, and octagonal shaft. It is an integration greybox, not the finished level design. Walk toward the centre of the north wall to climb to the vent walkway. The wall-to-floor transition is near the top centre. Follow the walkway into the shaft, descend, and reach the gold keypad on the opposite side. Heating/cooling shelters and optional notes demonstrate the systems.

Controls: **WASD** movement, **move mouse** to look, **J** journal, **E** interact/takedown/close. The nearest eligible keypad or warden supplies the E prompt. The cursor locks during gameplay and unlocks for notes and the keypad. **Escape** closes an open panel or releases the gameplay cursor; **left-click** the game to resume after releasing the cursor or switching windows. There is no jump. Notes collect on contact. Keypad buttons use the mouse. Reading disables player movement/look but does not pause enemies, temperature, or cameras.

Tune the camera on **Prototype Camera → Surface Camera → Distance** (default 5; obstructions can push it closer). Tune mouse look on **Player 3D → Prototype Controls → Horizontal Sensitivity / Vertical Sensitivity** (defaults 0.18 / 0.12 degrees per pixel). Higher values turn faster. Walking/climbing speed is set by **Move Speed / Climb Speed** on `Data/PrototypeSettings.asset`. Make persistent scene changes outside Play Mode. Security camera states use their beam colors; their suspicion bars appear only while suspicion is above zero. Sensor HUD indicators are hidden when world geometry blocks the view from the player camera.

The example code is **7139**. Fingerprints mark 1, 3, 7, and 9; they do not reveal the order. The two notes state that 7 comes first and the remaining digits ascend. Correct submission completes the game immediately. Wrong attempts have no additional penalty.

## Architecture and ownership

Camera pitch limits live on **Prototype Camera → Surface Camera → Minimum Pitch / Maximum Pitch**, defaulting to -80 / 80 degrees. Negative pitch looks upward; camera collision can bring the view close to the player near the floor. Each warden has **Warden Brain → Run Speed Multiplier**, default 1.6. Its running speed is **Npc Motor → Chase Speed × Run Speed Multiplier** (2.3 × 1.6 = 3.68 by default, versus the player's 2.6). Pursuit from visual detection or civilian alerts uses this multiplier; searching uses the base chase speed, and patrol/reset restores patrol speed.

| Component | Responsibility |
| --- | --- |
| `PrototypeSession` | Checkpoint, recovery grace, collected IDs, journal, modal state, alerts, completion. Never moves actors during normal play. |
| `SurfaceMotor` + `SurfaceCornerPath` | Player movement, swept collision, surface attachment, and automatic paths around adjoining convex faces. Authored links remain optional. |
| `PrototypeControls` | Hardware input, interaction targeting, modal input gating. |
| `PlayerThermal` | Temperature, environment sources, crowd/ambient matching, safe-zone state. |
| `NpcMotor` + `PatrolRoute` | Ground navigation and routes, usable by civilian NPCs without any perception component. |
| `CrowdZone` + `CivilianContact` | Thermal concealment opportunity and personal-space alarm. |
| `CivilianSuspicion` | Fast thermal suspicion inside an individual civilian's core ring; calls wardens at a full meter. |
| `VisionSensor` | Range, 3D field of view, occlusion, suspicion. Never captures or navigates. |
| `WardenBrain` + `CaptureContact` | Patrol/suspicious/chase/search decisions and reachable physical capture. |
| `WardenTakedown` + `WardenTakedownView` | Rear interaction eligibility, disabling/restoring a warden, and a separate collapsed-pose presentation. |
| `SecurityCamera` | Sweep/duty cycle, camera-specific detection/recovery timing, full-suspicion checkpoint reset. |
| `CollectibleActor` | Stable-ID, one-time collection lifecycle. |
| `NoteActor` / `MothCollectible` | Different rewards sharing collection behavior. Notes do not count toward a quota or award moths. |
| `LockActor` | Interaction eligibility and four-distinct-digit validation; owns locked state. |
| View components | Camera, thermal tint, rings, beams, suspicion bars, journal and keypad. |

## Authoring new content

The example's player, warden, civilian, notes, moth, security cameras, lock, and heating/cooling shelters are connected instances of the assets in `Prefabs/`. Edit those prefab assets for shared tuning, visuals, and collider changes. Scene overrides hold placement, session/camera references, patrol routes, note data, collectible IDs, and camera phase offsets. Intentional additional instance overrides are still supported; use Unity's Overrides menu to review or revert them.

The scene builder instantiates existing prefabs and creates default assets only when missing. **Rebuild Example** replaces the generated scene and navigation but preserves existing prefab and material tuning. It is unnecessary for normal prefab edits. **Connect Example Prefabs** repairs older loose example actors without regenerating the level and is a no-op once connected. Unique level geometry, route markers, checkpoints, labels, and the scene camera/session remain scene objects.

Serialized scene references on prefab assets are deliberately absent; standalone actors find the scene session in `Awake`. Assign the player camera rig, patrol route, note data, and unique moth ID when placing new instances. One session/player pair per scene is supported.

- Actor roots must have **unit scale**. Scale only visual children. The player's body is intentionally a forgiving sphere, not the shape of its mesh.
- Put solid floors, walls, and obstacles on **TT World**. Add `ClimbableSurface` only where attachment is allowed. Unmarked obstacles block movement. Floors also need the marker.
- Player/NPC bodies use **TT Actors**. Trigger volumes use **TT Volumes**. The builder assigns the first available custom layer slots, without replacing existing layer names.
- Configure player solid queries to include world and actor bodies. Configure sight/camera obstruction queries to include world geometry and ignore trigger volumes. Detection rays sample the player centre and shoulders.
- NPCs use a `NavMeshAgent`, kinematic Rigidbody, and solid capsule. Bake a `NavMeshSurface` after changing walkable geometry. Navigation owns the actor transform; do not add another movement controller or a dynamic Rigidbody. Agents remain ground-based and do not follow the lizard onto walls.
- Route points are world-space Transform markers. Loop routes wrap; non-loop routes ping-pong. Use waypoint spacing and agent avoidance to keep civilian routes legible. Dynamic changes to walls/gates require navigation updates; a physical collider alone does not change an existing NavMesh.
- Add `CrowdZone` as a child centred at the player's travel height. Its core requires civilian temperature. Its outer transition band accepts either civilian or ambient temperature. If zones overlap, any core takes priority over edge bands, and the closest matching core temperature is used. Defaults give every civilian the same target.
- Personal-space and capture triggers must extend beyond the solid body enough for the player to reach them. Rings show trigger boundaries; body overlap with the ring is the contact condition, not just the player's centre crossing it. Crowd matching uses centre distance and a spherical zone; the floor ring represents its horizontal extent.
- `SafeZone` prevents **new visual detection**. An already alerted warden can still track an identified player in plain sight, including inside a safe zone or at a matching temperature. It does not stop physical contact. Give shelters real visual cover and keep them out of NPC routes. A `ThermalVolume` is separate: it can be safe or exposed, override ambient, or approach a body-temperature target. Highest priority wins for overlapping heat sources and ambient overrides.
- `Checkpoint.Spawn` represents the player's body centre, with its up vector pointing away from the supporting surface. Set its temperature explicitly. Checkpoints preserve notes and moth collection; reset player position/temperature, NPCs, suspicion, and camera cycle time. No disk-save system is included.
- Each authored note asset needs a unique `Id`; each moth instance needs a unique `CollectionId`. Reusing an ID intentionally represents the same collectible. World pickup objects disappear but journal data remains for the run.
- `LockActor.Passcode` must contain exactly four distinct digits. Fingerprint buttons are derived from that code. Keep authored note content consistent with it. The lock is the objective; no exit den or moth quota is required.

## Surface transitions

Walking into a marked wall performs a concave surface turn. **Player 3D → Surface Motor → Automatic Corners** also rounds adjoining convex faces by default: wall-to-top, top-to-wall, and wall-to-wall, including octagonal corners. Mark the solid surfaces with `ClimbableSurface`; no entry/exit markers are required. **Maximum Corner Angle** defaults to 110 degrees.

At an edge the motor probes for the adjacent face, verifies both faces meet, then builds and sweeps a short arc with body clearance. Position and surface orientation advance together, preserving the movement frame and mouse heading. Unsupported edges, unmarked faces, gaps, and blocked arcs stop movement. Thin surfaces without enough landing support can also stop movement. This targets simple planar geometry, not arbitrary curved meshes or moving platforms. Dynamic obstruction during a committed turn pauses it until clear; checkpoint warps cancel it.

For gaps, unusual geometry, or a specific handoff, use an optional `SurfaceTransition` override:

1. Put a box trigger at the entry region.
2. Assign Entry and Exit markers at safe **sphere centre** positions (default clearance 0.34 units).
3. Entry.forward is the required approach direction. Entry.up and Exit.up point out of their surfaces; Exit.forward sets movement heading after the transition.
4. Assign a Control marker for a quadratic curved path around the edge. Give the complete swept sphere clearance, not just the centre line.
5. Create a separate reverse link if returning should be supported. A link starts only while moving into its entry and with a compatible surface normal.

Links check obstruction while moving and stop with a message if blocked. They are authored prototype transitions, not automatic traversal of arbitrary geometry. Test each link from the width of its entry region and against the intended camera angle. Octagonal interior wall corners use the normal concave attachment path.

## Stealth rules

Outside crowds, match local ambient. Inside a core, match civilian temperature. Within the outer band, either is accepted. Heating/cooling approaches a target without overshooting. Movement adds heat based on actual distance travelled, including climbs and transitions; passive drift slowly returns toward ambient.

Only visible, thermally mismatched players build suspicion. Cover, matching, inactive cameras, safe zones, and recovery grace prevent new gain; suspicion decays. Wardens start pursuit at the configured threshold. Once alerted, they continue to track an identified player with line of sight even if temperature matches. During pursuit their normal cone is supplemented by **Chase Awareness Radius**, a 360-degree range defaulting to 4 units (capped by sensor range), shown as a red ring. Walls still block all sight samples. Patrol does not use the chase-only 360-degree awareness.

The Warden prefab adds a short, wide zone alongside its longer cone: **Vision Sensor → Close Range** defaults to 2 units and **Close Field Of View** to 180 degrees. Both settings are inherited by the scene instance; the close angle is adjustable from 1 to 360 degrees. The close zone is measured from the eye and covers low and side approaches in front that fall outside the narrower distant cone. Two visible arcs show its horizontal and vertical extents, clipped by walls. Set Close Range to zero to disable it. Security cameras default to zero. The long cone still uses the separate **Range / Field Of View** settings. Both zones obey occlusion, thermal matching, safe zones, grace, and normal suspicion buildup.

Before the chase threshold, the first visible mismatch puts a warden into **Suspicious**: it pauses its route, shows an orange cone and `SUSPICIOUS` label, and turns toward the player's observed position. **Warden Brain → Suspicious Turn Speed** defaults to 150 degrees/second. The moving cone can keep a crossing player in sight, but the suspicion gain rate and chase threshold are unchanged. Matching or breaking visibility stops position updates; the warden faces the last observation until suspicion decays to zero, then resumes patrol. Suspicious wardens use their long and close vision zones, without chase-only rear awareness. Navigation's automatic rotation is suspended during the pause and restored for patrol or pursuit. Civilian alarms still trigger an immediate chase.

**Warden Brain → Lost Sight Grace** defaults to 1.5 seconds. While sight is lost, the warden runs toward a destination extrapolated from the last observed position and velocity, capped by **Prediction Seconds** (0.5). Hidden player movement never updates that destination. After grace expires it searches that location for **Investigation Seconds** (5), then returns to patrol. A fresh thermal detection can restart pursuit during search. **Run Acceleration** (16) and **Run Turn Speed** (540 degrees/second) make close turns more responsive; patrol restores the NavMeshAgent's original values. Reaching full warden suspicion does **not** capture remotely.

Civilian contact alerts nearby wardens, including while standing in personal space as grace ends, but does not directly reset the player. Warden contact requires line of sight to prevent grabs through walls. Camera suspicion reaching full triggers a checkpoint reset. UI remains live during these events and closes when caught. Completion stops gameplay immediately.

## Takedowns

Approach an unaware warden from behind and press **E** when **take down warden** appears. The shared Warden prefab's **Warden Takedown → Interaction Range** defaults to 1.5 units; **Rear Angle** is a 120-degree full arc centred behind its body. Walls block the interaction. Notes/keypad screens and active climb transitions block it as well. By default, the warden must be patrolling with zero suspicion; **Allow Alerted Takedowns** optionally permits rear takedowns of suspicious/chasing/searching wardens.

A takedown immediately disables navigation, perception, capture, and physical colliders, hides security indicators, and lays the visual body on its side. It does not pause the game or disable other enemies/cameras. **Restore On Checkpoint** defaults to enabled: a checkpoint reset restores the warden at its initial patrol position with its original components, colliders, and visual pose. Turning that option off keeps the warden down for the current scene's run. **Warden Takedown View** owns the body reference and adjustable pose offset/rotation; this prototype uses a direct pose change rather than an animation sequence.

## Validation

`Tests/PlayMode` contains Unity Test Runner checks for crowd transitions/overlaps, cooling after a warp, physical surface movement/blocking, note pickup and reset persistence, sight occlusion, civilian contact, camera alarms during reading, and keypad completion. Run the **ThermalTail.Prototype.Tests** assembly in Play Mode. The example-scene tests additionally exercise its authored links and baked navigation.

`Tests/EditMode` checks connected prefab instances, preserved scene references, shared tuning propagation, repeatable migration, and rebuilding without overwriting existing prefab/material assets. Run **ThermalTail.Prototype.Editor.Tests** in Edit Mode.

Prototype limits: keyboard/mouse input, one local player, session-only persistence, simple marked planar surfaces with automatic adjoining corners and optional authored links, and an IMGUI presentation suitable for proving behavior. Sight visuals show a sampled wire cone clipped against the world; they are not a volumetric visibility mesh. The new level's patrol timing, concealment handoffs, camera coverage, and heating/cooling balance still need authored playtesting.
