# SETUP_CHECKLIST.md

## Known pre-existing problem: stale compile cache referencing another game's code

A **release** APK build fails with a series of `error CS2001: Source file ... could not be found`, all pointing at `Assets/GameAssets/Scripts/Features/` — about 22 files named `Bomb.cs`, `EnemyCreep.cs`, `PlayerCombat.cs`, `PlayerDefense.cs`, `PlayerStats.cs`, `Projectile.cs`, `ProjectileBlocker.cs`, `ForbiddenZone.cs`, `GameHUD.cs`, `CombatUtils.cs`, `IDamageable.cs`, `FxDebris.cs`, `FxFactory.cs`, `IconFactory.cs`, `HoldButton.cs`, `AudioManager.cs`, `ProceduralAudio.cs` and similar.

That is a combat/shooter game's code, unrelated to car parking. **None of those files exist on disk**, nothing in the project references them, and they were already absent when the Phase 0 audit ran. The reference survives only inside Unity's generated compiler file list (`Library/Bee/artifacts/<hash>.dag/Assembly-CSharp.rsp`) and the asset database cache, i.e. this project contained another game's scripts at some point and `Library/` kept the record after they were deleted.

It did **not** stop the development APK from building successfully, so it may only surface for certain build configurations.

**How to clear it, cheapest first:**
1. Open the project in the Editor and let it finish importing, then build again — a proper asset refresh regenerates the file list.
2. If it persists: close Unity, delete the **`Library/Bee`** folder, reopen. This is build cache only; Unity regenerates it, and `Library/` is not in git.
3. If it still persists: close Unity, delete the whole **`Library`** folder. Safe but slow — a full reimport of the project.

Do not go looking for the missing files or recreate them; they are not part of this game.

## Latest state (read this first)

### Front end, modes and the four reported bugs

The whole front end is now one canvas, `GameUI`, built by `Tools → Car Parking → Build Game UI`. The legacy `MainMenuCanvas`, `MissionInfo` and `MobileControls` canvases are switched **off**: the overlapping screens in the bug report were two live canvases drawing over each other, not a layout mistake.

`GameSession` (on its own object in the scene) owns what the game is doing — menu, practice, challenge, free drive, paused — and switches the cameras, the driving controls, the showroom cars and the clock together. Nothing infers state from `Time.timeScale` any more.

Three game modes, reached from **PLAY** or **GAME MODE** on the home screen:

| Mode | What it does |
| --- | --- |
| Practice | One stage at a time, car placed on the start line, only that stage's props in the world. Clearing a stage unlocks the next. |
| Challenge | All eight distinct bays standing at once, played in order on a per-stage clock (`GameSession.challengeSecondsPerStage`, 120s). The car is **not** teleported between stages: driving to the next bay is the mode, and an on-screen arrow points at it with the distance. |
| Free drive | No missions, no timer, every mission's props hidden. |

Missions 9–30 are clones of 1–8 at the same world positions, so a challenge run is only the eight distinct bays (`MissionManager.BaseMissionCount`).

Four bugs from the report, and what was actually wrong:

- **"Can't steer once the yellow bar shows."** The track's barriers are tagged `Cone`, and the legacy `MissionFailedHandler` froze the car with `SetVehicleEnabled(false)` the moment one was touched — so clipping a barrier while lining up left the player stuck. `MissionFailedHandler` and `ParkingTrigger` now stand down wherever a `GameSession` exists, and a collision costs score instead. The hint text also now names the condition that is actually blocking the park ("Get the whole car inside the bay" / "Straighten up" / "Come to a stop") rather than the unhelpful "Line the car up inside the bay".
- **"The barriers can be driven through."** They had no colliders at all. The track pack ships one hand-made collision group, `oval_complete_colliders`, which covers the walls, garages and ground and nothing else. `Tools → Car Parking → Make Track Scenery Solid` adds 107 `MeshCollider`s across the barriers, tyre stacks, plastic blocks, lamp posts, pit wall, bridges, start lights and buildings. Trees and grandstands are deliberately left alone: they are out of reach and a collider each is a cost an Android build should not pay.
- **"No car in the garage."** The garage panel was an opaque full-screen rectangle in front of a camera pointed at nothing. The panel is now a column down the left, and `ShowroomCameraRig` frames the showroom car — which is standing in the world already — in the space beside it. Both menu framings look from the same side of the showroom; round the other side there is a lamp post within a couple of metres of the car. The paint swatches are also coloured from the palette now; they used to be eight identical white discs.
- **"The camera views are wrong."** Cockpit and look-back now share one eye point — the driver's head, measured per car from its own body bounds by `VehicleViewPoints` — and the rear view is that same seat yawed 180°. The old version used a fixed offset near the bonnet and a boom behind the boot.

