# UPGRADE_PLAN.md

Phase 0 technical audit for turning the existing **Car Parking Simulator** Unity project into a more complete commercial-style parking game, per `prompt2.txt`. This document is the required Phase 0 deliverable: it records what exists today, what must be preserved, what will be refactored, and the execution order. It will be kept up to date as phases complete.

Audit performed against `D:\Projects\CarParkingGame` on branch `fix/android-landscape-and-build-scene` (working tree clean at audit time; `prompt2.txt` was an untracked file at the repo root).

---

## 1. CURRENT ARCHITECTURE

### Project origin
Built from Unity's **"3D Mobile" template** (`com.unity.template.mobile3d@6.0.0`) and never fully rebranded — `productName` is `CarParkingGame` but `companyName` is still `DefaultCompany` and the Android `applicationIdentifier` is still the template default `com.UnityTechnologies.Mobile3DTemplate`.

### Folder layout
```
Assets/
  GameAssets/
    Scripts/        <- all 9 gameplay .cs scripts, flat, no subfolders, no namespaces
    CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity   <- THE real gameplay scene
    50s, 60s and 70s Car Pack (6 Cars)/   <- source car models (visual-only prefabs)
    Hatchback and Sedan/                  <- unused 3rd-party car pack
    Prototype Collection/                 <- cones/barriers/props source
    Audio/, Sprites/, SimplePixelUI/
  Plugins/SimpleInput/     <- mobile virtual joystick/button plugin
  Settings/                <- URP pipeline assets (Mobile_RPAsset, PC_RPAsset)
  Prefabs/                 <- Mission1.prefab … Mission8.prefab (ORPHANED, unreferenced anywhere)
  Scenes/SampleScene.unity <- stock template scene, disabled in build, unused by gameplay
  JMO Assets/, TutorialInfo/, Adaptive Performance/
```

### Gameplay scripts (all in `Assets/GameAssets/Scripts/`, flat, no namespace)
- **CarController.cs** — Rigidbody + WheelCollider based car physics. `ControlMode` = Keyboard or Buttons (SimpleInput). All 4 wheels receive motor torque (no real drive-axle distinction despite a `Axel` enum existing); only Front-axel wheels steer. `maxAcceleration` is used elsewhere as a de-facto "freeze car" flag (set to `0`/`5` by `ParkingTrigger`/`MissionFailedHandler`). Exposes `MoveInput` (get) and two apparently-dead setters (`SetMoveInput`, `SteerInput`) that nothing else calls.
- **CarCameraController.cs** — third-person follow/orbit camera. Picks the active car by linearly scanning a `Transform[] cars` for the first `activeSelf == true`. Orbit rotation reads desktop `Input.GetAxis("Mouse X/Y")` only — **no mobile touch-drag equivalent**.
- **CarEngineAudio.cs** — builds 4 `AudioSource`s at runtime per car (start/idle/onThrottle/offThrottle), cross-fades based on speed and throttle, reacts to `Time.timeScale` to mute during pause/menu.
- **CarSelection.cs** — reads/writes `PlayerPrefs "SelectedCarIndex"`. Exists on **two** GameObjects in the scene (the showroom `SelectCar` picker and the actual `PlayerCars` gameplay container); `OnDoneButton()` finds all `CarSelection` instances via `FindObjectsByType` and re-syncs them.
- **GameManager.cs** — classic static singleton (`Instance` + `DontDestroyOnLoad`). Owns the mission arrays: `missionStartPoints[8]`, `playerCars[3]`, `missionAreas[8]`, `parkingTriggers[8]`, `missionCompleted[8]` (bool), `currentMission` (int). `SaveProgress()`/`LoadProgress()` read/write PlayerPrefs directly. `SpawnPlayerAtMissionStart()` teleports **all** player cars, not just the active one, to the current mission's start transform.
- **MainMenuManager.cs** — panel toggling (main menu / select car / select mission), pause via `Time.timeScale`, dynamically enables mission buttons from `missionCompleted[]`. Has a dead public field `carControllerScripts` (never read).
- **MissionFailedHandler.cs** — `OnCollisionEnter` against tag `"Cone"` immediately fails the mission (zeroes `maxAcceleration` on all wired cars). Present on only **3 of 8** mission areas — the other 5 missions currently cannot be failed at all.
- **ParkingTrigger.cs** — `OnTriggerEnter` against collider tag `"Player"` (or `"Reverse"` if `isReverseMission`) instantly completes the mission. No angle/speed/hold-time check of any kind — pure trigger-touch detection. Present 8 times, all GameObjects literally named `ParkingTrigger` (differentiated only by hierarchy position and per-instance `isReverseMission`).
- **OrientationLock.cs** — static, no MonoBehaviour, `[RuntimeInitializeOnLoadMethod]` forcing landscape autorotation as a runtime safety net on top of the Android manifest. This is the fix this branch was created for and should be left alone.

