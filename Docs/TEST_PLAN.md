# TEST_PLAN.md

Manual test plan for the upgraded Car Parking Simulator. Written for whoever runs the game in the Editor and on a device — it assumes access to the Unity project but no knowledge of how the new systems are put together.

**Status key**

- **READY** — testable now, nothing else needed.
- **NEEDS WIRING** — the code exists but a scene/prefab step from `SETUP_CHECKLIST.md` must be done first.
- **NEEDS CONTENT** — needs level/scene authoring that does not exist yet.

Automated checks that cover the pure logic already run headlessly and should be green before manual testing starts:

```
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod CarParkingGame.EditorTools.SaveSystemSelfCheck.RunFromCommandLine
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod CarParkingGame.EditorTools.GameplaySelfCheck.RunFromCommandLine
Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod CarParkingGame.EditorTools.AndroidReadinessCheck.RunFromCommandLine
```

**Important:** Unity exits with code 0 even when C# compilation fails. Always grep the log for `error CS` — the exit code alone does not tell you the run passed.

---

## 1. Regression: the game that already worked

These must still behave exactly as before the upgrade. Test them first — if any fail, stop and fix before looking at new features.

| # | Test | Status | Expected |
|---|---|---|---|
| 1.1 | Launch, main menu appears, time is paused | READY | Menu shows, cars in showroom visible, mobile controls hidden |
| 1.2 | Pick each of the 3 cars, press Done | READY | The car you picked is the one you drive |
| 1.3 | Drive mission 1 and park in the trigger | READY | Mission Passed appears (legacy path) |
| 1.4 | Press Next Mission | READY | Next mission area activates, car respawns at its start point |
| 1.5 | Hit a cone | READY | Mission Failed appears (legacy instant-fail is still in place) |
| 1.6 | Retry after failing | READY | Car respawns, controls work again |
| 1.7 | Quit and relaunch | READY | Same mission and same car as before |
| 1.8 | Android landscape | READY | Never rotates to portrait |

### 1.9 Car acceleration after a retry — **deliberate behaviour change**
| Status | READY |
|---|---|
| Steps | Pass or fail a mission, then continue driving |
| Before | Every car was left at `maxAcceleration = 5` regardless of its authored value (20) |
| Now | Each car returns to its authored value |
| Expected | The car feels **faster after a retry than it used to**. This is the fix, not a bug. If it now feels too fast, lower `maxAcceleration` on the cars themselves. |

### 1.10 Brake button — **deliberate behaviour change**
| Status | READY |
|---|---|
| Steps | Hold the throttle, then press the on-screen brake |
| Before | The button did nothing (braking only happened when the throttle was released) |
| Now | The brake bites while the throttle is held; releasing the throttle still settles the car as before |
| Expected | Both behaviours present. Check on a device, not just with a keyboard. |

---

## 2. Save system and migration

| # | Test | Status | Expected |
|---|---|---|---|
| 2.1 | Fresh install (delete `carparking_save.json` **and** clear PlayerPrefs) | NEEDS WIRING | Mission 1 unlocked, nothing completed, 0 coins, car 0 owned only |
| 2.2 | Migration mid-progress: old save with missions 1–2 done, sitting on 3 | NEEDS WIRING | 2 completed, mission 3 unlocked and unplayed, **no stars awarded for the migrated ones** |
| 2.3 | Migration of a finished old save (all 8 done) | NEEDS WIRING | 8 completed, mission 9 unlocked |
| 2.4 | Migration of a never-played old save | NEEDS WIRING | Only mission 1 unlocked, nothing completed |
| 2.5 | Migrated player keeps all 3 cars | NEEDS WIRING | All three owned without paying — an existing player must not be charged for cars they already had |
| 2.6 | Legacy PlayerPrefs survive migration | NEEDS WIRING | The old keys still exist afterwards (migration reads, never deletes) |
| 2.7 | Corrupt save | NEEDS WIRING | Truncate `carparking_save.json` mid-file → game starts from defaults, and the bad file is renamed to `…corrupt_<timestamp>`, **not deleted** |
| 2.8 | Missing save file | READY | No exception; defaults created and written |
| 2.9 | Reset Progress | NEEDS WIRING | Progress cleared, settings kept, confirmation step required first |
| 2.10 | Kill the app mid-play, relaunch | NEEDS WIRING | Last saved progress intact, no corruption |

