# Thermal Tail (Unity)

Unity port of Pit's Thermal Tail - thermal-stealth lizard platformer. All 7
original levels imported from the browser build with the exact simulation
tuning constants.

## Open it
1. Install Unity Hub, then Unity **6000.3.5f2** (Unity 6). No extra modules
   needed for editing.
2. Clone this repo, open the folder in Unity Hub.
3. Open a level: `Assets/ThermalTail/Levels/01_Fernlight_Verge.unity`.
4. Press Play.

## Edit a level
Everything in a level is a prefab instance from `Assets/ThermalTail/Prefabs/`
(Warden, Surveillance Lens, Warm Vent, Thermal Gate, Ledge...). Drag one into
the scene, move it, press Play to test. Level rules (quota, ambient temp,
start temp) live on the LevelSettings object in each scene.

Original level data + importer: `Assets/ThermalTail/Editor/TTLevelImporter.cs`.