### Existing scenes
- `Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity` — **the real, fully-wired gameplay scene** (~218k lines). Contains `GameManager`, all 3 cars, all 8 `ParkingTrigger`s, all UI.
- `Assets/Scenes/SampleScene.unity` — stock template scene (camera + light + global volume only). Registered but **disabled** in Build Settings; still referenced as `templateDefaultScene`. Not used by gameplay.
- Build Settings (`ProjectSettings/EditorBuildSettings.asset`) currently has exactly these 2 scenes; only `complete_track_demo.unity` is enabled/ships.
- Several other `.unity` files exist but are all unused 3rd-party asset-pack demo scenes (car packs, Prototype Collection, SimpleInput example, JMO Cartoon FX demo) — not part of the shipping game, not touched.

### Existing car setup
- 3 playable cars, hand-configured **directly in the scene** (not as reusable prefabs): `ClassicCarRed`, `1950sHotRod`, `MuscleCar`. Each carries `CarController` + `CarEngineAudio` + WheelColliders + Rigidbody.
- The underlying imported car-pack prefabs (e.g. `1950sHotRod.prefab`) are pure visual meshes with **no** `CarController`/`WheelCollider`/physics — all physics setup exists only on the in-scene instances, with no prefab as source of truth.
- **Bug found:** `ClassicCarRed`'s GameObject is tagged `MainCamera` instead of `Player`. Since `ParkingTrigger.OnTriggerEnter` requires tag `"Player"` for forward-parking missions, **forward parking missions likely cannot be completed while this car is selected.** This needs to be fixed early (Phase 1/3) regardless of the broader refactor.

### Current mission architecture
8 hardcoded parallel arrays on `GameManager` (`missionStartPoints`, `playerCars`, `missionAreas`, `parkingTriggers`, `missionCompleted`), indexed positionally with no `MissionID`. `missionCompleted[bool]` conflates "unlocked" and "completed" (completing mission N sets `missionCompleted[N+1] = true` to unlock the next one, before it has actually been played). Only 3 of 8 missions have any fail condition. No score, no stars, no timing, no angle/speed validation — pure "touch this trigger" completion.

### Current save/progression (PlayerPrefs — full list, confirmed by project-wide grep, nothing else uses PlayerPrefs anywhere)
| Key | Type | Written/read by |
|---|---|---|
| `SelectedCarIndex` | Int | `CarSelection.cs` |
| `CurrentMission` | Int | `GameManager.cs` |
| `Mission{i}Completed` (i = 0..7) | Int (0/1) | `GameManager.cs` |

