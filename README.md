# Pocket Drive — Android prototype 0.3.4

A small offline driving prototype made in Godot 4.7.2 with GDScript. The environment uses original simple geometry. The car uses the supplied Porsche 911 Carrera 4S model by Karol Miklas, adapted under CC BY-SA 4.0. See `assets/porsche/ATTRIBUTION.md` and the in-game pause menu credits.

## Play

- Hold GAS to accelerate.
- Hold either steering arrow while pressing GAS to turn.
- Hold BRAKE / R to stop, then keep holding to reverse.
- Reset returns the car to the starting street.
- Pause → Switch car (or V) changes between the 911 Carrera 4S and, when installed, the 911 GT3 RS.
- Camera (top right) cycles seven views: Chase, Far chase, Hood, Bumper, Driver, Top-down and Cinematic roadside cameras.
- The minimap (top left) shows roads, buildings and your heading. North is up.
- Pause stops the simulation. Switching away from the app also pauses it.
- Keyboard fallback: WASD or arrow keys, Space for brake, R to reset, C to change camera, V to switch car, Escape to pause.

## Scope

One Porsche 911 Carrera 4S with arcade handling in a 640 m modern city: glass towers downtown, offices in midtown, apartments further out, a green belt, a perimeter loop road, a roundabout where the two central boulevards meet, a park, a mall car park, a drift lot, lane markings, crosswalks, streetlights and traffic lights, solid buildings and boundaries, a follow camera, speed display, and multi-touch controls. This is a handling prototype, not the finished visual style. No missions, traffic, multiplayer, or saved progress yet. Phone performance and visual layout still need on-device evaluation.

## Open in Godot

Import `project.godot` in Godot's project manager. Press F6/F5 to run. The Android preset builds an ARM64 debug APK; this is for local testing, not store publication. The Android SDK and Java paths are stored in the Mac's Godot editor settings, not in this project.

## Checks

The headless smoke test verifies the city layout (no buildings or trees on roads, spawn on a road, geometry batched), the roundabout island and off-road speed limit, acceleration, braking/reverse, steering direction, wall collision, reset, two-finger controls, independent finger release, and pause/resume:

```sh
/Applications/Godot.app/Contents/MacOS/Godot --headless --path . --fixed-fps 60 --script res://tests/smoke.gd
```

A successful automated test does not verify Android frame rate, visual appearance, or touch feel on the actual phone.

## Engine credit

Made with Godot Engine, distributed under the MIT license. License and third-party notices: https://godotengine.org/license/

Version 0.1.1 fixes off-screen driving pads and adds control-boundary checks at standard and wide phone resolutions.

Version 0.2.0 replaces the placeholder car with an optimized Porsche (83,711 triangles), animated wheels, corrected ground clearance, and model credits. Handling and touch controls are retained. Android visual appearance and frame rate require on-device testing.

Version 0.2.1 corrects baked front-wheel yaw and hub symmetry, keeps brake calipers stationary during wheel spin, and adds geometry-based wheel regression checks. Missing wheels now log an error and are skipped safely in release builds. The HUD subtitle is version-neutral. Handling, touch controls and the world are unchanged.

Version 0.2.2 brings the chase camera closer (5.3 m instead of 7.5 m behind the car) and lowers its height above the follow target from 3.8 m to 2.6 m. Camera smoothing, field of view, collision avoidance and driving behavior are unchanged.

Version 0.3.0 replaces the 240 m test town with a 640 m modern city (seven streets per axis plus a perimeter loop) and adds a minimap. City geometry is merged per material into 160 m chunks, so the whole city is about 220 meshes. Off-road detection now uses the city layout instead of hard-coded street positions. Lighting was retuned (AgX tone mapping, softer sun and ambient, weaker sky reflections on matte surfaces) because the previous settings washed out colors. Handling and touch controls are unchanged.

Version 0.3.1 adds a Camera button (and C key) with seven views. The top-right buttons now anchor to the corner and grow leftwards, and the smoke test checks that they stay on screen and that every camera is placed correctly.

Version 0.3.2 adds a built cockpit for the Carrera's driver view (the model has no interior) and an optional second car, the 2019 Porsche 911 (991.2) GT3 RS by Ddiaz Design (CC BY-NC-SA 4.0, non-commercial use only). The GT3 RS model is kept in `assets/gt3rs/`, which git ignores: it is not published with the source, and the game offers only the Carrera when it is absent. Its node and material names suggest it may come from a commercial game, so it should stay out of any public or paid release.

Version 0.3.3 adds sound, all synthesised in code at start-up (no audio files): an engine whose revs follow a six-speed automatic gearbox (the GT3 RS revs higher and sounds sharper), tyre screech in fast turns and hard braking, wind and road noise that rise with speed, a crash thud on hitting walls, trees or posts, button clicks, and a Sound On/Off button in the pause menu.

Version 0.3.4 fills wide phone screens (the view now expands instead of showing black bars) and fixes the driver, hood and bumper cameras trailing the car by one physics step, which put the driver camera behind the seat at top speed. The headless smoke test may print an "ObjectDB instances were leaked" warning for sound streams at exit; that comes from Godot's dummy audio driver and does not affect the game.
