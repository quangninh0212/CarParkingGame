using UnityEngine;

namespace CarParkingGame.Progression
{
    // Snapshot of the pre-upgrade PlayerPrefs progression, read once so the
    // conversion itself stays a pure function that can be validated headlessly.
    public struct LegacyProgress
    {
        public int currentMissionIndex;
        public int selectedCarIndex;
        public bool[] missionFlags;
    }

    // Converts the old PlayerPrefs progression into SaveData.
    //
    // The legacy GameManager.CompleteMission() did:
    //     missionCompleted[current] = true;  current++;  missionCompleted[current] = true;
    // so its bool[] cannot distinguish "completed" from "merely unlocked" - the
    // highest flagged entry is usually a mission the player never finished.
    // "CurrentMission" is the trustworthy marker: it only ever advances when a
    // mission is actually completed, so every index below it is a real completion
    // and the index itself is the next mission to play.
    //
    // The legacy keys are read but never deleted: a downgrade to the old build
    // still finds its data, and nothing is erased if migration is ever wrong.
    public static class LegacyPlayerPrefsMigration
    {
        public const int LegacyMissionCount = 8;
        public const int LegacyCarCount = 3;
        public const string CurrentMissionKey = "CurrentMission";
        public const string SelectedCarIndexKey = "SelectedCarIndex";

        public static string MissionCompletedKey(int legacyIndex)
        {
            return "Mission" + legacyIndex + "Completed";
        }

        // Legacy missions were 0-based array indices; new mission ids are 1-based
        // so they read the same as the "Mission 01 .. Mission 30" design list.
        public static int ToMissionId(int legacyIndex)
        {
            return legacyIndex + 1;
        }

        public static bool HasLegacyData()
        {
            if (PlayerPrefs.HasKey(CurrentMissionKey) || PlayerPrefs.HasKey(SelectedCarIndexKey))
            {
                return true;
            }

            for (int i = 0; i < LegacyMissionCount; i++)
            {
                if (PlayerPrefs.HasKey(MissionCompletedKey(i)))
                {
                    return true;
                }
            }

            return false;
        }

        public static LegacyProgress Read()
        {
            var flags = new bool[LegacyMissionCount];

            for (int i = 0; i < LegacyMissionCount; i++)
            {
                flags[i] = PlayerPrefs.GetInt(MissionCompletedKey(i), 0) == 1;
            }

            return new LegacyProgress
            {
                currentMissionIndex = PlayerPrefs.GetInt(CurrentMissionKey, 0),
                selectedCarIndex = PlayerPrefs.GetInt(SelectedCarIndexKey, 0),
                missionFlags = flags
            };
        }

        public static void Apply(SaveData data, LegacyProgress legacy)
        {
            int flagCount = legacy.missionFlags == null ? 0 : legacy.missionFlags.Length;
            int completedThrough = Mathf.Clamp(legacy.currentMissionIndex, 0, LegacyMissionCount);

            for (int legacyIndex = 0; legacyIndex < LegacyMissionCount; legacyIndex++)
            {
                bool flagged = legacyIndex < flagCount && legacy.missionFlags[legacyIndex];
                bool completed = legacyIndex < completedThrough;

                if (!flagged && !completed)
                {
                    continue;
                }

                MissionProgressData mission = data.GetOrCreateMission(ToMissionId(legacyIndex));
                mission.unlocked = true;
                mission.completed = completed;
            }

            // The mission after the last completed one is the one to play next. When
            // all 8 legacy missions were finished this unlocks mission 9, which the
            // Phase 9 content will fill in - an id with no definition yet is inert.
            data.GetOrCreateMission(ToMissionId(completedThrough)).unlocked = true;
            data.currentMissionId = ToMissionId(completedThrough);

            // The old build let the player drive all three cars for free, so a
            // migrated player keeps them. Fresh installs start with car 0 only and
            // buy the rest (Phase 12).
            for (int carIndex = 0; carIndex < LegacyCarCount; carIndex++)
            {
                data.GetOrCreateCar(carIndex).owned = true;
            }

            data.selectedCarIndex = Mathf.Clamp(legacy.selectedCarIndex, 0, LegacyCarCount - 1);
            data.migratedFromPlayerPrefs = true;

            // Legacy saves carry no score/star/time history; those stay at their
            // "no record" defaults and fill in the first time a mission is replayed.
            data.Sanitize();
        }
    }
}