---

## 3. Parking validation and scoring

| # | Test | Status | Expected |
|---|---|---|---|
| 3.1 | Park fully inside the bay, straight, stopped | NEEDS WIRING | Progress bar fills over the hold time, then the mission completes |
| 3.2 | Park with the car half out of the bay | NEEDS WIRING | Indicator amber, never completes |
| 3.3 | Park inside but crooked (beyond the tolerance) | NEEDS WIRING | Never completes |
| 3.4 | Roll slowly through the bay without stopping | NEEDS WIRING | Never completes |
| 3.5 | Leave the bay during the hold | NEEDS WIRING | Progress resets to zero |
| 3.6 | Reverse mission entered nose-first | NEEDS WIRING | Does **not** complete — reverse missions require actually reversing in |
| 3.7 | Clip one cone lightly | NEEDS WIRING | Small point loss, mission continues (no instant fail) |
| 3.8 | Scrape along the same cone for several seconds | NEEDS WIRING | Charged once per cooldown, not once per physics frame |
| 3.9 | Ram a barrier hard | NEEDS WIRING | Larger penalty than a light brush |
| 3.10 | Finish with a score below 50 | NEEDS WIRING | Mission **failed** with reason "Score Too Low" |
| 3.11 | Score 90+/70–89/50–69 | NEEDS WIRING | 3 / 2 / 1 stars |
| 3.12 | Overrun a timed mission's limit | NEEDS WIRING | Fails with "Time Expired" |
| 3.13 | Bay smaller than the car | READY | A warning is logged at mission start saying the mission cannot be completed — this is the most likely problem after running the mission setup tool |

---

## 4. Missions, selection UI and economy

| # | Test | Status | Expected |
|---|---|---|---|
| 4.1 | Mission list shows all 30 | NEEDS WIRING | Numbers, names, difficulty, lock state, best stars and best score |
| 4.2 | Locked missions look locked and cannot be tapped | NEEDS WIRING | Missions 9–30 appear locked until laid out and unlocked |
| 4.3 | Completing a mission unlocks the next | NEEDS WIRING | Next card becomes available |
| 4.4 | Best stars/score persist after a relaunch | NEEDS WIRING | Values survive |
| 4.5 | First clear pays 100/200/300 coins for 1/2/3 stars | NEEDS WIRING | Coin balance rises accordingly |
| 4.6 | Replay without improving | NEEDS WIRING | Pays only a quarter (minimum 10) |
| 4.7 | Replay that beats your best | NEEDS WIRING | Pays in full |
| 4.8 | **Anti-farming:** close and reopen the result screen repeatedly | NEEDS WIRING | Coins awarded **once**, at the moment the mission ended. Reopening pays nothing |
| 4.9 | Mission 30 completed | NEEDS CONTENT | No attempt to load mission 31; "all missions complete" shown and Next hidden |
| 4.10 | Mission validation tool | READY | `Validate Missions` lists missing refs, duplicate ids and un-laid-out missions |

---

## 5. Garage, colours, upgrades