Headlamps are placed from each car's **body bounds** rather than its wheel positions, so the pair sits symmetrically on the real nose and tail at bumper height; the spot lights are dipped 10°. Re-run with `Tools → Car Parking → Dress Cars With Lights And Horn`.

The HUD's car controls are five circular icon buttons on an arc over the brake pedal, with the indicators on the side they signal; the headlight and indicator icons light amber while active. Icons are generated as PNGs by `Tools → Car Parking → Generate UI Icons` (`Assets/GameAssets/Sprites/Icons`) — replace them with drawn art whenever it exists, the file names are the contract.

**Rebuilding:** run the tools in this order, each of which opens and saves the gameplay scene:
`Make Track Scenery Solid` → `Dress Cars With Lights And Horn` → `Build Game UI` → `Check Game UI`.
`Build Game UI` is safe to re-run: it lifts SimpleInput's steering wheel, pedals and brake out of the canvas before deleting it and puts them back afterwards.

**Still needs an Editor eyeball** (it cannot be checked from the command line):
- The driver's-seat camera on each of the three cars — the eye point is measured, but whether it clears the roof line and the dashboard is a judgement call.
- The headlamp spheres on each car. They are plain objects under `PlaceholderLights`; drag them if they sit proud of the bodywork.
- Whether 120s per challenge stage is the right difficulty (`GameSession` → Challenge mode → Challenge Seconds Per Stage).

### Missions

