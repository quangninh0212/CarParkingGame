using CarParkingGame.Parking;
using CarParkingGame.Progression;
using UnityEngine;

namespace CarParkingGame.OpenWorld
{
    // Tuning for one open-world parking challenge. Kept separate from MissionDefinition
    // rather than sharing a base type: challenges are identified by a stable string id,
    // are not part of the practice ladder, and pay on a different scale.
    [CreateAssetMenu(fileName = "Challenge", menuName = "Car Parking/Open World Challenge")]
    public class ChallengeDefinition : ScriptableObject
    {
        [SerializeField] private string challengeId = "challenge";
        [SerializeField] private string displayName = "Parking Challenge";
        [SerializeField, Range(1, 5)] private int difficulty = 3;

        [Tooltip("Coins paid for a three-star clear; fewer stars pay pro rata.")]
        [SerializeField] private int coinReward = 500;

        [SerializeField] private ParkingType parkingType = ParkingType.Forward;

        [Tooltip("0 means the challenge is untimed.")]
        [SerializeField] private float timeLimitSeconds;

        [SerializeField] private float maxParkingSpeedKmh = 2f;
        [SerializeField] private float angleToleranceDegrees = 15f;
        [SerializeField] private float holdSeconds = 1.5f;
        [SerializeField, Range(0.25f, 1f)] private float requiredContainment = 1f;
        [SerializeField] private bool allowOppositeHeading = true;
        [SerializeField] private ScoreRules scoreRules;

        public string ChallengeId => challengeId;
        public string DisplayName => displayName;
        public int Difficulty => difficulty;
        public int CoinReward => coinReward;
        public ParkingType ParkingType => parkingType;
        public float TimeLimitSeconds => timeLimitSeconds;
        public bool IsTimed => timeLimitSeconds > 0f;
        public float MaxParkingSpeedKmh => maxParkingSpeedKmh;
        public float AngleToleranceDegrees => angleToleranceDegrees;
        public float HoldSeconds => holdSeconds;
        public float RequiredContainment => requiredContainment;
        public bool AllowOppositeHeading => allowOppositeHeading;
        public ScoreRules ScoreRules => scoreRules;
    }
}