### Current input solution
- **SimpleInput** plugin at `Assets/Plugins/SimpleInput/` drives the on-screen mobile joystick/buttons (`SimpleInput.GetAxis("Vertical"/"Horizontal")`, `SimpleInput.GetButton("Break")` — note the plugin's own button is literally named "Break", a misspelling, used consistently by both the UI and the code).
- Desktop/editor fallback uses legacy `Input.GetAxis`/`Input.GetKey`.
- `com.unity.inputsystem` 1.11.2 is installed and `activeInputHandler` is set to `"Both"`. No *gameplay* script uses `UnityEngine.InputSystem`.

  **Correction to the original audit:** it is not inert. The scene's `EventSystem` uses `InputSystemUIInputModule` (not `StandaloneInputModule`), so **all uGUI input — every menu and HUD button — goes through the new Input System**. Removing or disabling that package would break UI input entirely. This was missed on the first pass because the audit only grepped C# files, and the dependency lives in scene data. SimpleInput still drives the vehicle; both systems are load-bearing, for different things.
- `CarCameraController`'s orbit rotation has no mobile touch-drag support (desktop mouse only) — a real gap, candidate for Phase 5 camera work, not Phase 1.

### Current Android/URP setup
- Android `MinSdkVersion` 23, `TargetSdkVersion` 0 (auto), ARM64-only, IL2CPP scripting backend, Linear color space.
- Landscape-locked via Player Settings + `OrientationLock.cs`.
- URP: two quality levels defined, `"Mobile"` (index 0, `Mobile_RPAsset.asset`) and `"PC"` (index 1, `PC_RPAsset.asset`). **Bug found:** `QualitySettings.m_PerPlatformDefaultQuality.Android = 2`, but only indices 0/1 exist — out-of-range, will silently clamp at runtime instead of reliably selecting the intended Mobile quality level/pipeline. Also, `GraphicsSettings`' *global* default render pipeline asset is `PC_RPAsset.asset`, not the Mobile one (per-quality-level overrides exist, but the global fallback is the wrong one).
- Android `applicationIdentifier` still the template default — must be renamed at some point before store upload (flagged, not urgent for gameplay phases).

---

## 2. RISKS

1. **Scene-only car setup, no prefabs.** Any Editor "Revert to Prefab" action on `ClassicCarRed`/`1950sHotRod`/`MuscleCar` could strip their hand-added physics/audio components back to the bare visual mesh. Garage/vehicle work (Phase 12+) must avoid touching these GameObjects destructively; consider promoting them to proper prefabs early so future car-adding is safe, but only as a deliberate, tested step, not incidentally.
2. **`missionCompleted[]` conflates unlocked/completed state.** Scaling to 30 missions on the current array design would compound this. This is exactly the architecture problem the prompt calls out in section 2 — must be resolved before mission count grows (Phase 1/2).
3. **8 orphaned `Mission1..8.prefab` files** in `Assets/Prefabs/` are unreferenced anywhere. Unclear if they're intentional backups or dead weight. Will not delete without confirming with the user; will leave untouched and documented.
4. **`ClassicCarRed` mistagged as `MainCamera`.** Silent gameplay-breaking bug (forward parking can't complete for that car). Needs a one-line scene fix.
5. **Legacy uGUI `Text`, not TextMeshPro**, is used throughout existing UI. New UI (mission selection grid, HUD elements) should stay consistent with legacy `Text` unless there's a strong reason to introduce TMP (`com.unity.ugui` 2.0 bundles TMP so it's available, but mixing systems adds inconsistency risk for no real gain).
6. **New Input System package is installed but unused; SimpleInput is the real input path.** New scripts must not accidentally start depending on `UnityEngine.InputSystem` — stay on `SimpleInput`/legacy `Input` for consistency, per explicit prompt instruction not to swap input systems.
7. **All gameplay logic currently lives directly in the scene**, not prefabs — migrating to data-driven `MissionDefinition` ScriptableObjects (Phase 1) must read the *existing* 8 missions' actual transform/trigger data out of the scene rather than re-authoring it from scratch, to avoid silently changing existing mission layouts.
8. **QualitySettings/GraphicsSettings misconfiguration** (see above) is a pre-existing bug unrelated to this upgrade; worth a small, isolated fix (Android default quality index, global RP asset) early since it's low-risk and affects every later performance phase.
9. **Instant-fail-on-cone-touch** and **instant-complete-on-trigger-touch** are both by design in the current build; replacing them (Phases 7–8) changes core gameplay feel and must be tuned carefully rather than assumed correct on first pass.

---

## 3. WHAT MUST BE PRESERVED

- The 3 existing playable cars, their current driving feel (WheelCollider + Rigidbody tuning), and their current visual/audio setup.
- SimpleInput as the mobile control system; the on-screen joystick/buttons must keep working throughout every phase.
- Landscape-only orientation and `OrientationLock.cs`.
- The existing 8 missions' actual level geometry/start points/trigger placement (to be wrapped in new data-driven definitions, not redesigned).
- Existing PlayerPrefs values, migrated forward rather than discarded.
- Android as the build target; IL2CPP/ARM64/landscape configuration.
- `complete_track_demo.unity` as the one real gameplay scene; `SampleScene.unity` stays disabled/unused as-is.

## 4. WHAT WILL BE REFACTORED (Phase 1 onward)

- `GameManager`'s monolithic mission arrays → `MissionManager` + `MissionDefinition` (ScriptableObject, static config) + `MissionProgressData` (serializable save data) with explicit `unlocked`/`completed`/`bestScore`/`bestStars`/`bestTime` fields per mission, keyed by a stable Mission ID rather than array index.
- Trigger-only pass/fail logic → `ParkingValidator` (position/angle/speed/hold-time based), `ScoreManager` (penalties instead of instant-fail).
- Direct `PlayerPrefs` progression storage → structured JSON save file under `Application.persistentDataPath`, with a one-time migration pass reading the 3 existing PlayerPrefs keys.
- `maxAcceleration = 0` as the generic "freeze the car" mechanism → a proper vehicle-enable/disable method on `CarController`.
- Car selection (`CarSelection` + hardcoded 3-car containers) → `GarageManager` + `VehicleManager` + `CarDefinition` data, while keeping the existing 3 cars' current stats/behavior as the baseline.

## 5. FILES EXPECTED TO CHANGE (Phase 1 first pass)

New files (additive, old scripts kept alongside until replacements are verified working):
- `Assets/GameAssets/Scripts/Core/` — `GameFlowManager.cs`, `SaveManager.cs`
- `Assets/GameAssets/Scripts/Progression/` — `MissionProgressData.cs`, save-data model, PlayerPrefs migration
- `Assets/GameAssets/Scripts/Missions/` — `MissionManager.cs`, `MissionDefinition.cs` (ScriptableObject)
- `Assets/GameAssets/Scripts/Parking/` — `ParkingValidator.cs`
- `Assets/GameAssets/ScriptableObjects/` — per-mission `MissionDefinition` assets generated from the existing 8 missions' scene data

Existing files expected to need small, careful edits later in Phase 1/3 (not rewrites):
- `GameManager.cs` — progressively delegate to the new managers rather than being replaced outright in one step
- `CarController.cs` — add a clean enable/disable API to replace the `maxAcceleration = 0` pattern (Phase 3), fix the `ClassicCarRed` tag bug (scene edit, not script)
- `ParkingTrigger.cs` / `MissionFailedHandler.cs` — eventually superseded by `ParkingValidator`/`ScoreManager`, kept working until the replacement is proven

No third-party plugin folders (`SimpleInput`, `JMO Assets`, car packs, `Prototype Collection`) will be modified.

---

## 6. PHASE-BY-PHASE EXECUTION PLAN

Following `prompt2.txt`'s mandated dependency order, executed **one phase at a time**, with a compile/regression check after each:

`Project Audit (this doc)` → `Core Architecture` → `Save Migration` → `Vehicle/Input Cleanup` → `Parking Validator` → `Score/Stars` → `Mission Data Architecture` → `Existing 8 Mission Migration` → `Dynamic Mission Selection` → `Additional Practice Missions (→30)` → `Economy` → `Garage` → `Color Customization` → `Optional Vehicle Upgrades` → `Camera/Vehicle Features` → `Settings` → `Open World Framework` → `Challenge Markers` → `Traffic` → `Pedestrians` → `Mirrors` → `UI Polish` → `Android Optimization` → `Full QA`.

Per the user's request, work proceeds **one part/phase at a time**, with a check-in after each before continuing to the next.

---

## STATUS

- [x] Phase 0 — Project Audit (this document)
- [x] Phase 1 — Core Architecture Refactor (`GameFlowManager` added, additive only; folder structure will grow incrementally per-phase rather than being pre-scaffolded empty; compile-verified via Unity 6000.0.29f1 batch mode, no `error CS`)
- [x] Phase 2 — Save System (`SaveData`/`MissionProgressData`/`LegacyPlayerPrefsMigration`/`SaveManager` + Editor self-check; additive only, gameplay still on PlayerPrefs until the mission-data phase; headless self-check 0 failures)
- [x] Vehicle / Input Cleanup — clean data API on `CarController` (`CurrentSpeedKmh`, throttle/steer/brake state), `SetVehicleEnabled()` replacing the `maxAcceleration = 0` freeze, input split out into `VehicleInput`. Fixed two real bugs: the brake button was dead, and freezing a car permanently left its acceleration at 5.
- [x] Parking Validator — `ParkingZone` + `ParkingValidator`: containment from the car's footprint corners, heading tolerance, speed threshold, hold time, real reverse-entry requirement, all configurable per mission.
- [x] Score / Stars — `ScoreRules` (ScriptableObject) + `MissionScoreTracker`: 100 starting score, tag-driven penalties, per-collider debounce, impulse-scaled severity, 90/70/50 star thresholds, capped overtime penalty.
- [x] Mission Data Architecture — `MissionDefinition`, `MissionCatalog`, `MissionAuthoring`, `MissionManager`, `MissionResult` (with modelled fail reasons).
- [x] Existing 8 Mission Migration — `MissionSceneSetupTool` converts them from the legacy `GameManager` arrays as a single undoable Editor action (developer-run; not applied blindly from outside Unity).
- [x] Dynamic Mission Selection — `MissionSelectionView` + `MissionCardView` build the list from the catalog; 30 missions is a data change, not a UI change.
- [x] Economy — `EconomyManager` with anti-farming (full pay on first clear or personal best, 25% on a flat replay, granted once at mission end).
- [x] Mission content — 30 `MissionDefinition` assets + catalog + default score rules generated; difficulty curve lives in `MissionContentGenerator.BuildSpecs()`.
- [x] Garage + Colour Customisation — `CarDefinition`/`CarCatalog`/`CarColorPalette`/`CarPaintTarget`/`GarageManager`/`GarageView`. Keeps all 3 existing cars; prices 0/3,000/6,000. Paint uses a MaterialPropertyBlock so shared material assets are never modified.
- [x] Optional Vehicle Upgrades — attempted and kept small: 3 levels, +15% max, applied as multipliers over the authored values. No WheelCollider configuration touched.
- [x] Camera / Vehicle Features — `VehicleLights` (headlights, brake lights, indicators), `VehicleHorn`, `SpeedometerView`, `VehicleCameraDirector` (third-person/cockpit/rear, with the existing follow camera left in charge of third-person).
- [x] Settings + Graphics Tiers — `SettingsManager`, `QualityProfile` (Low/Medium/High incl. mirror and traffic budgets for later phases), `AudioCategoryVolume`, `SettingsView` with Reset Progress.
- [x] Open World Framework + Challenge Markers + light navigation — `ChallengeDefinition`, `ChallengeMarker`, `OpenWorldChallengeManager` (throttled marker scan, no scene reload), `ChallengePromptView`, `ChallengeCompassView` (arrow + distance, no minimap).
- [x] Mirrors — `MirrorSurface`: quality-gated (none/rear/rear+sides), 256×128 or 512×256, manual render every *n*th frame, nothing rendered off-screen, RenderTextures released on every change.
- [x] Traffic AI — `WaypointPath`/`TrafficWaypoint`/`TrafficVehicle`/`TrafficManager`: kinematic cars, manager-driven ticking (no per-car `Update`), staggered sensors, fixed pool with no runtime instantiation, count from the graphics tier.
- [x] Traffic Lights — `TrafficLight` + two-phase `TrafficLightGroup`; traffic holds at red via waypoint data.
- [x] Pedestrians — `Pedestrian`/`PedestrianManager`: walk, pause, wait for vehicles, wider check at crossings; zero on Low.
- [x] HUD + parking feedback — `GameplayHudView`: score, coins, countdown, and neutral → amber → green parking indicator with a hold-time progress bar.
- [x] Mission Complete / Mission Failed — `MissionResultView`: one component, two panels, modelled fail reasons, and no attempt to load a mission 31.
- [x] Android pass — `AndroidReadinessCheck` (automated Phase 30 checklist), `AndroidProjectSettingsFixer` (one-click fix for the two real config bugs), `AndroidBuildScript` (batch development APK), `Docs/TEST_PLAN.md` (~90 tagged manual tests).
- [ ] Remaining: UI visual polish pass, and everything that needs authoring — the `GameManager` → `MissionManager` handover, mission layouts 9–30, the open-world scene, prefabs for traffic/pedestrians/mirror surfaces, and the full manual QA run.

All code-side phases from `prompt2.txt` are now implemented. What is left is Editor authoring plus the manual test pass in `Docs/TEST_PLAN.md`.

Biggest outstanding authoring work, all of it Editor-side: the `GameManager` → `MissionManager` handover, mission layouts 9–30, the mission-card prefab, per-car light/paint/camera wiring, and the open-world scene itself.

Still pending a human in the Editor: the scene handover from `GameManager` to `MissionManager`, the mission-card prefab, and mission layouts for 9–30. See `Docs/SETUP_CHECKLIST.md`.