- **All 30 missions exist in the scene and validate with no issues.** 9–30 are clones of 1–8 at the same positions (only one mission's environment is ever active), with bays sized by difficulty.
- **Every bay was refitted** from measured geometry: the legacy triggers are thin end-line plates, and bays now extend from them along +forward. Select any `ParkingTrigger` and the cyan gizmo box should cover the painted bay — **this is the single most important thing to eyeball**. If a box sits on the wrong side of its end line, flip that one by hand.
- Textures and the three heaviest meshes are compressed for Android; release APK is **80.6 MB** at `Builds/CarParkingGame-release.apk`. Look at the track for visible quality loss; revert with `git checkout -- Assets` if it is unacceptable.
- "Parallel" missions (9–12, 23, 29) are straight-in bays with parallel rules — there was no parallel layout to clone.

## ALREADY DONE — do not redo these

The gameplay scene has been wired automatically (`Tools → Car Parking → Wire Gameplay Scene`, already run). In `complete_track_demo.unity`:

- All 8 legacy missions have a `MissionAuthoring` + `ParkingZone`, wired from the legacy `GameManager` arrays. Validation reports no structural issues.
- `MissionManager`, `SettingsManager` and `GarageManager` objects exist with their asset references assigned. Car containers resolved to `PlayerCars` and `MenuCars`.
- Mission definitions for 1–30 and all garage/quality assets are generated under `Assets/GameAssets/ScriptableObjects/`.
- Mission parking types for 2, 3 and 5 were corrected to match what the scene's triggers actually do.

A working UI has also been generated (`Tools → Car Parking → Build New Gameplay UI`, already run), under one canvas called **`NewGameplayUI`**: HUD (speed, score, coins, parking feedback), a mission-list grid built from the catalog, mission complete/failed screens, and a **"PRACTICE (NEW)"** button bottom-left that opens the list. Deleting that one canvas removes all of it.

The generated canvas also includes a **Garage** panel (previous/next, buy, select, 8 colour swatches, stat bars, coin balance) and a **Settings** panel (music/SFX/steering sliders, Low/Medium/High, 30/60 FPS, and Reset Progress behind a confirmation). Both open from buttons on the left while the menu is up.

Each car now also has `VehicleLights`, `VehicleHorn` and `CarPaintTarget` wired automatically.

**About car colour:** the paint target was filled with every renderer on the car except wheels and lamps (9–12 per car). The car bodies are single meshes, so **if a body mesh also covers the windows, changing colour will tint those too**. That is a limit of the art, not the code: open `CarPaintTarget` on the car and remove the offending renderer from the list. Downloading a different car model would not fix it either — a new car would not match the style of the existing three.

**Two rough edges to expect, because the layout was generated without being able to see it:**
- The HUD and the "PRACTICE (NEW)" button are **always visible**, including over the main menu — nothing hides them by game state yet. Tie them to `GameFlowManager` state, or just hide the HUD child while the menu is open.
- Positions, sizes and font sizes are all guesses. Expect to move things, especially on a phone aspect ratio.

**State of play:** `SettingsManager` is live (applies quality + frame rate, creates the save file). `MissionManager` is present but inert. `GarageManager` is wired but **disabled on purpose**. The game still plays through the legacy `GameManager`/`CarSelection` path, so behaviour is unchanged apart from the two deliberate `CarController` fixes.

**Undo everything scene-side:** `git checkout -- "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity"`

The sections below are what is still left, roughly in the order worth doing them.

Manual steps a developer must perform in the Unity Editor. Claude Code cannot safely verify visual/scene state purely from YAML or CLI, so anything here needs an actual Editor check. Updated as each phase progresses.

---

## Outstanding from Phase 0 audit

### 1. Fix `ClassicCarRed`'s mistagged GameObject (pre-existing bug, unrelated to this upgrade)
- **Where:** `complete_track_demo.unity` → `PlayerCars` → `ClassicCarRed`
- **Problem:** its Tag is currently `MainCamera` instead of `Player`.
- **Impact:** `ParkingTrigger.OnTriggerEnter` requires `CompareTag("Player")` for non-reverse missions. While `ClassicCarRed` is selected, forward-parking missions likely never register completion.
- **Fix:** Select `ClassicCarRed` in the Hierarchy → Inspector → Tag dropdown → change to `Player`. Save the scene.
- **Verify:** Select `ClassicCarRed` as the active car, drive into a forward-parking mission's zone, confirm "Mission Passed" fires.

### 2. Confirm mission-area naming for mission index 0
- **Where:** `complete_track_demo.unity`, mission-area containers.
- **Observation:** containers `Mission2` … `Mission8` were found by name; no container literally named `Mission1` was found via YAML text search. It may exist under a different name or be the always-active base track.
- **Action:** Open the Hierarchy, locate the container wired as `GameManager.missionAreas[0]`, confirm what it's actually named. No fix needed unless you want consistent naming — just note for later Editor tooling (Phase 27) which will need exact object names.

### 3. Confirm `missionPassedUI` wiring on all 8 `ParkingTrigger`s
- **Where:** each of the 8 `ParkingTrigger` components in the scene.
- **Observation:** only 3 `MissionPassed`/`MissionPassedText` UI subtrees were found by name-grep, but there are 8 `ParkingTrigger` instances.
- **Action:** Click through all 8 `ParkingTrigger` components in the Inspector and confirm each has a valid `missionPassedUI` + `nextMissionButton` reference (not empty). Note which ones share a UI object vs. have their own. This matters once Phase 7 (Parking Validator) and Phase 10 (Mission Selection UI) replace this logic — we need to know the current wiring is intact before replacing it.

### 4. Known pre-existing config issues (not blocking, fix opportunistically)
- `ProjectSettings/QualitySettings.asset`: `m_PerPlatformDefaultQuality.Android` is `2`, but only quality levels `0` ("Mobile") and `1` ("PC") exist. Recommend changing Android's default to `0` in **Edit → Project Settings → Quality**.
- `ProjectSettings/GraphicsSettings.asset`: global default Render Pipeline Asset is `PC_RPAsset`, not `Mobile_RPAsset`. Per-quality-level overrides exist and take priority per-platform, but the global fallback should probably also point at Mobile. Check **Edit → Project Settings → Graphics**.
- Android `applicationIdentifier` is still the Unity template default (`com.UnityTechnologies.Mobile3DTemplate`). Must be changed to a real package name before any Play Store upload (not urgent for gameplay work).

---

## Phase 1 — Core Architecture

No manual Editor steps required. `GameFlowManager.cs` is self-initializing and not yet wired to anything — nothing to verify in the Editor beyond confirming the project still opens and compiles (see compile validation log, done via Unity batch mode this phase).

## Phase 2 — Save System

No manual Editor steps required yet, and **no gameplay behaviour changed**: `GameManager`/`CarSelection` still read and write their original PlayerPrefs keys. The new JSON save system exists alongside them and is not yet called by any gameplay script (the handover happens in the mission-data phase, as one switch instead of two systems writing progression at once).

Useful things to know when you do start testing it:

- **Save file location:** `Application.persistentDataPath/carparking_save.json`. On Android that is `/Android/data/<package name>/files/`. In the Editor on Windows it is `C:\Users\<you>\AppData\LocalLow\DefaultCompany\CarParkingGame\`.
- **Migration runs once**, the first time `SaveManager.Load()` finds no save file. It reads the legacy keys (`CurrentMission`, `Mission{i}Completed`, `SelectedCarIndex`) and **never deletes them**, so the old build still works and nothing is lost if the conversion ever needs revisiting.
- **Corrupt files are not deleted.** An unreadable save is renamed to `carparking_save.json.corrupt_<timestamp>` and the game starts from defaults. If you're debugging a save issue, look for those files first.
- **Run the self-check** any time you touch the save model: **Tools → Car Parking → Run Save System Self-Check**. It only exercises pure functions — it does not touch your real PlayerPrefs or your real save file.
- **Reset Progress** exists as `SaveManager.ResetProgress()` but has no UI button yet; that gets wired up with the Settings screen (Phase 22).

## Phases 3–11 — Vehicle, Parking Validation, Scoring, Missions, Selection UI, Economy

Three existing scripts changed behaviour this round (`CarController`, `ParkingTrigger`, `MissionFailedHandler`). Everything else is new and **not yet wired into the scene**. The legacy `GameManager` still runs mission flow, so the game should behave as before apart from the two deliberate fixes below.

### Behaviour that deliberately changed — please feel-test these
1. **The on-screen brake button now actually works.** It previously did nothing: the condition around `Brake()` was true in every control mode, so braking only ever happened when the throttle was released. That auto-brake-on-release behaviour is preserved (it's what makes the car settle), but the brake button now also bites while the throttle is held.
2. **Freezing the car no longer permanently retunes it.** The old code set `maxAcceleration = 0` to freeze and `= 5` to release — so after the first mission pass or retry, every car was left at 5 regardless of what the prefab said (the default is 20). `SetVehicleEnabled(bool)` now restores each car's authored value, and also applies the brakes so a frozen car actually stops instead of coasting. **Cars will feel faster after a retry than they used to.** That is the intended value; if you preferred the slower feel, lower `maxAcceleration` on the cars themselves rather than reintroducing the old hack.

### One-click setup, in this order
1. **Generate the mission data** — `Tools → Car Parking → Generate Mission Content`. Writes `Assets/GameAssets/ScriptableObjects/`: `DefaultScoreRules.asset`, `MissionCatalog.asset` and `Missions/Mission01…Mission30.asset`. Safe to re-run; it updates in place rather than duplicating. The difficulty curve lives in `MissionContentGenerator.BuildSpecs()` — edit it there, not in 30 separate assets.
2. **Open `complete_track_demo.unity`**, then run `Tools → Car Parking → Set Up Mission Authoring From Legacy GameManager`. It reads the legacy `GameManager` arrays and, for each of the 8 existing missions, adds a `MissionAuthoring` to the mission-area object and a `ParkingZone` to the parking trigger (copying the trigger's `BoxCollider` size so the bay matches what you already authored). It is a single undoable action — **Ctrl+Z reverts it** — and it does not save the scene for you.
3. **Run `Tools → Car Parking → Validate Missions`.** It reports missing start points/zones/definitions, duplicate mission ids, invalid rewards or angle tolerances, and lists which catalogued missions have no layout yet (missions 9–30 will all be listed — that's expected, they're data-only for now).
4. **Check the reverse-mission warnings.** The setup tool compares each trigger's `isReverseMission` against the generated definition's parking type and warns on mismatches without changing anything. The scene is the existing behaviour, so if a mismatch appears, fix the definition asset.

### Still to wire by hand (needs eyes on the Editor)
- **`MissionManager`**: create an empty GameObject, add `MissionManager`, assign `DefaultScoreRules`. **Do not enable it at the same time as the legacy `GameManager` mission flow** — both would spawn/freeze the car and write progress. Treat the switch-over as its own deliberate step and test mission 1 end to end immediately after.
- **Mission card prefab** for `MissionSelectionView`: a `Button` with `MissionCardView` on it, wiring up the number label, name label, best-score label, difficulty label, a locked overlay object, and up to 3 star `Image`s. Then add `MissionSelectionView` to the mission-selection panel and assign the catalog, the card prefab and a container with a Grid/Vertical Layout Group. This replaces the fixed 8 `missionButtons` on `MainMenuManager`.
- **Parking feedback UI** (neutral/partial/valid colours): `ParkingValidator` raises `StateChanged` and `ProgressChanged` — hook a HUD indicator to those. No visual exists yet.
- **The legacy `ParkingTrigger` components stay live** until the switch-over. Once `MissionManager` drives missions, disable or remove them — otherwise touching the bay still instantly completes the mission through the old path, bypassing the angle/speed/hold checks entirely.
- **Watch for the "bay cannot fit vehicle" warning.** The old parking triggers may be small boxes sitting at the centre of a bay rather than boxes the size of the bay. `ParkingValidator` logs a warning at mission start when the bay is smaller than the car and required containment is 1 (which would make the mission impossible). If you see it, enlarge the `ParkingZone` to the real bay size — that is the most likely piece of hand-tuning needed after running the setup tool.
- **`MissionFailedHandler` is still the old instant-fail-on-cone behaviour.** The replacement (score penalties instead of instant failure) is in `MissionScoreTracker`, active only once `MissionManager` drives the mission. Remove the old handler from the cars as part of that switch-over, not before.

### Things that need no setup
- Collision penalties and parking validation attach themselves at runtime (`VehicleCollisionReporter` is added to the active car, `ParkingValidator` to the bay), so no car prefab or bay needs a component added by hand.

## Android configuration — run this first, it is one click

`Tools → Car Parking → Check Android Readiness` reports the project's Android configuration. It currently finds five things; two are plain bugs and are fixed by `Tools → Car Parking → Fix Android Project Settings`:

1. **Android's default quality level is index 2, but only 2 levels exist** (`Mobile`, `PC`). Unity clamps it, so Android is not reliably starting on `Mobile`. → fixed by the tool.
2. **The global default render pipeline asset is `PC_RPAsset`**, not the mobile one. Per-quality overrides still apply per platform, but the global fallback should be mobile. → fixed by the tool. **Look at the game in the Editor afterwards** to confirm rendering is unchanged.

The other three are release decisions the tool deliberately leaves to you:
3. Bundle id is still `com.UnityTechnologies.Mobile3DTemplate` — must change before any store upload.
4. Company name is still `DefaultCompany`.
5. Android target SDK is `Automatic` — worth pinning so store compliance does not shift under you.

Confirmed already correct: IL2CPP, ARM64, Vulkan + GLES3, landscape-only, Android is the active build target, and only the real gameplay scene is enabled in Build Settings.

## Mirrors, Traffic, Pedestrians, HUD, Result screens

- **`MirrorSurface`** goes on the mirror's **own surface renderer** (so Unity's visibility callbacks apply to it). Assign the mirror camera — aim it backwards by hand; this is a rear-view camera feeding a quad, not a planar reflection. Texture property is `_BaseMap` for URP Lit. Mirrors self-disable on Low, rear-only on Medium, all on High. After wiring, run test 7.5 in the test plan (toggle quality 20 times, watch render-texture memory) — that is the one that catches a texture leak.
- **Traffic**: build `WaypointPath` objects along the roads (2+ waypoints each, `loop` on for circuits, `continuations` to branch at junctions). Add `TrafficWaypoint` only where you need a speed limit, a traffic light, or a crossing. Then one `TrafficManager` with the traffic prefabs, the paths and a sensible ceiling.
  - **Traffic car prefabs need a `Rigidbody` and a collider, and a tag that matches a `ScoreRules` entry** (`Vehicle` by default) — otherwise hitting one charges the generic default penalty. Tags are not assigned at runtime because assigning a tag that does not exist in the project throws.
  - The `obstacleMask` on each prefab should include the player car and other traffic, and exclude the road itself.
- **Traffic lights**: `TrafficLight` per head with its green/amber/red glow objects, then one `TrafficLightGroup` per junction with the lights split into two phases.
- **Pedestrians**: their own `WaypointPath`s on the pavements, crossing waypoints flagged `isCrossing`, then one `PedestrianManager`. Set `vehicleMask` to the vehicle layers only. None will be active on Low.
- **`GameplayHudView`** on the HUD canvas: mission name, score, coins, timer, and the parking feedback indicator (an `Image` for the colour and a second `Image` set to Filled for the progress bar).
- **`MissionResultView`** on the result canvas: assign both panels, the labels, the star images, the buttons, the `MissionCatalog`, and the existing `MainMenuManager` for the Menu button. One component drives both the passed and failed screens.

## Garage, Vehicle Features, Cameras, Settings, Open World

Assets are already generated (`Tools → Car Parking → Generate Garage Content`, safe to re-run): `CarCatalog`, `Cars/classic_red|hot_rod|muscle`, `CarColorPalette`, `UpgradeRules`, `QualityProfile`.

### Verify first — car index mapping
Ownership and prices are keyed by each car's **child index in the player-car container** (0 = free, 1 = 3,000 coins, 2 = 6,000 coins). The generated catalog assumes the order `Classic, Hot Rod, Muscle`.
1. Set up `GarageManager` (below) so it knows the container.
2. Run `Tools → Car Parking → Validate Garage`. It logs which scene object each index resolves to.
3. If the names don't match, fix `carIndex` on the three assets in `ScriptableObjects/Cars/` — **do not** reorder the container's children, since the existing `CarSelection` depends on that order too.

### Wiring
- **`GarageManager`**: empty GameObject + component. Assign `CarCatalog`, `CarColorPalette`, `UpgradeRules`, the player-car container, and the showroom container. Leave "keep legacy car selection in sync" **on** until the old `CarSelection` components are removed — it writes the legacy `SelectedCarIndex` key so both systems agree on the selected car.
- **`CarPaintTarget`** on each of the 3 cars: assign only the **bodywork** renderers (not glass, lights or tyres). The colour property is `_BaseColor` for URP Lit — change it to `_Color` if a car uses a built-in-pipeline shader. Without this component, colour selection does nothing and logs a warning.
- **`GarageView`** on the garage panel: assign the labels/buttons you want; every field is optional, so start with name + price + coins + buy/select and add the rest later.
- **`SettingsManager`**: empty GameObject + component, assign `QualityProfile`. Put it in the scene so settings apply at startup. Add `AudioCategoryVolume` to any music/SFX AudioSource that should follow the sliders.
- **`SettingsView`** on the settings panel.
- **`VehicleLights`** per car: assign the glow objects and/or `Light`s for headlights, brake lights and each indicator. Anything left empty is simply inert. Keep real-time `Light`s to the headlights only on mobile.
- **`VehicleHorn`** per car: assign a horn clip.
- **`VehicleCameraDirector`**: assign the camera transform and the existing `CarCameraController`. **Check the cockpit and rear offsets from inside each car** — the defaults are guesses and will need nudging per vehicle.
- **`SpeedometerView`** on the HUD.
- **HUD buttons need no glue code**: point a `Button.onClick` straight at `VehicleLights.ToggleHeadlights`, `ToggleLeftIndicator`, `ToggleRightIndicator`, `VehicleHorn.Play`, `VehicleCameraDirector.CycleMode`.

### Open world
No open-world scene exists yet; the framework is scene-agnostic. To build it:
1. Create the scene from the modular track/prop assets already in the project, add it to Build Settings.
2. Per challenge location: a `ParkingZone` on the bay, a `ChallengeMarker` nearby (assign the zone + an activation radius), and a `ChallengeDefinition` asset (`Create → Car Parking → Open World Challenge`) with a unique `challengeId` — **the id is the save key, so never change it after release**.
3. One `OpenWorldChallengeManager` in the scene, plus `ChallengePromptView` and optionally `ChallengeCompassView` on the HUD.

### Known gap worth deciding on
The third-person camera **cannot be rotated by touch on Android**: `CarCameraController` reads `Input.GetAxis("Mouse X/Y")`, which is desktop-only. It was left alone on purpose rather than adding touch handling that can't be tested here. If camera orbit matters on device, that's a small, isolated change to make and feel-test together.

### Worth verifying when migration goes live (later phase)
Because migration is derived from the legacy `CurrentMission` value, test these three shapes of existing save on a device before shipping:
1. A player mid-progress (e.g. finished missions 1–2, sitting on 3) — should show 2 completed, mission 3 unlocked and unplayed.
2. A player who finished all 8 old missions — should show 8 completed and mission 9 unlocked.
3. A player who never completed anything — should show only mission 1 unlocked, nothing completed.

---

_This file will grow as later phases (Save Migration, Mission Data Architecture, Garage, Open World, etc.) introduce steps that require hands-on Editor verification._
