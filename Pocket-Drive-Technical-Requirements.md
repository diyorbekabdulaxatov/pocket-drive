# Pocket Drive — Technical Requirements and Build Plan

Version: 0.2
Date: 27 September 2026
Status: Working plan. Replaces the 0.1 discussion draft (see git history) and folds in the two Claude reviews: the requirements review and the Unity prototype review.

Section 7 is the step-by-step plan. Each step is marked **Done**, **In progress**, **Next**, or **Needs your OK** (steps that are slow or awkward to undo).

## 1. Goal

A polished, offline-friendly arcade driving game for Android phones. It must be fun without paying and must never lose a player's progress or purchases.

## 2. Decisions

| Topic | Decision | Status |
|---|---|---|
| Platform | Android first. iOS is out of scope for 1.0. | Confirmed |
| Game mode | **Parking and time-trial challenges** on short handcrafted levels. | Default. Not confirmed by Diyorbek yet; can change before Step 8. |
| Visual style | **Realistic**: supercars and real-looking 3D cars in a believable city. No cartoon or low-poly look. | Confirmed by Diyorbek |
| Assets | **Free only**, with licences that allow commercial use (for example CC0 textures from Poly Haven or ambientCG). | Confirmed by Diyorbek |
| Map | Keep Codex's Coastal City layout (street grid, freeway loop, ramps) and replace its art area by area, starting near the spawn. | Confirmed by Diyorbek |
| Vehicle physics | Custom Rigidbody controller, not WheelColliders. Moving from pure arcade toward per-wheel suspension and weight transfer to suit supercars. | Updated |
| Backend | **No own backend for 1.0.** Local saves on the device. If cloud save or purchase checks become necessary, use a managed service (for example Unity Gaming Services) instead of running a server. | Confirmed by review |
| Ads | **No banner ads.** "Daily banners" means in-game announcement cards on the home screen. Optional rewarded ads may be added after launch. | Confirmed by review |
| Purchases | Small catalog through Google Play Billing (Unity IAP), added after the core game is proven. | Planned for Phase C |
| Subscription | After launch, only once there is a steady stream of new content to offer. | Deferred |
| Multiplayer | Out of scope. | Confirmed |
| Unity version | 6000.6.3f1 today. Move to the current LTS release before content production if 6000.6 is not LTS. | Needs your OK |
| Render pipeline | URP 17.6 with ACES tonemapping, light bloom and MSAA 2x. | Done |
| Input | Move from the legacy Input Manager to the Input System package. | Needs your OK |

## 3. Scope of the first release (1.0)

In:
- One car to start, then 3 to 5 unlockable cars (colour and stat variants).
- 20 to 30 short levels: parking bays and timed checkpoint runs, 1 to 3 stars each.
- Touch controls with steering smoothing, a reset button, and pause.
- Home, level select, garage, results, settings screens.
- One soft currency earned by playing; spent in the garage.
- Local save with versioning and a backup copy.
- Announcement cards on the home screen, loaded from a bundled config file (remote config later).
- Basic audio: engine, collisions, UI.

Out (later phases): accounts and login, cloud save, purchases, rewarded ads, daily prizes that need server time, push notifications, subscription, leaderboards.

## 4. Driving requirements

- Controls: accelerate, brake, reverse (after the car stops), steer, reset to last safe point.
- Brake and reverse are separate behaviours even if they share a button.
- Steering input is smoothed; steering angle reduces as speed rises.
- The car's collider has no friction; all grip is done in code so tuning values mean what they say.
- Ground detection uses a layer mask and is robust on slopes and small bumps.
- Decorative objects (lane markings, props) have no colliders unless they are meant to be hit.
- Collisions feel physical: the car bounces or slides off walls instead of sticking to them.
- All handling values live in one tunable asset per car.
- Physics runs at a fixed 50 Hz and must behave the same at 30 and 60 FPS.
- Pausing, backgrounding the app, and phone calls must not break the physics state.

## 5. Challenge rules

**Parking level:** drive to a marked bay and stop fully inside it, facing the required direction, within the time limit. Hitting obstacles costs time or stars. Stars come from time taken and number of hits.

**Time-trial level:** pass checkpoints in order and cross the finish. Missing a checkpoint means the player must go back for it. Stars come from finish time.

Each level is a scene or prefab with its own rules asset (time limits, star thresholds, reward).

## 6. Technical requirements

### Client structure
- Code under `Assets/PocketDrive/Scripts`. Split into area subfolders (`Vehicle`, `Input`, `Challenges`, `UI`, `Save`, `Core`) once there are enough files to need it.
- Input is read by an input component and handed to the car as plain values, so the car never reads keys or touches itself.
- Tuning and level rules in ScriptableObject assets.
- No third-party SDKs in Phase A.

