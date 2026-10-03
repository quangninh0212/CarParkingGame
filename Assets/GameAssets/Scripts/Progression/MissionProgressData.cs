using System;

namespace CarParkingGame.Progression
{
    // Per-mission player record. Deliberately separates "unlocked" from "completed",
    // which the legacy bool[] missionCompleted conflated (see Docs/UPGRADE_PLAN.md).
    [Serializable]
    public class MissionProgressData
    {
        public const float NoTime = -1f;
        public const int NoCollisionRecord = -1;

        public int missionId;
        public bool unlocked;
        public bool completed;
        public int bestScore;
        public int bestStars;
        public float bestTimeSeconds = NoTime;
        public int bestCollisions = NoCollisionRecord;

        public MissionProgressData()
        {
        }

        public MissionProgressData(int missionId)
        {
            this.missionId = missionId;
        }

        public bool HasTimeRecord => bestTimeSeconds > 0f;

        // Returns true when this attempt beat the stored record, so callers can tell
        // a personal best from a replay (used by the economy's anti-farming rules).
        public bool SubmitResult(int score, int stars, float timeSeconds, int collisions)
        {
            bool improved = false;

            if (score > bestScore)
            {
                bestScore = score;
                improved = true;
            }

            if (stars > bestStars)
            {
                bestStars = stars;
                improved = true;
            }

            if (timeSeconds > 0f && (!HasTimeRecord || timeSeconds < bestTimeSeconds))
            {
                bestTimeSeconds = timeSeconds;
                improved = true;
            }

            if (collisions >= 0 && (bestCollisions < 0 || collisions < bestCollisions))
            {
                bestCollisions = collisions;
                improved = true;
            }

            completed = true;
            return improved;
        }
    }
}