| # | Test | Status | Expected |
|---|---|---|---|
| 5.1 | Car index mapping | NEEDS WIRING | `Validate Garage` logs which scene car each index is — confirm the names match |
| 5.2 | Buy car 2 with enough coins | NEEDS WIRING | Coins deducted, car owned, selectable |
| 5.3 | Buy with insufficient coins | NEEDS WIRING | Purchase refused, no coins deducted, button disabled |
| 5.4 | Select an owned car | NEEDS WIRING | Showroom and the car you drive both change |
| 5.5 | Change colour | NEEDS WIRING | Only bodywork changes — glass, lights and tyres unaffected |
| 5.6 | Colour persists after relaunch | NEEDS WIRING | Same colour on the gameplay car |
| 5.7 | **Material safety:** after changing colours, check the material assets in the Project window | NEEDS WIRING | Project material assets are **unchanged** (colour is applied per-renderer, not written to the shared material) |
| 5.8 | Buy upgrades to max | NEEDS WIRING | Costs 1500/2500/3500, then shows MAX |
| 5.9 | Upgraded car feels slightly stronger | NEEDS WIRING | +15% at most. Check the car is still stable, no odd physics |
| 5.10 | **Upgrade survives freeze/unfreeze:** upgrade the engine, complete a mission, keep driving | NEEDS WIRING | The upgrade is still applied after the car is re-enabled |

---

## 6. Vehicle features and cameras

| # | Test | Status | Expected |
|---|---|---|---|
| 6.1 | Speedometer | NEEDS WIRING | Reads km/h, smooth, 0 when stopped |
| 6.2 | Headlight button | NEEDS WIRING | Toggles on/off, visible at night/in the garage |
| 6.3 | Brake lights | NEEDS WIRING | On when braking, and on under strong deceleration |
| 6.4 | Left/right indicators | NEEDS WIRING | Blink; pressing the same side cancels; the opposite side replaces it |
| 6.5 | Horn | NEEDS WIRING | Plays; holding the button does not stack sources or crackle |
| 6.6 | Camera cycle | NEEDS WIRING | Third-person → cockpit → rear → third-person |
| 6.7 | Third-person unchanged | NEEDS WIRING | Identical to before the upgrade |
| 6.8 | Cockpit/rear offsets | NEEDS WIRING | Sensible in **each** of the 3 cars — the defaults are guesses |
| 6.9 | Camera switching does not break driving | NEEDS WIRING | Controls keep working in every mode |
| 6.10 | Camera orbit by touch on device | **KNOWN GAP** | Does **not** work — the legacy camera reads mouse axes only. Decide whether this matters before release |

---

## 7. Mirrors

| # | Test | Status | Expected |
|---|---|---|---|
| 7.1 | Low quality | NEEDS WIRING | No mirror renders at all, mirror surfaces hidden |
| 7.2 | Medium quality | NEEDS WIRING | Rear mirror only, 256×128 |
| 7.3 | High quality | NEEDS WIRING | Rear + side mirrors, 512×256 |
| 7.4 | Switch quality at runtime | NEEDS WIRING | Mirrors appear/disappear without errors, textures resized |
| 7.5 | **Memory:** switch quality back and forth 20 times, watch the profiler | NEEDS WIRING | No growth in render-texture memory (textures are released, not leaked) |
| 7.6 | Look away from the mirror | NEEDS WIRING | Mirror camera stops rendering while off-screen |

---

## 8. Traffic, lights, pedestrians

| # | Test | Status | Expected |
|---|---|---|---|
| 8.1 | Traffic follows its paths | NEEDS CONTENT | Cars circulate, no drifting off the road |
| 8.2 | Traffic stops behind an obstacle | NEEDS CONTENT | Slows/stops when something is ahead, resumes after |
| 8.3 | Traffic stops at red | NEEDS CONTENT | Holds at the stop line, moves on green |
| 8.4 | Traffic count per quality | NEEDS CONTENT | ~4 / 8 / 12 on Low / Medium / High; never above the ceiling |
| 8.5 | Changing quality at runtime | NEEDS CONTENT | Count adjusts live, no instantiation spike |
| 8.6 | Hitting a traffic car | NEEDS CONTENT | Player takes a collision penalty; the traffic car is not thrown around (kinematic) |
| 8.7 | Traffic cars are tagged for scoring | NEEDS CONTENT | Their tag matches a `ScoreRules` entry, otherwise they charge the default penalty |
| 8.8 | Traffic lights cycle | NEEDS CONTENT | Green → yellow → red, both phases never green together |
| 8.9 | Pedestrians walk, pause, cross | NEEDS CONTENT | Walk between waypoints, pause occasionally, wait at crossings |
| 8.10 | Pedestrians avoid cars | NEEDS CONTENT | Hold back when a vehicle is close instead of walking through it |
| 8.11 | Pedestrians on Low | NEEDS CONTENT | None active at all |

