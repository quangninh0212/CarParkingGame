using CarParkingGame.Parking;
using CarParkingGame.Progression;
using UnityEngine;

namespace CarParkingGame.Missions
{
    // Static, per-mission configuration. Scene references (start point, parking zone,
    // environment) cannot live in a ScriptableObject, so they sit on MissionAuthoring
    // in the scene and are matched to this definition by mission id.
    [CreateAssetMenu(fileName = "Mission", menuName = "Car Parking/Mission Definition")]
    public class MissionDefinition : ScriptableObject
    {
        [SerializeField] private int missionId = 1;
        [SerializeField] private string displayName = "New Mission";
        [SerializeField] private string description = string.Empty;
        [SerializeField, Range(1, 5)] private int difficulty = 1;
        [SerializeField] private ParkingType parkingType = ParkingType.Forward;

        [Tooltip("0 means the mission is untimed.")]
        [SerializeField] private float timeLimitSeconds;

        [SerializeField] private bool failOnTimeout = true;
        [SerializeField] private float maxParkingSpeedKmh = 2f;
        [SerializeField] private float angleToleranceDegrees = 15f;
        [SerializeField] private float holdSeconds = 1.5f;

        [Tooltip("Fraction of the car's footprint that must sit inside the bay. 1 = fully inside.")]
        [SerializeField, Range(0.25f, 1f)] private float requiredContainment = 1f;

        [Tooltip("When on, a car parked facing the opposite way still counts as aligned.")]
        [SerializeField] private bool allowOppositeHeading = true;

        [Tooltip("Coins paid for a three-star clear; fewer stars pay pro rata.")]
        [SerializeField] private int coinReward = 300;

        [SerializeField] private ScoreRules scoreRules;

        public int MissionId => missionId;
        public string DisplayName => displayName;
        public string Description => description;
        public int Difficulty => difficulty;
        public ParkingType ParkingType => parkingType;
        public float TimeLimitSeconds => timeLimitSeconds;
        public bool IsTimed => timeLimitSeconds > 0f;
        public bool FailOnTimeout => failOnTimeout;
        public float MaxParkingSpeedKmh => maxParkingSpeedKmh;
        public float AngleToleranceDegrees => angleToleranceDegrees;
        public float HoldSeconds => holdSeconds;
        public float RequiredContainment => requiredContainment;
        public bool AllowOppositeHeading => allowOppositeHeading;
        public int CoinReward => coinReward;
        public ScoreRules ScoreRules => scoreRules;

#if UNITY_EDITOR
        // Used by the mission content generator so the runtime API needs no setters.
        public void EditorConfigure(
            int id,
            string name,
            string missionDescription,
            int missionDifficulty,
            ParkingType type,
            float timeLimit,
            bool failWhenOutOfTime,
            float maxSpeedKmh,
            float angleTolerance,
            float hold,
            float containment,
            bool allowOpposite,
            int reward,
            ScoreRules rules)
        {
            missionId = id;
            displayName = name;
            description = missionDescription;
            difficulty = missionDifficulty;
            parkingType = type;
            timeLimitSeconds = timeLimit;
            failOnTimeout = failWhenOutOfTime;
            maxParkingSpeedKmh = maxSpeedKmh;
            angleToleranceDegrees = angleTolerance;
            holdSeconds = hold;
            requiredContainment = containment;
            allowOppositeHeading = allowOpposite;
            coinReward = reward;
            scoreRules = rules;
        }
#endif
    }
}
