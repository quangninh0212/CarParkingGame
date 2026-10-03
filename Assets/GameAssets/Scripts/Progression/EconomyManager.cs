using System;
using CarParkingGame.Core;
using UnityEngine;

namespace CarParkingGame.Progression
{
    public static class EconomyManager
    {
        public const int CoinsPerStar = 100;
        public const float RepeatRewardMultiplier = 0.25f;
        public const int MinimumRepeatReward = 10;

        public static event Action<int> CoinsChanged;

        public static int Coins => SaveManager.Data.coins;

        public static int BaseRewardFor(int stars, int missionReward)
        {
            stars = Mathf.Clamp(stars, 0, 3);

            if (stars <= 0)
            {
                return 0;
            }

            // A mission's own reward is the three-star payout; fewer stars pay pro rata.
            if (missionReward > 0)
            {
                return Mathf.RoundToInt(missionReward * (stars / 3f));
            }

            return stars * CoinsPerStar;
        }

        // Anti-farming: a first clear or a genuine improvement pays in full, replays pay
        // a fraction. Rewards are granted once, when the mission ends - never from the
        // results screen, so reopening or reloading that screen cannot pay again.
        public static int CalculateReward(int stars, int missionReward, bool isFirstCompletion, bool improved)
        {
            int baseReward = BaseRewardFor(stars, missionReward);

            if (baseReward <= 0)
            {
                return 0;
            }

            if (isFirstCompletion || improved)
            {
                return baseReward;
            }

            return Mathf.Max(MinimumRepeatReward, Mathf.RoundToInt(baseReward * RepeatRewardMultiplier));
        }

        public static void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SaveData data = SaveManager.Data;
            data.coins = Mathf.Max(0, data.coins + amount);
            SaveManager.Save();
            CoinsChanged?.Invoke(data.coins);
        }

        public static bool TrySpend(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            SaveData data = SaveManager.Data;

            if (data.coins < amount)
            {
                return false;
            }

            data.coins -= amount;
            SaveManager.Save();
            CoinsChanged?.Invoke(data.coins);
            return true;
        }
    }
}