---

## 9. Open world

| # | Test | Status | Expected |
|---|---|---|---|
| 9.1 | Open-world scene loads | NEEDS CONTENT | Scene exists and is in Build Settings |
| 9.2 | Free driving | NEEDS CONTENT | Drive anywhere, no challenge running |
| 9.3 | Approach a marker | NEEDS CONTENT | Prompt shows name, difficulty, reward, best score |
| 9.4 | Cancel the prompt | NEEDS CONTENT | Returns to free driving, nothing recorded |
| 9.5 | Complete a challenge | NEEDS CONTENT | Coins awarded, best score updated, **no scene reload**, driving resumes |
| 9.6 | Replay the same challenge | NEEDS CONTENT | Anti-farming applies as in practice mode |
| 9.7 | Compass | NEEDS CONTENT | Arrow points at the nearest challenge, distance sensible |
| 9.8 | Challenge id stability | NEEDS CONTENT | Ids are never changed after release — they are the save keys |

---

## 10. Settings

| # | Test | Status | Expected |
|---|---|---|---|
| 10.1 | Music/SFX sliders | NEEDS WIRING | Affect sources carrying `AudioCategoryVolume`. **Engine audio is not covered yet** |
| 10.2 | Steering sensitivity | NEEDS WIRING | Low = calmer steering, high = sharper; the car stays controllable at both ends |
| 10.3 | Quality Low/Medium/High | NEEDS WIRING | Shadows, shadow distance, mirrors, traffic and pedestrians all change |
| 10.4 | 30 FPS | NEEDS WIRING | Device frame rate caps near 30 |
| 10.5 | 60 FPS | NEEDS WIRING | Device frame rate rises where hardware allows |
| 10.6 | Settings persist | NEEDS WIRING | Survive a relaunch and are applied at startup, not only when the panel opens |
| 10.7 | Opening the settings panel | READY | Does not write the same values back to disk (sliders refresh without notifying) |

---

## 11. Android device pass

Run on a real low-end device, not just the Editor.

| # | Test | Expected |
|---|---|---|
| 11.1 | Development APK installs and launches | No immediate crash |
| 11.2 | Landscape only | Never rotates to portrait |
| 11.3 | Touch controls | Joystick, brake, and every HUD button reachable with thumbs |
| 11.4 | UI scaling | Readable at 16:9 and at a tall 20:9 aspect; nothing clipped by the notch/safe area |
| 11.5 | Low-end, Low quality | Stable ~30 FPS |
| 11.6 | Mid-range | 30–60 FPS depending on setting |
| 11.7 | Save persistence on device | Progress survives force-stop and relaunch |
| 11.8 | No desktop-only dependency | Nothing relies on mouse or keyboard input |
| 11.9 | Memory over a long session | Play 15+ minutes across several missions; memory does not climb steadily |
| 11.10 | Logcat is clean | No repeating `NullReferenceException`, `MissingReferenceException` or `IndexOutOfRangeException` |

---

## 12. Error classes to watch for throughout

- `NullReferenceException` — most likely from an unassigned Inspector reference on the new views.
- `MissingReferenceException` — a destroyed object still being used, typically after a scene change.
- `IndexOutOfRangeException` — car index vs. container children mismatch (see 5.1).
- Save corruption — check for `…corrupt_<timestamp>` files after any crash.
- Mission unlock bugs — a mission unlocking too early or not at all after completion.
- Double-awarded coins — the single most valuable thing to try to break deliberately (see 4.8).
