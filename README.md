# Pocket Drive — Android prototype 0.3.0

A small offline driving prototype made in Godot 4.7.2 with GDScript. The environment uses original simple geometry. The car uses the supplied Porsche 911 Carrera 4S model by Karol Miklas, adapted under CC BY-SA 4.0. See `assets/porsche/ATTRIBUTION.md` and the in-game pause menu credits.

## Play

- Hold GAS to accelerate.
- Hold either steering arrow while pressing GAS to turn.
- Hold BRAKE / R to stop, then keep holding to reverse.
- Reset returns the car to the starting street.
- The minimap (top left) shows roads, buildings and your heading. North is up.
- Pause stops the simulation. Switching away from the app also pauses it.
- Keyboard fallback: WASD or arrow keys, Space for brake, R to reset, Escape to pause.

## Scope

One Porsche 911 Carrera 4S with arcade handling in a 640 m modern city: glass towers downtown, offices in midtown, apartments further out, a green belt, a perimeter loop road, a roundabout where the two central boulevards meet, a park, a mall car park, a drift lot, lane markings, crosswalks, streetlights and traffic lights, solid buildings and boundaries, a follow camera, speed display, and multi-touch controls. This is a handling prototype, not the finished visual style. No missions, traffic, multiplayer, audio, or saved progress yet. Phone performance and visual layout still need on-device evaluation.

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
