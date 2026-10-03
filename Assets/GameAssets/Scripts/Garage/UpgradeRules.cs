using UnityEngine;

namespace CarParkingGame.Garage
{
    public enum UpgradeKind
    {
        Engine,
        Brakes,
        Handling
    }

    // Upgrades are deliberately small. This is a parking simulator, and the vehicle
    // physics are WheelCollider-based and already tuned: multiplying torque, brake force
    // and steering sensitivity by a few percent is safe, while touching the WheelCollider
    // setup itself is not and is not attempted.
    [CreateAssetMenu(fileName = "UpgradeRules", menuName = "Car Parking/Upgrade Rules")]
    public class UpgradeRules : ScriptableObject
    {
        [SerializeField] private int maxLevel = 3;
        [SerializeField] private int firstLevelCost = 1500;
        [SerializeField] private int costIncreasePerLevel = 1000;
        [SerializeField, Range(0f, 0.15f)] private float engineGainPerLevel = 0.05f;
        [SerializeField, Range(0f, 0.15f)] private float brakeGainPerLevel = 0.05f;
        [SerializeField, Range(0f, 0.15f)] private float handlingGainPerLevel = 0.04f;

        public int MaxLevel => maxLevel;

        public static UpgradeRules CreateDefault()
        {
            return CreateInstance<UpgradeRules>();
        }

        // Returns 0 when the part is already at its highest level.
        public int CostForNextLevel(int currentLevel)
        {
            if (currentLevel >= maxLevel)
            {
                return 0;
            }

            return firstLevelCost + costIncreasePerLevel * Mathf.Max(0, currentLevel);
        }

        public float MultiplierFor(UpgradeKind kind, int level)
        {
            level = Mathf.Clamp(level, 0, maxLevel);

            switch (kind)
            {
                case UpgradeKind.Engine:
                    return 1f + engineGainPerLevel * level;
                case UpgradeKind.Brakes:
                    return 1f + brakeGainPerLevel * level;
                case UpgradeKind.Handling:
                    return 1f + handlingGainPerLevel * level;
                default:
                    return 1f;
            }
        }
    }
}
