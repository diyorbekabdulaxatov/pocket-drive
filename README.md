# Pocket Drive — Unity Android prototype

This folder is now the Unity project root. Open it with **Unity 6000.6.3f1**.

## Run in the editor

1. In Unity Hub, choose **Add → Add project from disk** and select this folder.
2. Open `Assets/PocketDrive/Scenes/DrivingSandbox.unity`.
3. Press Play. Use **WASD / arrow keys** to drive and **R** to reset.
4. Android uses the on-screen LEFT, RIGHT, BRAKE/reverse, and DRIVE controls. Hold steering and DRIVE together with two fingers.

The scene is a handling sandbox with placeholder geometry, not the finished commercial game. It uses Unity's built-in render pipeline and a simple arcade Rigidbody controller. URP, final vehicle physics, art, and gameplay mode are later decisions. The prototype uses the legacy Input Manager; replace its input adapter when production controls are selected.

## Android development build

Use **Pocket Drive → Build Android Development APK**. Output:
`Builds/Android/PocketDrive-development.apk`

Configuration: landscape, ARM64, IL2CPP, minimum Android API 26, installed automatic target SDK. Builds use development signing. The application ID `com.pocketdrive.prototype` is a placeholder; choose a permanent owned identifier before store registration. This is not a Play Store release build.

On a connected Android phone, enable Developer options and USB debugging, authorize this Mac, then use Unity's Android Build and Run workflow or install the development APK with Android's `adb install -r` command. Actual phone performance and multi-touch behavior require device testing.

For command-line builds (close this project in the editor first):

```sh
'/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity' \
  -batchmode -nographics -quit \
  -projectPath "$PWD" -buildTarget Android \
  -executeMethod PocketDrive.Editor.ProjectSetup.BuildAndroid \
  -logFile /tmp/pocket-drive-build.log
```

`Pocket Drive → Rebuild Sandbox Scene` regenerates the sandbox scene from code and overwrites hand edits. The headless handling check is `PocketDrive.Editor.SandboxChecks.Run` (use it with `-executeMethod` like the build command).

`Pocket Drive → Configure Android Project` reapplies prototype settings and creates the sandbox only if its scene file is absent. The build command also reapplies these prototype settings; update the setup script before changing release identifiers or versioning. It does not overwrite an existing sandbox scene.

## Layout

- `Assets/PocketDrive/Scripts`: car handling (`ArcadeCar`, `CarTuning`), controls (`CarInput`), follow camera, prototype HUD, app bootstrap.
- `Assets/PocketDrive/Settings`: tuning assets such as `DefaultCarTuning`.
- `Assets/PocketDrive/Editor`: project configuration and Android build automation.
- `Assets/PocketDrive/Scenes`: editable sandbox scene.
- `Assets/PocketDrive/Art`: generated prototype materials.
- `Packages` and `ProjectSettings`: pinned Unity project configuration.
- `outputs`: original concept artwork preserved outside the game asset import pipeline.
- `Pocket-Drive-Technical-Requirements.md`: commercial roadmap and open decisions.

Commit Unity `.meta` files with assets. Generated caches, local settings, APKs, and signing secrets are ignored by Git.

## Scope

Implemented: Unity project, Android configuration, basic car movement, chase camera, driving pad/obstacles, reset, keyboard/touch prototype HUD, and build automation.

Not implemented: finalized racing/drifting/parking mode, progression, login, cloud saves, ads, purchases, subscriptions, notifications, daily prizes, audio, or production UI. These remain planned work in the requirements document.

## Credits

- Car: "(FREE) Porsche 911 Carrera 4S" by Karol Miklas, adapted, CC BY-SA 4.0. See `Assets/PocketDrive/Vehicles/Porsche911/ATTRIBUTION.md`. Vehicle design and trademarks belong to their owners; no endorsement implied.
- Textures: Poly Haven, CC0. See `Assets/PocketDrive/Textures/CREDITS.md`.
