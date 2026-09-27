# Pacific Coast — original city prototype

Open `Assets/PocketDrive/Scenes/CoastalCity.unity` and press Play.

This is an original, Los Angeles-inspired city layout built from procedural geometry, not a reconstruction of Los Angeles or an imported GTA map. It is an initial playable environment; its simplified architecture and materials do not match GTA V's visual quality.

## Included

- 600 × 600 metre street grid, 25 intersections, lane markings and crossings.
- Six-lane elevated freeway loop within an approximately 860 × 860 metre footprint.
- Two access ramps connecting the south boulevard and freeway.
- 64 buildings, with a stepped skyline landmark, rooftop equipment, storefront glazing, and residential blocks.
- 263 palms, pavement, streetlights, static traffic-light props, directional signs, and distant hills.
- Coastal strip and beach promenade west of the freeway.
- Existing keyboard/touch input and arcade handling, a scene-specific tuning asset for ramps, and a small HUD/minimap.

Controls: WASD/arrows, R to reset, M to toggle the schematic minimap; on Android hold DRIVE and a steering button together. The minimap is a schematic grid/freeway overview, not turn-by-turn navigation. Traffic lights are decorative; no AI traffic or pedestrians are included.

## Build and edit

- City build: **Pocket Drive → City → Build Coastal City Android APK**.
- Output: `Builds/Android/PocketDrive-coastal-city.apk` (development signing).
- Generator: **Pocket Drive → City → Create or Rebuild Coastal City**.
- Rebuilding replaces `CoastalCity.unity` and its generated meshes/materials. Duplicate the scene before making hand edits you want to preserve.
- Generated meshes are grouped by material and 120 m sector to allow view culling. Facade textures are small, tiled textures. This is preparation for mobile profiling, not a verified frame-rate guarantee.
- The original sandbox and its handling/input work are preserved. The existing generic **Build Android Development APK** command still builds the sandbox; use the city-specific command for this map.

The generator makes the city the first scene in Editor Build Settings and keeps the sandbox as the second scene. It does not change the existing global Android application ID or store-signing configuration.

## Validation

Unity generation and rendering passed. Integration checks verify all 25 intersection centers, the full freeway centerline, sampled road surfaces on both ramps, driving from spawn, and physical ascents of both ramps using the existing car controller and a low-speed automated steering input.

This does not establish real-device performance or validate every possible crash, camera position, or driving trajectory. Android touch behavior, sustained frame rate, thermal behavior, and visual quality should be checked on the target phone.

Rendered previews are in `outputs/coastal-city-street.png`, `outputs/coastal-city-driving.png`, and `outputs/coastal-city-overview.png`. These are real Unity renders, not concept art. They omit the runtime HUD.

## Next art pass

More varied building silhouettes and facades, realistic palm textures, storefront detail, road wear, curb transitions, improved car art, lighting/reflections, and LOD/culling tuning. No ads, accounts, purchases, gameplay missions, traffic simulation, or new commercial services were added in this map pass.