### Saves (Phase B)
- JSON save with a schema version, written to a temp file then swapped in, with a last-good backup.
- Stores stars per level, best times, currency, owned cars, settings.

### Performance targets
- 60 FPS on a mid-range phone; never below 30 FPS on the minimum device.
- No frame-time spikes from garbage collection during driving (no per-frame allocations in gameplay code).
- APK under 150 MB for 1.0.

### Android settings
- Landscape, ARM64, IL2CPP, minimum API 26, latest target API required by Google Play.
- Final application ID must be chosen before the first Play upload; it can never change afterwards.
- Release builds use an upload key kept outside the repo; version code increases on every upload.

### Privacy and store
- Privacy policy and Play data-safety form before launch. With no accounts, ads or analytics in 1.0, data collection is minimal.
- Asset licences recorded for every model, sound and font.

### Later phases (only when needed)
- Purchases: Unity IAP with Google Play; verify with a managed service or Google Play's server APIs, never trust the client alone.
- Rewarded ads: shown only when the player chooses, never during driving.
- Accounts and cloud save: Google Play Games sign-in or a managed service. No custom passwords.
- Subscription: only with a defined monthly content schedule.

## 7. Step-by-step plan

### Phase A — playable prototype

1. **Done.** Write this plan.
2. **Done.** Fix the car controller physics: frictionless car collider, robust ground check with a layer mask, remove lane-marking colliders, split brake from reverse, steering smoothing and speed-based steering, less sticky collisions, clean reset.
3. **Done.** Separate input from the car: a `CarInput` component for keyboard and on-screen touch buttons, handling values in a `CarTuning` asset (`Assets/PocketDrive/Settings/DefaultCarTuning.asset`), frame-rate setup moved to `GameBootstrap`.
4. **Needs your OK.** Switch to the Input System package (needs a Unity editor restart).
5. **Done.** Switch the render pipeline to URP and convert materials (`Pocket Drive → Switch to URP`).
6. **Needs your OK.** Decide the Unity version: stay on 6000.6.3f1 or move to the current LTS.
7. **Done.** Better chase camera: heading follows the car smoothly so spins and reversing don't whip the view.
7a. **Done (first pass).** Realistic first area in Coastal City: CC0 photo textures (Poly Haven) on roads, pavements, stucco and brick with real-world scale, a 116-bay parking lot south of Palm Boulevard, and the Porsche 911 Carrera 4S model (Karol Miklas, CC BY-SA 4.0) on the player car. Before a paid release the Porsche must be debadged and renamed, or replaced.
7b. **Next.** Performance check of the Coastal City build on Diyorbek's phone.
7d. **Done (first pass).** AI traffic: 12 cars keep to the right lane, turn at intersections, stop at timed lights and brake for the player and each other. Pedestrian system written; it activates when a character model is placed in `Assets/PocketDrive/Characters`.
7e. **Waiting for Diyorbek.** Character model and walking animation from Mixamo for pedestrians.
7c. **Next.** Wheels that spin and steer; realistic trees and building models from free Asset Store packs added to Diyorbek's Unity account.
8. **Next.** Parking challenge: parking bay, stop detection, timer, hit counter, stars, results panel, restart.
9. **Next.** Time-trial challenge: ordered checkpoints, timer, finish, stars.
10. **Next.** Pause menu and app-background handling.
11. **Next.** First engine, collision and UI sounds.
12. Build 3 parking and 3 time-trial test levels. Playtest on a phone (Diyorbek runs the build).

Exit: someone outside the team understands and enjoys a level without help, at a steady frame rate on the test phone.

### Phase B — game foundation

13. Replace the IMGUI prototype HUD with real UI (UGUI or UI Toolkit): home, level select, results, settings.
14. Local save system with versioning and backup.
15. Level progression with stars and unlocks.
16. Soft currency and garage with 3 to 5 cars.
17. Announcement cards on the home screen from a bundled config file.
18. Settings: sound, steering sensitivity, control layout.
19. 20 to 30 levels.
20. Performance pass on low and mid-range phones.

### Phase C — release

21. Final app ID, icon, store listing, privacy policy, data-safety form.
22. Signed release build (AAB) and Play internal testing, then closed testing.
23. Optional: purchases through Unity IAP and Google Play Billing.
24. Optional: rewarded ads outside driving.
25. Staged production release.

### Phase D — after launch

26. New levels and cars on a regular schedule.
27. Remote announcement cards, daily challenges, cloud save if players ask for it.
28. Subscription, once the content schedule can support it.

## 8. Open questions for Diyorbek

1. Is parking plus time-trial the right game mode? (Default until you say otherwise.)
2. OK to switch to URP and the Input System now? (Steps 4 and 5.)
3. Stay on Unity 6000.6.3f1, or move to the LTS version? (Step 6.)
4. Which phone is the main test device?
5. What permanent application ID do you want (for example `com.yourname.pocketdrive`)?
