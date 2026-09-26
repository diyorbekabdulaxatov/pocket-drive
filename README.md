# Pocket Drive — Android prototype 0.2.0

A small offline driving prototype made in Godot 4.7.2 with GDScript. The environment uses original simple geometry. The car uses the supplied Porsche 911 Carrera 4S model by Karol Miklas (Lionsharp Studios), adapted under CC BY-SA 4.0. See `assets/porsche/ATTRIBUTION.md` and the in-game pause menu credits.

## Play

- Hold GAS to accelerate.
- Hold either steering arrow while pressing GAS to turn.
- Hold BRAKE / R to stop, then keep holding to reverse.
- Reset returns the car to the starting street.
- Pause stops the simulation. Switching away from the app also pauses it.
- Keyboard fallback: WASD or arrow keys, Space for brake, R to reset, Escape to pause.

## Scope

One Porsche 911 Carrera 4S with arcade handling, a compact street grid, a park, a maneuvering lot, solid buildings and boundaries, a follow camera, speed display, and multi-touch controls. This is a handling prototype, not the finished visual style. No missions, traffic, multiplayer, audio, or saved progress yet. Phone performance and visual layout still need on-device evaluation.

## Open in Godot

Import `project.godot` in Godot's project manager. Press F6/F5 to run. The Android preset builds an ARM64 debug APK; this is for local testing, not store publication. The Android SDK and Java paths are stored in the Mac's Godot editor settings, not in this project.

## Checks

The headless smoke test verifies acceleration, braking/reverse, steering direction, wall collision, reset, two-finger controls, independent finger release, and pause/resume:

```sh
/Applications/Godot.app/Contents/MacOS/Godot --headless --path . --fixed-fps 60 --script res://tests/smoke.gd
```

A successful automated test does not verify Android frame rate, visual appearance, or touch feel on the actual phone.

## Engine credit

Made with Godot Engine, distributed under the MIT license. License and third-party notices: https://godotengine.org/license/

Version 0.1.1 fixes off-screen driving pads and adds control-boundary checks at standard and wide phone resolutions.

Version 0.2.0 replaces the placeholder car with an optimized Porsche (83,711 triangles), animated wheels, corrected ground clearance, and model credits. Handling and touch controls are retained. Android visual appearance and frame rate require on-device testing.
