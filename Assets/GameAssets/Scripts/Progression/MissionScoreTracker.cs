using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Progression
{
    // Plain class driven by MissionManager rather than a MonoBehaviour: the mission
    // clock is passed in, so the whole scoring model can be exercised headlessly.
    public class MissionScoreTracker
    {
        private readonly ScoreRules rules;
        private readonly Dictionary<int, float> lastContactTimes = new Dictionary<int, float>();

        private float elapsed;
        private int score;
        private int collisions;

        public MissionScoreTracker(ScoreRules scoreRules)
        {
            rules = scoreRules != null ? scoreRules : ScoreRules.CreateDefault();
            Reset();
        }

        public event Action<int> ScoreChanged;

        public ScoreRules Rules => rules;
        public int Score => score;
        public int Collisions => collisions;
        public float ElapsedSeconds => elapsed;
        public int Stars => rules.StarsFor(score);
        public bool IsFailing => rules.IsFailingScore(score);

        public void Reset()
        {
            score = rules.StartingScore;
            elapsed = 0f;
            collisions = 0;
            lastContactTimes.Clear();
        }

        public void Tick(float deltaTime)
        {
            elapsed += Mathf.Max(0f, deltaTime);
        }

        // Physics reports the same cone repeatedly while a car scrapes along it, so
        // each individual collider is only allowed to cost the player once per cooldown.
        public bool RegisterCollision(string colliderTag, float impulse, int colliderId)
        {
            if (lastContactTimes.TryGetValue(colliderId, out float lastTime)
                && elapsed - lastTime < rules.CollisionCooldownSeconds)
            {
                return false;
            }

            lastContactTimes[colliderId] = elapsed;
            collisions++;

            ApplyPenalty(rules.ScaleByImpulse(rules.PenaltyForTag(colliderTag), impulse));
            return true;
        }

        public void ApplyPenalty(int points)
        {
            if (points <= 0)
            {
                return;
            }

            score = Mathf.Max(0, score - points);
            ScoreChanged?.Invoke(score);
        }

        public int ApplyOvertimePenalty(float timeLimitSeconds)
        {
            if (timeLimitSeconds <= 0f || elapsed <= timeLimitSeconds)
            {
                return 0;
            }

            int secondsOver = Mathf.CeilToInt(elapsed - timeLimitSeconds);
            int penalty = Mathf.Min(secondsOver * rules.OvertimePenaltyPerSecond, rules.MaxOvertimePenalty);

            ApplyPenalty(penalty);
            return penalty;
        }
    }
}
