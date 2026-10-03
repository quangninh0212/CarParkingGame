using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Progression
{
    [Serializable]
    public class CollisionPenalty
    {
        public string colliderTag = "Cone";
        public int points = 3;
    }

    // Scoring configuration. Tag-driven rather than hardcoded, because the project's
    // obstacle assets only define a "Cone" tag today and more can be added without
    // touching code.
    [CreateAssetMenu(fileName = "ScoreRules", menuName = "Car Parking/Score Rules")]
    public class ScoreRules : ScriptableObject
    {
        [SerializeField] private int startingScore = 100;

        [SerializeField]
        private List<CollisionPenalty> collisionPenalties = new List<CollisionPenalty>
        {
            new CollisionPenalty { colliderTag = "Cone", points = 3 },
            new CollisionPenalty { colliderTag = "Barrier", points = 5 },
            new CollisionPenalty { colliderTag = "Vehicle", points = 10 }
        };

        [SerializeField] private int defaultCollisionPenalty = 5;
        [SerializeField] private int leftMissionAreaPenalty = 5;
        [SerializeField] private int misalignedParkingPenalty = 5;
        [SerializeField] private int overtimePenaltyPerSecond = 1;
        [SerializeField] private int maxOvertimePenalty = 20;
        [SerializeField] private float collisionCooldownSeconds = 0.75f;
        [SerializeField] private float lightContactImpulse = 2f;
        [SerializeField] private float heavyContactImpulse = 8f;
        [SerializeField] private int threeStarScore = 90;
        [SerializeField] private int twoStarScore = 70;
        [SerializeField] private int oneStarScore = 50;

        public int StartingScore => startingScore;
        public int DefaultCollisionPenalty => defaultCollisionPenalty;
        public int LeftMissionAreaPenalty => leftMissionAreaPenalty;
        public int MisalignedParkingPenalty => misalignedParkingPenalty;
        public int OvertimePenaltyPerSecond => overtimePenaltyPerSecond;
        public int MaxOvertimePenalty => maxOvertimePenalty;
        public float CollisionCooldownSeconds => collisionCooldownSeconds;
        public int OneStarScore => oneStarScore;

        public static ScoreRules CreateDefault()
        {
            return CreateInstance<ScoreRules>();
        }

        public int PenaltyForTag(string colliderTag)
        {
            if (!string.IsNullOrEmpty(colliderTag) && collisionPenalties != null)
            {
                for (int i = 0; i < collisionPenalties.Count; i++)
                {
                    if (collisionPenalties[i] != null && collisionPenalties[i].colliderTag == colliderTag)
                    {
                        return collisionPenalties[i].points;
                    }
                }
            }

            return defaultCollisionPenalty;
        }

        // A brush against a cone should not cost the same as ramming a barrier, so the
        // collision impulse scales the penalty instead of every contact costing the same.
        public int ScaleByImpulse(int basePoints, float impulse)
        {
            if (basePoints <= 0)
            {
                return 0;
            }

            if (impulse <= lightContactImpulse)
            {
                return Mathf.Max(1, Mathf.RoundToInt(basePoints * 0.5f));
            }

            if (impulse >= heavyContactImpulse)
            {
                return basePoints * 2;
            }

            return basePoints;
        }

        public int StarsFor(int score)
        {
            if (score >= threeStarScore)
            {
                return 3;
            }

            if (score >= twoStarScore)
            {
                return 2;
            }

            return score >= oneStarScore ? 1 : 0;
        }

        public bool IsFailingScore(int score)
        {
            return score < oneStarScore;
        }
    }
}
