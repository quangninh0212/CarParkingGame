using System.Collections.Generic;
using CarParkingGame.Progression;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Headless validation of the save model and the legacy PlayerPrefs conversion.
    // Only pure functions are exercised: this never reads or writes real PlayerPrefs
    // keys or the real save file, so it is safe to run on a developer machine.
    public static class SaveSystemSelfCheck
    {
        [MenuItem("Tools/Car Parking/Run Save System Self-Check")]
        public static void RunFromMenu()
        {
            int failures = RunChecks();

            if (failures == 0)
            {
                Debug.Log("[SaveSystemSelfCheck] All checks passed.");
            }
        }

        // Batch entry point:
        // Unity.exe -batchmode -quit -projectPath . -executeMethod CarParkingGame.EditorTools.SaveSystemSelfCheck.RunFromCommandLine
        public static void RunFromCommandLine()
        {
            int failures = RunChecks();
            Debug.Log($"[SaveSystemSelfCheck] Finished with {failures} failure(s).");
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int RunChecks()
        {
            var failures = new List<string>();

            CheckFreshInstallDefaults(failures);
            CheckMidProgressMigration(failures);
            CheckFullyCompletedMigration(failures);
            CheckFirstRunShapedMigration(failures);
            CheckUnreadableFieldsAreRepaired(failures);
            CheckOutOfRangeValuesAreClamped(failures);
            CheckTestFunds(failures);
            CheckChallengeResumePoint(failures);

            foreach (string failure in failures)
            {
                Debug.LogError("[SaveSystemSelfCheck] FAILED: " + failure);
            }

            return failures.Count;
        }

        // The stage a challenge run reached has to survive being written out and read
        // back, because that is the whole point of it: the player quits the game at stage
        // thirty and comes back to stage thirty.
        private static void CheckChallengeResumePoint(List<string> failures)
        {
            var data = SaveData.CreateDefault();

            Check(failures, data.challengeStage == 0, "a fresh save should have no challenge run to resume");

            data.challengeStage = 30;

            var round = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            round.Sanitize();

            Check(failures, round.challengeStage == 30, "the challenge stage should survive a save and a load");

            // A file hand-edited to nonsense resumes at the beginning rather than at a
            // stage that does not exist.
            round.challengeStage = -5;
            round.Sanitize();

            Check(failures, round.challengeStage == 0, "a negative challenge stage should be repaired to none");
        }

        // What the demo money actually does, which is not what a grant would do.
        //
        // A demo has to start every session able to buy a car, and has to show the price
        // coming off when it does. That means a floor applied when a save is loaded and
        // nothing at all while one is being played - top it up as the player spends and
        // buying looks free, which is the opposite of what the demo is for.
        private static void CheckTestFunds(List<string> failures)
        {
            var data = SaveData.CreateDefault();
            data.coins = 0;

            bool toppedUp = TestFunds.TryTopUp(data);

            if (!TestFunds.Enabled)
            {
                Check(failures, !toppedUp, "test funds are off, so an empty save should stay empty");
                Check(failures, data.coins == 0, "test funds are off, so no coins should appear from nowhere");
                return;
            }

            Check(failures, toppedUp, "test funds are on, so an empty save should be topped up");
            Check(failures, data.coins == TestFunds.Coins, $"a topped-up save should hold {TestFunds.Coins} coins");

            // Spending has to stick for as long as the session lasts.
            data.coins -= 2500;
            Check(failures, data.coins == TestFunds.Coins - 2500, "buying something should take the money");
            Check(failures, TestFunds.TryTopUp(data), "the next load should put the demo money back");

            // And a save that is already richer is left alone rather than cut down to the
            // floor.
            var rich = SaveData.CreateDefault();
            rich.coins = TestFunds.Coins * 2;

            Check(failures, !TestFunds.TryTopUp(rich), "a save above the floor should not be touched");
            Check(failures, rich.coins == TestFunds.Coins * 2, "a save above the floor should keep its coins");
        }

        private static void CheckFreshInstallDefaults(List<string> failures)
        {
            SaveData data = SaveData.CreateDefault();

            Check(failures, data.saveVersion == SaveData.CurrentVersion, "a fresh save should carry the current save version");
            Check(failures, data.IsMissionUnlocked(1), "mission 1 should be unlocked on a fresh install");
            Check(failures, !data.IsMissionCompleted(1), "mission 1 should not be completed on a fresh install");
            Check(failures, IsOwned(data, 0), "car 0 should be owned on a fresh install");
            Check(failures, !IsOwned(data, 1) && !IsOwned(data, 2), "cars 1 and 2 should be locked on a fresh install");
            Check(failures, data.coins == 0, "a fresh install should start with 0 coins");
            Check(failures, data.currentMissionId == 1, "a fresh install should point at mission 1");
            Check(failures, !data.migratedFromPlayerPrefs, "a fresh install should not be flagged as migrated");
        }

        private static void CheckMidProgressMigration(List<string> failures)
        {
            // Legacy player finished missions 1 and 2 and is sitting on mission 3:
            // CurrentMission == 2, and the old code also flagged index 2 as "completed".
            SaveData data = Migrate(currentMissionIndex: 2, selectedCarIndex: 1, flaggedIndices: new[] { 0, 1, 2 });

            Check(failures, data.IsMissionCompleted(1), "legacy mission 1 should migrate as completed");
            Check(failures, data.IsMissionCompleted(2), "legacy mission 2 should migrate as completed");
            Check(failures, data.IsMissionUnlocked(3) && !data.IsMissionCompleted(3), "the mission in progress should migrate as unlocked but not completed");
            Check(failures, !IsUnlockedEntry(data, 4), "mission 4 should still be locked after migration");
            Check(failures, data.currentMissionId == 3, "migration should point at the mission the player was on");
            Check(failures, data.selectedCarIndex == 1, "migration should keep the selected car");
            Check(failures, IsOwned(data, 0) && IsOwned(data, 1) && IsOwned(data, 2), "a migrated player should keep all three legacy cars");
            Check(failures, data.migratedFromPlayerPrefs, "a migrated save should be flagged as migrated");
            Check(failures, data.FindMission(1).bestStars == 0 && !data.FindMission(1).HasTimeRecord, "legacy completions should carry no fabricated score or time record");
        }

        private static void CheckFullyCompletedMigration(List<string> failures)
        {
            // Legacy "All Missions Completed": CurrentMission ran past the array end.
            SaveData data = Migrate(currentMissionIndex: 8, selectedCarIndex: 2, flaggedIndices: new[] { 0, 1, 2, 3, 4, 5, 6, 7 });

            for (int missionId = 1; missionId <= 8; missionId++)
            {
                Check(failures, data.IsMissionCompleted(missionId), $"legacy mission {missionId} should migrate as completed for a finished save");
            }

            Check(failures, IsUnlockedEntry(data, 9) && !data.IsMissionCompleted(9), "mission 9 should be unlocked and unplayed after all legacy missions were finished");
            Check(failures, data.currentMissionId == 9, "a finished legacy save should point at the first new mission");
        }

        private static void CheckFirstRunShapedMigration(List<string> failures)
        {
            // Legacy save written by LoadProgress()'s first-run branch: nothing completed.
            SaveData data = Migrate(currentMissionIndex: 0, selectedCarIndex: 0, flaggedIndices: new[] { 0 });

            Check(failures, data.IsMissionUnlocked(1) && !data.IsMissionCompleted(1), "an untouched legacy save should migrate with mission 1 unlocked and unplayed");
            Check(failures, CountCompleted(data) == 0, "an untouched legacy save should not migrate any completions");
            Check(failures, data.currentMissionId == 1, "an untouched legacy save should point at mission 1");
        }

        private static void CheckUnreadableFieldsAreRepaired(List<string> failures)
        {
            var data = new SaveData
            {
                saveVersion = 0,
                coins = -500,
                currentMissionId = -3,
                selectedCarIndex = -1,
                cars = null,
                missions = null,
                settings = null
            };

            data.Sanitize();

            Check(failures, data.saveVersion == SaveData.CurrentVersion, "a save with no version should be repaired to the current version");
            Check(failures, data.coins == 0, "negative coins should be repaired to 0");
            Check(failures, data.currentMissionId == 1, "a negative current mission should be repaired to mission 1");
            Check(failures, data.selectedCarIndex == 0, "a negative selected car should be repaired to car 0");
            Check(failures, data.settings != null, "missing settings should be rebuilt");
            Check(failures, data.IsMissionUnlocked(1), "repair should leave mission 1 unlocked");
            Check(failures, IsOwned(data, 0), "repair should leave car 0 owned");
        }

        private static void CheckOutOfRangeValuesAreClamped(List<string> failures)
        {
            SaveData data = SaveData.CreateDefault();
            data.settings.musicVolume = 5f;
            data.settings.sfxVolume = -3f;
            data.settings.graphicsQuality = 77;
            data.settings.targetFrameRate = 144;
            data.settings.steeringSensitivity = 0f;
            data.missions.Add(new MissionProgressData(1));
            data.missions.Add(new MissionProgressData(5) { completed = true, unlocked = false, bestStars = 99, bestScore = -10 });

            data.Sanitize();

            Check(failures, Mathf.Approximately(data.settings.musicVolume, 1f), "music volume above range should clamp to 1");
            Check(failures, Mathf.Approximately(data.settings.sfxVolume, 0f), "sfx volume below range should clamp to 0");
            Check(failures, data.settings.graphicsQuality == SaveData.MaxGraphicsQuality, "graphics quality above range should clamp to the highest level");
            Check(failures, data.settings.targetFrameRate == 60, "an unsupported frame rate should snap to 60");
            Check(failures, Mathf.Approximately(data.settings.steeringSensitivity, 0.25f), "zero steering sensitivity should clamp to the minimum");
            Check(failures, CountMissionEntries(data, 1) == 1, "duplicate mission entries should be removed");

            MissionProgressData mission5 = data.FindMission(5);
            Check(failures, mission5 != null && mission5.unlocked, "a completed mission should never stay locked");
            Check(failures, mission5 != null && mission5.bestStars == SaveData.MaxStars, "stars above range should clamp to the maximum");
            Check(failures, mission5 != null && mission5.bestScore == 0, "a negative best score should clamp to 0");
        }

        private static SaveData Migrate(int currentMissionIndex, int selectedCarIndex, int[] flaggedIndices)
        {
            SaveData data = SaveData.CreateDefault();

            LegacyPlayerPrefsMigration.Apply(data, new LegacyProgress
            {
                currentMissionIndex = currentMissionIndex,
                selectedCarIndex = selectedCarIndex,
                missionFlags = BuildFlags(flaggedIndices)
            });

            return data;
        }

        private static bool[] BuildFlags(int[] flaggedIndices)
        {
            var flags = new bool[LegacyPlayerPrefsMigration.LegacyMissionCount];

            foreach (int index in flaggedIndices)
            {
                if (index >= 0 && index < flags.Length)
                {
                    flags[index] = true;
                }
            }

            return flags;
        }

        private static bool IsOwned(SaveData data, int carIndex)
        {
            CarSaveEntry car = data.FindCar(carIndex);
            return car != null && car.owned;
        }

        private static bool IsUnlockedEntry(SaveData data, int missionId)
        {
            MissionProgressData mission = data.FindMission(missionId);
            return mission != null && mission.unlocked;
        }

        private static int CountCompleted(SaveData data)
        {
            int count = 0;

            foreach (MissionProgressData mission in data.missions)
            {
                if (mission.completed)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountMissionEntries(SaveData data, int missionId)
        {
            int count = 0;

            foreach (MissionProgressData mission in data.missions)
            {
                if (mission.missionId == missionId)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Check(List<string> failures, bool condition, string description)
        {
            if (!condition)
            {
                failures.Add(description);
            }
        }
    }
}
