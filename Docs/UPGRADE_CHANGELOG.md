# UPGRADE_CHANGELOG.md

Running log of changes made during the upgrade described in `prompt2.txt`. Newest entries at the top. Each entry records what was added/changed, why, and what (if anything) still needs manual verification in the Unity Editor.

---

## First real playtest in the Editor — the mission loop works

The user ran the game in the Editor (Device Simulator, Galaxy Note20 Ultra profile). **Missions 1 (forward) and 2 (reverse) completed end to end**: validator → MISSION COMPLETE (score 100, 3 stars, 0 collisions, NEW BEST) → coins 0 → 300 → 600 → NEXT loads the following mission. This is the first time the new pipeline has been seen working by a person rather than by headless checks.

Bugs found by looking at screenshots, all fixed:

- **Starting a mission from the new list left the game paused behind the menu.** The legacy menu pauses with `Time.timeScale = 0`; `MissionSelectionView` now hands back to `MainMenuManager.ResumeGame()` and hides the legacy mission caption, which named the wrong mission.
- **The HUD missed the mission it was showing** (name "-", a stray "0:00"): it is hidden while the menu is up, so `MissionStarted` fired before it subscribed. It now syncs from `MissionManager` state when enabled.
- **The parking progress bar was permanently full**: a `Filled` Image with no sprite renders as a solid quad. The builder now assigns the built-in UI sprite.
- **The lowest HUD button was clipped** at the screen edge; the column was moved up.
- **Result screen showed no stars** although the brief requires them. Three rating images were added (built-in round sprite; the only star textures in the project are single-channel particle masks in the FX pack, and re-importing those would risk that pack's particles).
- **Bays were offset from the glowing target the player sees.** Each mission carries a `Rays`/`Runes`/`Point Light` target; on mission 8 it sat 3.6 m out from the end line while the bay centre was at 2.15 m, so a car parked dead centre on the target would have failed. `BayFitTool` now centres each bay on its own mission's target. Verified visually: the bay sits between the striped barriers with the target inside it.
- The cursor stayed locked on the result screen in the Editor; it is now released.

Laptop testing aids, Editor-only (`#if UNITY_EDITOR`, absent from the APK) — the assignment is submitted as an Android build but developed on a laptop where driving the touch joystick with a mouse is impractical:

- Keyboard driving (arrows/WASD, Space to brake) alongside the touch controls, read from **both** the legacy Input Manager and the new Input System, because not every Editor view feeds key presses to the legacy one.
- **P** (or `MissionManager` ⋮ → *Debug: Snap Car Into Bay*) drops the car dead centre in the active bay, at rest, facing the parked heading, and satisfies the reverse-entry rule for reverse missions. It proves the mission → result → reward loop, **not** bay placement.

The recurring `WheelCollider requires an attached Rigidbody` warning is pre-existing and harmless: the original scene already had 24 WheelColliders and 3 Rigidbodies; the extra 12 wheels belong to the showroom cars, which never drive.

---

## Missions 9–30 laid out, every bay refitted, and a scale bug found by measuring

### Missions 9–30

`Editor/MissionLayoutGenerator.cs` clones the eight working missions in place. Mission environments are activated one at a time, so a clone can sit at exactly its source's position and never coexist with it — no free ground on the track has to be found. Each clone takes a source with the same parking type where one exists, has its legacy `ParkingTrigger` removed (generated missions only run through `MissionManager`), and gets a bay sized for its difficulty. **All 30 missions now validate with no issues.**

Honest limit: there is no parallel-parking layout in the original eight, so the "parallel" missions (9–12, 23, 29) are straight-in bays with parallel scoring rules. Real parallel layouts need a person to place them.

### The original eight bays were uncompletable — measured, then fixed

`Editor/MissionGeometryProbe.cs` read the actual layout of the eight legacy missions instead of assuming it:

- the legacy triggers are **thin plates** — seven scaled to (1, 2.3, 0.19), i.e. 1 m wide and 19 cm thick — the **end line** of a bay, not the bay;
- the plates were rotated by hand: where the surrounding cones form a clear line, their axis matches the plate's forward axis within 0–8°;
- in **8 of 8** missions the cones sit on the plate's +forward side, 2–4 m out.

That exposed a real bug in `ParkingZone`: it measured through the transform's scale, so on a plate scaled 0.19 a "5.2 m" bay was under 1 m in the world. No car could ever have completed any mission through the new validator. The unit tests had missed it because they used an unscaled transform. Fixed: centre and size are now metres in the object's rotation frame, independent of scale, and `GameplaySelfCheck` has a regression test on a (1, 2.3, 0.19) plate.

`Editor/BayFitTool.cs` then places every bay from the measurements: it extends from the end line along +forward, starts 0.6 m behind it (the legacy rule fired as soon as the bumper touched the line), runs 2.8 × 6.0 m for difficulty 1 down to 2.3 × 5.0 m for difficulty 5 (the widest car is 1.94 m, so the hardest bay still leaves ~18 cm a side), and sets the parked heading from the legacy completion rule — nose to the end line for forward missions, tail for reverse. `ParkingZone` gained an explicit parked-heading flag for that.

### APK size, continued

Mesh compression on the three models over 4 MB (54 MB of source) brought meshes from 150.3 MB to 65.2 MB.

| Build | APK |
|---|---|
| Development, original | 264 MB |
| Release | 218 MB |
| + texture compression | 107 MB |
| + mesh compression | **80.6 MB** |

Both compression passes change how the track looks and are revertible with `git checkout -- Assets`.

---

## Stale cache from another project, and where the APK size actually comes from

### The CS2001 mystery, solved

A release build failed with `error CS2001` for ~22 files under `Assets/GameAssets/Scripts/Features/` — `Bomb.cs`, `EnemyCreep.cs`, `PlayerCombat.cs`, `Projectile.cs`, `PlayerStats.cs`, `GameHUD.cs` and similar. Combat/shooter code, nothing to do with car parking, and absent from disk.

They were listed in Unity's generated compiler file list (`Library/Bee/artifacts/<hash>.dag/Assembly-CSharp.rsp`) and recorded in `Library/SourceAssetDB`. The user confirmed it was a 2D game of theirs that had leaked into this project earlier; the files were deleted but `Library/` kept the record. **Pre-existing, not caused by this upgrade.**

Fixed by deleting the caches that held the record — `Library/Bee` (13 GB), `SourceAssetDB`, `ArtifactDB`, their lock files and `ScriptAssemblies` — while **deliberately keeping `Library/PackageCache`**, since deleting that forces a package re-download and would break the project on a machine without internet. Verified afterwards: `CS2001` 0, `error CS` 0, self-check 0 failures, no remaining reference to `Scripts/Features`. Also freed ~13 GB.

### Where the 264 MB actually was

With a release build finally working, the size breakdown is unambiguous:

| | size | share |
|---|---|---|
| Textures | 592.5 MB | **79.5%** |
| Meshes | 150.3 MB | 20.2% |
| Shaders | 2.0 MB | 0.3% |
| Sounds | 84 KB | ~0% |

Worst offenders: `road.psd` at **256 MB in the build (549 MB on disk)**, `garages_specular.psd` 85 MB, `garages.psd` 64 MB, `road_normal.png` 38 MB, `oval_mod_trees.FBX` 92.5 MB, and every Prototype Collection cone/barrier texture at 9.5 MB each.

So none of this upgrade's code is the problem — scripts and shaders together are about 0.3% of the build. The project was shipping uncompressed Photoshop files as mobile textures.

`Editor/TextureOptimizationTool.cs` applies Android import overrides (ASTC 6×6, max 1024 px, or 2048 px for road and garage surfaces which the player looks at closely) to every texture whose source file exceeds 2 MB — 137 textures, 2345 MB of source art. Pixel-art UI under `SimplePixelUI` and `Sprites` is skipped, since compressing pixel art ruins it and those files are small anyway.

Import settings live in `.meta` files, which git tracks, so this is revertible with `git checkout -- Assets`. **It changes how the game looks** — the track needs a look in the Editor before shipping.

**Measured result:**

| | before | after |
|---|---|---|
| Textures in build | 592.5 MB (79.5%) | **61.9 MB (28.8%)** |
| Release APK | 218 MB | **107 MB** |
| Development APK | 264 MB | — |

A 59% cut against the original development APK, for an import-settings change and no code.

**Meshes are now the biggest cost** at 150.3 MB (69.9%), dominated by `oval_mod_trees.FBX` (92.5 MB). Two obvious next moves, both needing a visual check first: enable mesh compression on the heavy model importers, and check whether the gameplay scene even uses those track trees — the track pack ships a full racing circuit and this game uses part of it.

---

## Placeholder lights and horn — closing the "no assets" gap without downloading anything

The car models contain **no lamp objects at all** (a grep of the scene turns up only wheels), and the project has **no horn sound**. Rather than download third-party assets — which `prompt2.txt` forbids, and which would put licensing risk into a project that may reach a store — both were created from scratch, which is what the same document asks for ("create a clean placeholder").

**Horn** (`Tools → Car Parking → Generate Placeholder Horn Clip`): the PCM is synthesised in C# and written as a 22 kHz mono 16-bit WAV — two notes a major third apart (440 Hz + 554.37 Hz) plus a second harmonic for bite, with a short attack and release. A real car horn is two close notes sounded together, which is why a single sine sounds like a test tone and this does not. 22 KB, no licensing question.

**Lamps** (`Tools → Car Parking → Dress Cars With Lights And Horn`): positions are **derived from each car's own WheelColliders**, not guessed:
- front and rear axle Z → which end is the front, and the wheelbase
- largest |x| → half the track width
- wheelbase × 0.25 → how far bodywork overhangs each axle (a realistic car proportion)

Each car therefore gets lamps matched to its own dimensions. The measurements it reported are sane, which is itself a check on the method: track 1.47–1.59 m and wheelbase 2.72–2.98 m across the three cars.

Per car: 2 headlights, 2 brake lights, 4 indicators (one per corner), plus 2 spot lights on the headlights only. The spot lights are off unless the player switches the headlights on, and they are the one genuinely expensive part — drop them first if a low-end device struggles.

**One trap avoided:** `GameObject.CreatePrimitive` attaches a collider. Left in place, every car would have gained 8 invisible bumpers that collide with the world and trigger scoring penalties. All lamp colliders are destroyed.

**Still placeholders:** glowing spheres, not modelled lamps. Swap the meshes when art exists; the wiring stays valid.

**Reachable from the HUD:** `VehicleControlButtons` adds LIGHT / HORN / < L / R > / CAM buttons down the right side (clear of SimpleInput's joystick and brake on the left). It resolves the car being driven **on each press** instead of holding an Inspector reference, because the scene swaps between three cars and a bound reference would end up pointing at one that is switched off.

**And the HUD no longer sits on top of the menu:** `VisibleWhilePlaying` shows the HUD only while `Time.timeScale > 0` and the "PRACTICE (NEW)" button only while paused. `Time.timeScale` is used as the signal because that is already how `MainMenuManager` pauses — no second source of truth to keep in step.

Verified in the saved scene: 3 × `VehicleLights`, 3 × `VehicleHorn`, 3 lamp roots, 8 lamps per car, 6 spot lights, 1 × `VehicleControlButtons`, and the 22 KB horn WAV on disk.

---

## Playable UI + headless validator tests

### The parking validator is now actually tested

`ParkingValidator.Update` was split so the state machine can be driven with an explicit time step (`public void Tick(float)`). `GameplaySelfCheck` then runs five scenarios that step 2 seconds of game time past the 1.5s hold requirement:

| Scenario | Expected | Result |
|---|---|---|
| Car centred, straight, stopped | validates | pass |
| Car 12m away from the bay | never validates | pass |
| Car in the bay but 40° crooked | never validates | pass |
| Car in the bay still rolling at 3 m/s | never validates | pass |
| Reverse mission, car placed in the bay without reversing in | never validates | pass |

This is as close to "park the car and see" as is possible without a human at the controls, and it covers the core of the new gameplay.

**The tests caught a bug on the first run — in the test, not the game.** The fake car used a kinematic Rigidbody, and a kinematic body does not retain an assigned `linearVelocity`, so the "still rolling" case read as stopped and wrongly validated. Fixed by using a non-kinematic body.

### A real conflict fixed

Starting a mission through `MissionManager` while the legacy `ParkingTrigger` was live meant the old "touch the trigger, instantly complete" path fired first, bypassing angle/speed/hold validation entirely — and the legacy `MissionFailedHandler` would instantly fail the run on any cone contact. `MissionManager` now disables both for the duration of a mission and restores them afterwards, so the new pipeline is clean and the old menu flow still behaves exactly as before.

### Generated UI

`Editor/GameplayUiBuilder.cs` builds a working UI under a single canvas named `NewGameplayUI` (delete that one object to remove all of it):

- **HUD**: speed in km/h, mission name, score, coins, countdown, and the parking feedback indicator with its hold-time progress bar.
- **Mission list**: a 4-column grid built from the catalog, using a generated `Assets/GameAssets/Prefabs/UI/MissionCard.prefab` (number, name, difficulty, best score, 3 stars, locked overlay).
- **Result screens**: mission complete (score, time, collisions, time penalty, coins, new-best badge, Next/Replay/Menu) and mission failed (reason, score, Retry/Menu).
- **A "PRACTICE (NEW)" button** bottom-left that opens the mission list, so the new pipeline is reachable without rewiring the legacy menu.
- `CanvasScaler` set to scale with screen size against 1920×1080, matching height — the right choice for landscape phones with varying aspect ratios.

All ~25 serialized references were assigned through `SerializedObject`, and the build log contains no `No field` warnings, which means every field name resolved. Scene went from 656 to 711 GameObjects.

**This layout is functional, not designed.** Every position, size and font size is a number chosen without being able to see the result. Expect to move things.

### Audit correction worth recording

While checking for a duplicate `EventSystem`, the scene's existing one turned out to use **`InputSystemUIInputModule`**, not `StandaloneInputModule`. So the Phase 0 audit's claim that the new Input System package is "present but inert" was **wrong**: every uGUI button in the game receives input through it. Removing that package would break all UI input. The audit missed this because it grepped C# files, and the dependency lives in scene data. `Docs/UPGRADE_PLAN.md` has been corrected.

### Still not verified

No one has pressed Play. The UI's appearance, the driving feel, and whether the generated layout is usable on a phone screen are all unknown. The five validator scenarios and the successful APK build are what is actually verified.

---

## Scene wiring — the new systems are now actually in the gameplay scene

Up to this point every new system was dormant code. `complete_track_demo.unity` has now been modified.

**What was done** (`Editor/SceneWiringTool.cs`, run headlessly):

- **The 8 legacy missions were converted**: each mission area got a `MissionAuthoring` and each parking trigger got a `ParkingZone` sized from that trigger's existing `BoxCollider`. All references (definition, start point, zone, environment container) were read out of the legacy `GameManager` arrays, not guessed.
- **Three manager objects created**: `MissionManager` (with `DefaultScoreRules`), `SettingsManager` (with `QualityProfile`), `GarageManager` (with catalog, palette, upgrade rules and both car containers).
- **Car containers resolved from real scene data, not from object names**: the player container is whatever the three `CarController`s share as a parent (`PlayerCars`, 3 children), and the showroom is the other `CarSelection.allCarsContainer` (`MenuCars`). Guessing at names would have been the fragile way to do this.

**A real data bug this surfaced.** The setup tool compares each trigger's `isReverseMission` against the generated definition, and reported three mismatches: missions **2, 3 and 5** are actually Reverse / Forward / Reverse in the scene, while the generated data (which followed the design brief's suggested ladder) had them as Forward / Reverse / Forward. The scene is the existing behaviour, so `MissionContentGenerator.BuildSpecs()` was corrected to match and the definitions regenerated — missions 2, 3 and 5 are now `First Reverse`, `Straight And Narrow` and `Wide Reverse`. This is exactly the kind of thing that would have shipped as "mission 3 refuses to complete" if the parking types had been left as guesses.

**Verification that nothing was destroyed** (the scene diff is large because Unity re-serialises the whole file):

| | before | after | delta |
|---|---|---|---|
| GameObject | 656 | 659 | +3 (the three managers) |
| Transform | 620 | 623 | +3 |
| MonoBehaviour | 128 | 147 | +19 (8 authoring + 8 zones + 3 managers) |

`CarController` ×3, `ParkingTrigger` ×8 and `GameManager` ×1 are all still present and untouched. Mission validation then reported **no structural issues on the 8 real missions** — the 22 warnings it raises are missions 9–30 having no layout yet, which is expected.

**What actually changes when you press Play now:**

- `SettingsManager` applies the saved graphics tier and frame-rate cap at startup, and creates `carparking_save.json` on first run (migrating the legacy PlayerPrefs, which are left intact).
- `MissionManager` is present but **inert** — nothing calls `StartMission` until a mission-selection UI exists, so the legacy `GameManager` still runs missions exactly as before.
- `GarageManager` is wired but **deliberately disabled**: enabling it would have it activating and painting cars alongside the legacy `CarSelection`, and nothing drives it until `GarageView` is built. The tedious part — the references — is done.

**To undo all of it:** `git checkout -- "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity"`.

**Unintended change worth knowing about:** `ProjectSettings/QualitySettings.asset` and `GraphicsSettings.asset` were modified as a side effect of running Unity in batch mode (most likely the Android build), not by a deliberate edit. The changes are `Android: 2 → 0` and the default render pipeline `PC_RPAsset → Mobile_RPAsset` — which happen to be exactly the two fixes the audit called for, but they were not made on purpose. Revert with `git checkout -- ProjectSettings/` if preferred.

---

## Mirrors, Traffic, Traffic Lights, Pedestrians, HUD, Mission Complete/Failed, Android Pass (done)

### Mirrors (quality-gated)

`Scripts/Vehicle/MirrorSurface.cs` — a small camera rendering into a low-resolution RenderTexture shown on the mirror surface. **Not** a true planar reflection: the camera is aimed backwards by hand, which costs a fraction of the maths and reads the same on a 256×128 mirror. Three things keep it affordable:
- the player's graphics tier decides whether the mirror is allowed at all (none on Low, rear only on Medium, sides too on High, 512×256 on High),
- the camera is `enabled = false` and driven manually every *n*th frame rather than rendering every frame,
- nothing renders while the mirror is off-screen (`OnBecameVisible`/`OnBecameInvisible`).

RenderTextures are released and destroyed on disable and on every resolution change — a RenderTexture is a native allocation, and dropping the reference without releasing it leaks GPU memory for the rest of the session. Test 7.5 in the test plan exists specifically to catch a regression there.

### Traffic

- `Scripts/Traffic/WaypointPath.cs` + `TrafficWaypoint.cs` — one path type shared by traffic and pedestrians. Branching is a list of continuations picked at random, so agents circulate with no runtime pathfinding. Waypoints optionally carry a speed limit, a governing traffic light, or a crossing flag.
- `Scripts/Traffic/TrafficVehicle.cs` — **kinematic on purpose.** WheelCollider physics for a dozen background cars would cost far more than it adds; the player's car is still a dynamic body, so collisions with it behave normally and the traffic car is not thrown around. It has **no `Update` of its own** (the manager ticks it) and its obstacle sensor runs on a staggered timer (0.15s, phase-offset per vehicle) rather than every frame — directly addressing the brief's warning about Update cost on many AI cars.
- `Scripts/Traffic/TrafficManager.cs` — instantiates a fixed pool once at startup and never grows it, so there is no runtime instantiation during play. The active count comes from the graphics tier (~4 / 8 / 12) with a hard scene ceiling, and changing the setting adjusts the count live.

### Traffic lights

- `Scripts/Traffic/TrafficLight.cs` — a light head with green/amber/red glow objects. Yellow counts as "stop if you have not entered yet".
- `Scripts/Traffic/TrafficLightGroup.cs` — two-phase junction controller. State is driven by the group rather than by each light's own timer, so lights at one junction cannot drift out of step. Deliberately not a general intersection simulation.

### Pedestrians

- `Scripts/Traffic/Pedestrian.cs` — walks a path, pauses at random intervals, and holds back when a vehicle is within range (a larger radius at crossings, so it waits for traffic before stepping into the road). Manager-ticked, staggered vehicle check, optional animator parameter that is skipped entirely when left blank.
- `Scripts/Traffic/PedestrianManager.cs` — same fixed-pool approach. The Low tier's budget is zero, so on Low every pedestrian is switched off and the manager's Update returns immediately.

### HUD and end-of-mission screens

- `Scripts/UI/GameplayHudView.cs` — mission name, score, coins, countdown for timed missions, and the **parking feedback indicator**: neutral/red → amber → green, with a progress bar that fills over the hold time so the player can see the park being confirmed instead of guessing.
- `Scripts/UI/MissionResultView.cs` — both end screens in one component (two panels). Complete shows score, stars, time, collisions, overtime penalty, coins earned and a new-best badge, with Next / Replay / Menu; Failed shows the reason (`Score Too Low`, `Time Expired`, `Left Mission Area`, `Mission Cancelled`) with Retry / Menu. **There is no mission 31**: past the end of the ladder the Next button is hidden and a completion message is shown instead. One component rather than two because they share the same result data, star widgets and buttons — wiring that twice in the Inspector is how references get missed.

### Android pass

- `Editor/AndroidReadinessCheck.cs` — automates the Android checklist (identity, orientation, SDK levels, architectures, scripting backend, quality levels, per-platform default quality, render pipeline asset, build scenes). Ran headlessly and **independently confirmed the two configuration bugs found in the Phase 0 audit**, plus three release-readiness items:
  - Android's default quality level is index **2** while only 2 levels exist (`Mobile`, `PC`) → Unity clamps it, so Android was not reliably starting on `Mobile`.
  - The global default render pipeline asset is **`PC_RPAsset`**, not the mobile one.
  - Bundle id is still `com.UnityTechnologies.Mobile3DTemplate`, company is still `DefaultCompany`, and target SDK is `Automatic`.
  - Confirmed good: IL2CPP, ARM64, Vulkan + GLES3, landscape-only, active build target Android, one enabled build scene.
- `Editor/AndroidProjectSettingsFixer.cs` — one-click fix for the two unambiguous bugs (Android default quality → the `Mobile` level, global pipeline → `Mobile_RPAsset`). **Deliberately a menu item, not applied automatically**: these are project-wide render settings and whoever runs it should be able to look at the result. Identity and SDK values are left alone because they are release decisions, not bugs.
- `Editor/AndroidBuildScript.cs` — batch-mode development APK build that changes no project settings and writes outside the repo when `CARPARKING_APK_PATH` is set.
- `Docs/TEST_PLAN.md` — ~90 manual tests across regression, save/migration, parking validation, scoring, missions, economy, garage, vehicle features, mirrors, traffic, open world, settings and a device pass. Each is tagged READY / NEEDS WIRING / NEEDS CONTENT so it is obvious what can be tested today.

Performance work in this round was structural rather than micro-optimisation: fixed pools with no runtime instantiation, manager-driven ticking instead of per-agent `Update`, staggered sensors, mirrors rendering every *n*th frame and never off-screen, the shared throttled `ActiveVehicleLocator` instead of `FindObjectsByType` in Update loops, and traffic/pedestrian/mirror budgets all driven from the player's graphics setting.

### Verification

- **Compile:** clean (log grepped for `error CS`).
- **`AndroidReadinessCheck`:** ran headlessly, output above.
- **APK build: succeeded.** A development APK was built in batch mode with the bundled Android SDK/NDK/OpenJDK: `Result: Succeeded, errors: 0, time: 00:03:58`, IL2CPP/ARM64. So the project still compiles, links and packages for Android with all of the new code in it.
- Existing self-checks still green.

### One thing the build surfaced: APK size

The resulting APK is **264 MB**. That is a development build (no managed code stripping, debug symbols included), so a release build will be considerably smaller — but 264 MB is large enough to be worth measuring properly before shipping, and it is the one concrete number this round produced for the Android optimisation work:

- Build a **release** APK/AAB and compare, with managed stripping enabled.
- Check the Editor's build report for which assets dominate. The project carries several asset packs that gameplay does not reference (a 6-car pack of which 3 cars are used, `Hatchback and Sedan`, `Prototype Collection`, `JMO Assets` Cartoon FX, `SimplePixelUI`, and half a dozen demo scenes). Unused assets should not be included, but anything reachable from a built scene is.
- Confirm Android texture compression is ASTC rather than uncompressed/RGBA32.

This was not investigated further — it needs the build report open in the Editor, and guessing at texture settings from outside would be exactly the kind of blind change that breaks a working build.

### Not verified — and why

No traffic, pedestrian, mirror or HUD behaviour was seen running: all of it needs prefabs, waypoint paths and canvas wiring that only exist inside the Editor. Mirror camera aiming, traffic path layout and pedestrian crossing placement are all judgement calls that need the Scene view.

### Known limitations / deferred

- **Traffic cars must be tagged to score correctly.** `ScoreRules` has a `Vehicle` entry; a traffic prefab with an unlisted tag falls back to the default penalty. Tags are not set at runtime on purpose — assigning a tag that does not exist in the project throws.
- Traffic uses a simple sphere-cast ahead; it will not negotiate a blocked junction, it just waits. That is intentional.
- Pedestrians have no animation state machine, only an optional float parameter.
- Mirrors need one camera each; three mirrors on High is three extra (throttled) renders — measure on a real mid-range device before shipping High with side mirrors on.
- Engine audio still does not follow the SFX slider (`CarEngineAudio` builds its own sources).
- The open-world scene still does not exist, so everything in section 9 of the test plan is untestable.

---

## Garage, Colours, Upgrades, Vehicle Features, Cameras, Settings, Open World Framework (done)

Continuing the dependency order: Garage → Colour Customisation → Optional Vehicle Upgrades → Camera/Vehicle Features → Settings → Open World Framework → Challenge Markers.

### Garage and colours

- `Scripts/Garage/CarDefinition.cs` + `CarCatalog.cs` — per-car id, display name, price, stat ratings and thumbnail. **Ownership stays keyed by the car's child index in the player-car container**, which is how `CarSelection` has always identified cars; the definition carries a stable `carId` for display and debugging. A save-format migration to string keys was deliberately *not* done: mapping index → id correctly requires knowing the container's real child order, which cannot be verified from outside the Editor, and guessing it would silently give players the wrong car. `Tools → Car Parking → Validate Garage` prints the actual scene object each index resolves to so the mapping can be confirmed by eye.
- `Scripts/Garage/CarColorPalette.cs` + `CarPaintTarget.cs` — eight colours, applied through a **MaterialPropertyBlock**. Writing to `Renderer.material` would permanently modify the project's shared material assets; a property block keeps it per-renderer and per-session. `CarPaintTarget` lists which renderers are bodywork so painting a car does not also tint its windows, lights and tyres, and warns once if nothing is assigned.
- `Scripts/Garage/GarageManager.cs` — preview/next/previous, buy, select, paint, upgrade; keeps the showroom and gameplay cars in step. It also writes the legacy `SelectedCarIndex` PlayerPrefs key (toggleable) so the two existing in-scene `CarSelection` components stay correct until they are removed — without that bridge, the old and new systems would disagree about which car is selected.
- `Scripts/UI/GarageView.cs` — buttons, price, coin balance, owned/locked/selected states, stat bars, colour swatches and upgrade rows.

### Vehicle upgrades (attempted, kept deliberately small)

- `Scripts/Garage/UpgradeRules.cs` + `VehicleUpgradeService.cs` — three levels per part, costs 1500/2500/3500, gains of +5% engine, +5% brakes, +4% handling per level (so +15% at most). **No WheelCollider configuration is touched** — friction curves and suspension are left exactly as tuned — which is the condition the brief set for attempting this feature at all.
- `CarController` now caches its authored acceleration, brake force and turn sensitivity and applies upgrades as **multipliers on top of those**. This also fixes an interaction bug that would otherwise have been introduced: `SetVehicleEnabled(true)` restored the authored acceleration, so unfreezing the car after a mission would have silently wiped the player's engine upgrade.
- Still needs real device feel-testing; if +15% torque destabilises anything, the gains are one number each in `UpgradeRules`.

### Driving features and cameras

- `Scripts/Vehicle/VehicleLights.cs` — headlights, brake lights and indicators. Lights are driven by **showing/hiding glow objects plus optional real `Light`s**, not by pushing an emissive colour: a `MaterialPropertyBlock` cannot enable a shader's emission keyword, so on a material with emission off, writing `_EmissionColor` would silently do nothing. Indicators blink on a configurable interval, pressing the same side cancels it, and the opposite side replaces it. Brake lights also trigger on strong deceleration, not just on the brake input.
- `Scripts/Vehicle/VehicleHorn.cs` — one AudioSource created once and reused, so holding the button cannot spawn sources. Volume follows the SFX setting.
- `Scripts/Vehicle/ActiveVehicleLocator.cs` — shared, throttled lookup of the car the player is driving (0.5s between searches), so the HUD and cameras never call `FindObjectsByType` in an Update loop.
- `Scripts/UI/SpeedometerView.cs` — smoothed km/h readout from `CarController.CurrentSpeedKmh`, with an optional radial fill.
- `Scripts/Vehicle/VehicleCameraDirector.cs` — cycles third-person → cockpit → rear. **Third person is not reimplemented**: the existing `CarCameraController` stays in charge of it, so that view behaves exactly as it does today; the other two modes park the camera rigidly on the car.
- These components expose plain public methods (`ToggleHeadlights`, `ToggleLeftIndicator`, `Play`, `CycleMode`), so HUD buttons can call them directly from the Inspector with no glue code.

### Settings and graphics tiers

- `Scripts/Settings/QualityProfile.cs` — what Low/Medium/High actually mean: shadows on/off and softness, shadow distance and resolution, LOD bias, **whether realtime mirrors are allowed**, and traffic/pedestrian budgets. The mirror and traffic flags exist now so the systems added later read the player's setting instead of hardcoding counts.
- `Scripts/Settings/SettingsManager.cs` — applies settings at startup and on change: shadow quality/distance/resolution, LOD bias, `Application.targetFrameRate` with `vSyncCount = 0` (Android ignores the target frame rate otherwise), and steering sensitivity. Falls back to sane defaults when no profile asset is assigned rather than breaking.
- `Scripts/Settings/AudioCategoryVolume.cs` — per-source music/SFX volume. The project has no AudioMixer and one with exposed parameters cannot be created reliably from a script, so volume is applied per source; a mixer remains the better long-term answer and is noted as such.
- `Scripts/UI/SettingsView.cs` — volume/sensitivity sliders, quality and frame-rate buttons, and Reset Progress behind a confirmation step. Sliders are refreshed with `SetValueWithoutNotify` so opening the panel does not write the same values back to disk.
- Steering sensitivity multiplies the *input* (`VehicleInput.SteeringSensitivity`), kept separate from `CarController.turnSensitivity`, which is the vehicle's own physics tuning that upgrades scale.

### Open world framework and challenge markers

- `Scripts/OpenWorld/ChallengeDefinition.cs` — per-challenge id, name, difficulty, reward and parking requirements. Kept separate from `MissionDefinition` rather than sharing a base type: challenges use stable string ids, are not part of the practice ladder, and pay on a different scale.
- `Scripts/OpenWorld/ChallengeMarker.cs` — a place in the world where a challenge can start, with an activation radius and gizmos. Markers do **not** run their own proximity checks.
- `Scripts/OpenWorld/OpenWorldChallengeManager.cs` — scans all markers on a throttled timer (0.25s) so adding markers does not add Update calls, then runs the challenge with the same `ParkingValidator` and `MissionScoreTracker` as practice missions. **Nothing is reloaded**: the car stays where it is, and afterwards the player simply drives away. Best score/stars per challenge are saved, and coins use the same anti-farming rule. Cancelling is not recorded as a failure — it is free driving.
- `Scripts/UI/ChallengePromptView.cs` — the name/difficulty/reward/best-score panel with START and cancel.
- `Scripts/UI/ChallengeCompassView.cs` — an arrow to the nearest challenge plus distance. Deliberately **not a minimap**: a second camera rendering the world is not worth the frame budget here, which the brief also called out.

### Save model additions

`CarSaveEntry` gained `engineLevel`/`brakeLevel`/`handlingLevel`; a new `ChallengeSaveEntry` list records per-challenge completion and bests. Both are additive fields — an existing save file simply deserialises them as defaults — so **no save version bump and no migration were needed**. `Sanitize()` clamps the new fields and de-duplicates challenge records.

### Verification

- **Compile:** clean via Unity 6000.0.29f1 batch mode. One real error was caught and fixed on the way (`SettingsView` was missing a `using` for `SettingsSaveData`). Worth noting for future runs: **Unity returns exit code 0 even when compilation fails**, so the log has to be grepped for `error CS` — the exit code alone is not a pass.
- **`GameplaySelfCheck`:** 0 failures, now ~60 assertions, extended with upgrade costs/multipliers and clamping, quality-tier sanity (low has no mirrors and no pedestrians, traffic density rises with tier and stays inside the mobile budget), and the new save fields' clamping/de-duplication.
- **Assets generated and verified on disk:** 3 car definitions, car catalog, 8-colour palette, upgrade rules, quality profile.

### Not verified — and why

Nothing was played and no scene was touched. Cockpit and rear camera offsets are starting values that need looking at from inside the cars; the colour palette, stat ratings and upgrade gains are design defaults; light positions depend on how the glow objects get wired. `Docs/SETUP_CHECKLIST.md` lists what to wire and what to judge by eye.

### Known limitations / deferred

- **No open-world scene exists.** The challenge framework is scene-agnostic and works in any scene, but building the map (city roads, parking lots, garage, multi-storey, driving school) is authoring work that needs the Editor. Challenge definition assets are created per location from `Create → Car Parking → Open World Challenge`; none were generated, since placeholder challenges pointing at no bay would only be noise.
- **Touch-drag camera orbit is still missing.** The existing `CarCameraController` reads `Input.GetAxis("Mouse X/Y")`, which returns nothing on Android, so the third-person camera cannot be rotated by touch on a device. Left alone deliberately: adding touch input that cannot be tested on hardware is worse than a documented gap.
- Car ownership remains index-keyed (see above).
- Mirrors, traffic and pedestrians are not implemented; their budgets are already in `QualityProfile`.
- Music/SFX volume applies only to sources carrying `AudioCategoryVolume`; the existing `CarEngineAudio` builds its own sources and is not yet covered.

---

## Phases 3–11 — Vehicle/Input, Parking Validator, Scoring, Mission Architecture, Selection UI, Economy (done)

Worked in the master prompt's **dependency order** ("this order matters"), not its phase numbering: Vehicle/Input Cleanup → Parking Validator → Score/Stars → Mission Data Architecture → Existing 8 Mission Migration → Dynamic Mission Selection → Economy. Headlights, horn, indicators, extra cameras and mirrors sit *after* the garage in that order and need scene wiring, so they are not part of this round.

### Existing scripts changed (small, behaviour-preserving edits)

- **`CarController.cs`** — every serialized public field kept exactly as-is (scene references and authored values are untouched), plus `SetMoveInput`/`SteerInput` kept because the scene may invoke them by name through UnityEvents. Added: `ThrottleInput`, `SteeringInput`, `IsBraking`, `VehicleEnabled`, `Velocity`, `CurrentSpeed`, `CurrentSpeedKmh` (the speedometer's data source), and `SetVehicleEnabled(bool)`. Input reading moved out to `VehicleInput`. Two real bugs fixed:
  - **The brake button did nothing.** `LateUpdate` read `if (Input.GetKey(Space) || control == Keyboard) Brake(); else if (SimpleInput.GetButton("Break") || control == Buttons) Brake();` — the `|| control == ...` half is true in every mode, so `Brake()` ran every frame regardless and the button's state was never actually consulted. Braking-when-throttle-is-released (what makes the car settle) is preserved; the button now also bites while the throttle is held.
  - **Freezing the car permanently retuned it.** `ParkingTrigger`/`MissionFailedHandler` set `maxAcceleration = 0` to freeze and `= 5` to release, so after the first mission pass or retry every car was left at 5 instead of its authored 20. `SetVehicleEnabled` now caches the authored value at `Awake` and restores it, and applies the brakes so a frozen car stops instead of coasting.
- **`ParkingTrigger.cs`, `MissionFailedHandler.cs`** — the four `maxAcceleration = 0 / = 5` sites now call `SetVehicleEnabled(false/true)`. Nothing else changed; both scripts keep working exactly as before.

### New systems (all additive, none wired into the scene yet)

- `Scripts/Vehicle/VehicleInput.cs` — `VehicleInputState` + keyboard/mobile readers. SimpleInput stays the mobile path; its misspelled `"Break"` button name is kept deliberately, since renaming it would break the scene's serialized button wiring.
- `Scripts/Vehicle/VehicleCollisionReporter.cs` — reports collision tag/impulse/collider id. **Added to the active car at runtime**, so no car prefab or scene object needs editing.
- `Scripts/Parking/ParkingZone.cs` — a bay as an oriented box, with scene gizmos. Containment is measured from the **car's four footprint corners** (derived from its WheelCollider positions), not a single trigger touch, so a car hanging half out no longer counts as parked. Height is ignored on purpose — bays sit on kerbs and ramps.
- `Scripts/Parking/ParkingValidator.cs` — real parking validation: inside the bay, heading within tolerance, speed below threshold, held for a configurable time, plus a genuine reverse-entry requirement for reverse missions. Every threshold is pushed in per mission. Raises `StateChanged`/`ProgressChanged`/`Validated` for the (not yet built) neutral/partial/valid HUD feedback. Logs a loud warning when a bay is smaller than the car and containment is set to 1, because that combination is silently impossible to complete.
- `Scripts/Progression/ScoreRules.cs` — ScriptableObject: starting score 100, tag-driven collision penalties (cone 3 / barrier 5 / vehicle 10, default 5), area/alignment/overtime penalties, collision cooldown, impulse thresholds, and the 90/70/50 star thresholds. Tag-driven rather than hardcoded because the project currently only defines a `Cone` tag.
- `Scripts/Progression/MissionScoreTracker.cs` — plain class (not a MonoBehaviour) so the whole scoring model is exercisable headlessly. Per-collider debounce: scraping one cone repeatedly costs once per cooldown, and impulse scales the penalty so a brush costs half and a heavy hit costs double.
- `Scripts/Progression/EconomyManager.cs` — coins with anti-farming: a first clear or a genuine personal best pays in full, a replay that improves nothing pays 25% (floor of 10). Rewards are computed and granted **once, at the moment the mission ends**, and the amount is carried on the result object — so reopening or reloading the results screen cannot pay again.
- `Scripts/Missions/MissionDefinition.cs` + `MissionCatalog.cs` — static per-mission config as ScriptableObjects, plus the ordered catalog the UI reads. The catalog is independent of the scene, so the full 30-mission ladder can be displayed (and locked) before every layout exists.
- `Scripts/Missions/MissionAuthoring.cs` — the scene half of a mission (start point, parking zone, environment container, optional allowed-area volume) matched to a definition by id, with gizmos and a `CollectIssues` self-report. Scene references cannot live in a ScriptableObject, which is why authoring is split this way.
- `Scripts/Missions/MissionManager.cs` + `MissionResult.cs` — runs a mission end to end: place the car, configure the validator from the definition, track score/time, detect timeout and leaving the allowed area, then write progress to the save file, unlock the next mission and pay out. Fail reasons are modelled (`ScoreTooLow`, `TimeExpired`, `LeftMissionArea`, `Aborted`) for the Phase 21 failure screen.
- `Scripts/UI/MissionCardView.cs` + `MissionSelectionView.cs` — mission list built from the catalog instead of eight hand-placed buttons, showing number, name, difficulty, lock state, best stars and best score. Growing to 30 missions is now a data change, not a UI change. Uses legacy uGUI `Text` to match the rest of the project.

### Editor tooling (Phase 27 groundwork)

- `Editor/MissionContentGenerator.cs` — generates `DefaultScoreRules.asset`, `MissionCatalog.asset` and `Missions/Mission01…30.asset`. Idempotent (updates in place). **The 30-mission difficulty curve lives in `BuildSpecs()`** — tolerances tighten from 20° to 6°, hold times grow from 1.2s to 2.0s, containment from 0.75 to 1.0, parking speed from 2 to 1 km/h, and timed missions only begin at 21. Practice payouts are fixed at 300 coins for three stars (so 100/200/300 per star, as specified).
- `Editor/MissionSceneSetupTool.cs` — converts the existing 8 missions by reading the legacy `GameManager` arrays: adds `MissionAuthoring` per mission area and a `ParkingZone` per parking trigger, copying the trigger's `BoxCollider` size so bays match what was already authored. Single undoable action, does not save the scene. Also `Validate Missions`: missing refs, duplicate ids, invalid rewards/tolerances, and which catalogued missions still have no layout.
- `Editor/GameplaySelfCheck.cs` — headless checks for star thresholds, impulse scaling, collision debounce, overtime penalties, reward/anti-farming maths, and parking containment/heading geometry.

### Verification

- **Compile:** clean via Unity 6000.0.29f1 batch mode. (One transient `CS0234` appeared in an intermediate compile pass of the first run, while `VehicleInput.cs` was still being imported — the log shows Unity immediately recompiling and succeeding, and later runs are clean from the first pass.)
- **`GameplaySelfCheck`:** 0 failures — ~40 assertions across scoring, debounce, overtime, economy and parking geometry.
- **`SaveSystemSelfCheck`:** still 0 failures.
- **Mission content:** 30 definition assets + catalog + score rules generated and verified on disk (spot-checked `Mission30.asset`: id 30, reverse, 90s limit, 6° tolerance, 2s hold, containment 1, opposite heading disallowed, reward 300, score rules linked).

### Not verified — and why

No gameplay was played and no scene was modified. Mission layouts, UI appearance, and the actual *feel* of the brake fix need a human in the Editor. The scene wiring is left as a one-click, undoable Editor action rather than something done blindly from outside Unity: its correct output depends on scene state (bay sizes, hierarchy) that can't be judged from YAML. See `Docs/SETUP_CHECKLIST.md` for the ordered handover steps.

### Known limitations / deferred

- **`MissionManager` must not run at the same time as the legacy `GameManager` mission flow** — both would spawn/freeze the car and write progress. The switch-over is a deliberate, separate step.
- Progression still lives in PlayerPrefs until that switch; `SaveManager` only takes over when `MissionManager` does.
- No parking-feedback visual, no speedometer UI, no HUD yet (the data is exposed; the UI is Phase 19).
- Missions 9–30 exist as data only — no layouts. They show as locked in the selection UI.
- "Improper parking/alignment" penalty exists in `ScoreRules` but is not applied yet (parking currently either validates or doesn't; a partial-credit alignment penalty needs the validator to report *how* well the car was aligned at the moment of success).

---

## Phase 2 — Save System (done)

**Files created:**
- `Assets/GameAssets/Scripts/Progression/MissionProgressData.cs` — per-mission record with `unlocked` and `completed` as **separate** fields (the legacy `bool[] missionCompleted` conflated them), plus `bestScore`, `bestStars`, `bestTimeSeconds`, `bestCollisions`. `SubmitResult(...)` returns whether the attempt was a personal best, which the economy phase needs for anti-farming.
- `Assets/GameAssets/Scripts/Progression/SaveData.cs` — root save model (`saveVersion`, `selectedCarIndex`, `coins`, `currentMissionId`, `lastGameMode`, car entries, mission entries, settings) with `CreateDefault()` and `Sanitize()`. Covers every field the master prompt requires the save to support, including car colour and the graphics/FPS/volume/sensitivity settings.
- `Assets/GameAssets/Scripts/Progression/LegacyPlayerPrefsMigration.cs` — converts the old PlayerPrefs progression into `SaveData`. Deliberately split into PlayerPrefs reads (`Read()`) and a **pure** conversion (`Apply()`) so the conversion can be validated headlessly.
- `Assets/GameAssets/Scripts/Core/SaveManager.cs` — JSON store at `Application.persistentDataPath/carparking_save.json`. Handles no-save-file, corrupt-save quarantine, atomic writes, and `ResetProgress()`.
- `Assets/GameAssets/Editor/SaveSystemSelfCheck.cs` — Editor-only validation (menu item + `-executeMethod` entry point) exercising only pure functions; never touches real PlayerPrefs or the real save file.

**Files changed:** none. No existing script and no scene was modified.

**Save migration behaviour (the important part):**

The legacy `GameManager.CompleteMission()` did `missionCompleted[current] = true; current++; missionCompleted[current] = true;` — it marked the *next* mission's flag as well, to unlock it. So the old `bool[]` **cannot** tell "completed" from "merely unlocked", and its highest flagged entry is normally a mission the player never finished. Migrating it naively would hand players free completions (and later, free stars/coins).

`CurrentMission` is the trustworthy marker: it only ever advances inside `CompleteMission()`. So the conversion is:
- `completed` = legacy index `< CurrentMission`
- `unlocked` = the legacy flag was set, **or** the index is at/below `CurrentMission`
- mission ids are 1-based (legacy index `i` → mission `i + 1`), matching the "Mission 01 … Mission 30" design list
- `CurrentMission == 8` (the old "All Missions Completed" out-of-range state) → all 8 marked completed and **mission 9 unlocked**, ready for the Phase 9 content
- legacy completions get **no fabricated** score/star/time records; they sit at "no record" until replayed
- **migrated players keep all three cars** (the old build let you drive all 3 for free — charging an existing player for a car they already had would be a regression). Fresh installs own car 0 only.
- legacy PlayerPrefs keys are **read but never deleted**, so a downgrade still finds its data and nothing is lost if the conversion ever needs revisiting.

**Old systems replaced:** none yet, deliberately. `GameManager` and `CarSelection` still own their PlayerPrefs keys and still fully drive the game. Migration runs the first time `SaveManager.Load()` is called, which no gameplay script does yet — the handover happens in the mission-data phase as a single coherent switch. Two systems writing progression at once could let them diverge on real player data, which is the one failure mode worth designing against here.

**Verification:** `SaveSystemSelfCheck` run headlessly via `Unity.exe -batchmode -quit -executeMethod …` → **0 failures**, exit code 0, no `error CS`. It covers: fresh-install defaults; mid-progress migration; fully-completed migration (including the mission-9 unlock); never-played migration; repair of a save with null/zeroed fields; and clamping of out-of-range volumes, quality, frame rate, stars, scores and duplicate mission entries.

**Manual Unity setup still required:** none for this phase. See `Docs/SETUP_CHECKLIST.md` for where the save file lives, how corrupt saves are quarantined, and the three save shapes worth testing on a device once migration goes live.

**Known limitations / deferred:**
- Car ownership/colour is keyed by **car index** (how `CarSelection` already identifies cars today), not a stable string id. Phase 12 (Garage) introduces `CarDefinition` ids and bumps `saveVersion` to migrate — inventing ids now would mean guessing the container's child order, which can't be verified from outside the Editor.
- `ResetProgress()` has no UI yet (Settings, Phase 22).
- Settings live in the same JSON file rather than PlayerPrefs — one source of truth, still satisfies "persist settings".

---

## Phase 1 — Core Architecture Refactor (done)

**Compile validation:** ran `Unity.exe -batchmode -nographics -projectPath . -quit` with Unity 6000.0.29f1 (matches the project's `ProjectVersion.txt`). Exit code 0, no `error CS` in the log. Project compiles cleanly with `GameFlowManager.cs` added.

**Files created:**
- `Assets/GameAssets/Scripts/Core/GameFlowManager.cs` — new, additive. Self-initializing (`[RuntimeInitializeOnLoadMethod]`, same pattern already used by `OrientationLock.cs`) so it requires **no scene wiring**. Tracks `GameMode` (`MainMenu`/`Practice`/`OpenWorld`) and `GameState` (`Menu`/`Playing`/`Paused`/`Result`) with C# events (`GameModeChanged`, `GameStateChanged`) so later systems (Mission Manager, Garage, Open World) can react without hard references. Folded what the master prompt calls "GameFlowManager" and "GameModeManager" into a single class for now — they were small enough that a second class would just be indirection; will split if it grows unwieldy.
- `Docs/UPGRADE_PLAN.md` (Phase 0 deliverable, see below)
- `Docs/UPGRADE_CHANGELOG.md` (this file)
- `Docs/SETUP_CHECKLIST.md`

**Files changed:** none. No existing script was modified. No scene (`complete_track_demo.unity`) was touched.

**Old systems replaced:** none yet. `GameManager`, `MainMenuManager`, `CarController`, etc. are untouched and still fully in control of the game. `GameFlowManager` exists but nothing calls it yet — it's scaffolding for the Save/Mission/Garage phases that come next, per the master prompt's explicit phase ordering (Core Architecture is its own step before Save Migration, Vehicle/Input Cleanup, Parking Validator, etc.).

**Save migration behavior:** N/A this phase (Phase 2).

**Manual Unity setup still required:** see `Docs/SETUP_CHECKLIST.md` — one pre-existing bug found during the Phase 0 audit (car tagged `MainCamera` instead of `Player`) needs a one-click Inspector fix; not touched programmatically because a live scene YAML edit for this was judged higher-risk than a documented manual fix.

**Known limitations / deferred:** Full manager set (`MissionManager`, `SaveManager`, `ParkingValidator`, `ScoreManager`, `EconomyManager`, `GarageManager`, `SettingsManager`, `VehicleManager`) is intentionally NOT created yet — each belongs to its own later phase per the master prompt's implementation order and will be added when that phase starts, reading real data out of the existing scene rather than being pre-built empty.

---

## Phase 0 — Project Audit

Read-only. No files created or changed except `Docs/UPGRADE_PLAN.md` (the audit document itself). See that file for full findings (architecture, risks, PlayerPrefs keys, Android/URP config, bugs found).
