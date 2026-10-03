using System;
using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using CarParkingGame.Progression;
using CarParkingGame.Vehicle;
using UnityEngine;

namespace CarParkingGame.OpenWorld
{
    public class ChallengeResult
    {
        public string challengeId;
        public bool success;
        public MissionFailReason failReason = MissionFailReason.None;
        public int score;
        public int stars;
        public float timeSeconds;
        public int collisions;
        public int coinsAwarded;
        public bool isNewBest;
        public bool isFirstCompletion;
    }

    // Free driving with challenges scattered around the map. Starting and finishing a
    // challenge never reloads the scene: the car stays where it is, the validator is
    // configured for that bay, and afterwards the player simply drives away.
    public class OpenWorldChallengeManager : MonoBehaviour
    {
        public static OpenWorldChallengeManager Instance { get; private set; }

        [SerializeField] private ScoreRules defaultScoreRules;
        [SerializeField] private float scanInterval = 0.25f;

        private readonly List<ChallengeMarker> markers = new List<ChallengeMarker>();

        private ChallengeMarker markerInRange;
        private ChallengeMarker activeMarker;
        private ParkingValidator validator;
        private VehicleCollisionReporter reporter;
        private MissionScoreTracker tracker;
        private CarController vehicle;
        private float nextScanTime;
        private bool running;

        public event Action<ChallengeMarker> MarkerInRangeChanged;
        public event Action<ChallengeDefinition> ChallengeStarted;
        public event Action<ChallengeResult> ChallengeEnded;
        public event Action<int> ScoreChanged;
        public event Action<float> ParkingProgressChanged;

        public bool IsRunning => running;
        public ChallengeMarker MarkerInRange => markerInRange;
        public ChallengeDefinition ActiveChallenge => activeMarker != null ? activeMarker.Definition : null;
        public IReadOnlyList<ChallengeMarker> Markers => markers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            RebuildMarkers();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void RebuildMarkers()
        {
            markers.Clear();

            ChallengeMarker[] found = FindObjectsByType<ChallengeMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ChallengeMarker marker in found)
            {
                if (!marker.IsConfigured)
                {
                    Debug.LogWarning($"[OpenWorldChallengeManager] '{marker.name}' has no definition or parking zone and was skipped.", marker);
                    continue;
                }

                markers.Add(marker);
            }
        }

        private void Update()
        {
            if (running)
            {
                TickActiveChallenge();
                return;
            }

            if (Time.time < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.time + scanInterval;
            ScanForNearbyMarker();
        }

        public bool StartNearbyChallenge()
        {
            return markerInRange != null && StartChallenge(markerInRange);
        }

        public bool StartChallenge(ChallengeMarker marker)
        {
            if (running || marker == null || !marker.IsConfigured)
            {
                return false;
            }

            vehicle = ActiveVehicleLocator.Current;

            if (vehicle == null)
            {
                Debug.LogError("[OpenWorldChallengeManager] No active car to run a challenge with.");
                return false;
            }

            activeMarker = marker;
            ChallengeDefinition definition = marker.Definition;

            tracker = new MissionScoreTracker(definition.ScoreRules != null ? definition.ScoreRules : defaultScoreRules);
            tracker.ScoreChanged += OnScoreChanged;

            validator = marker.ParkingZone.GetComponent<ParkingValidator>();

            if (validator == null)
            {
                validator = marker.ParkingZone.gameObject.AddComponent<ParkingValidator>();
            }

            validator.Zone = marker.ParkingZone;
            validator.Configure(
                definition.ParkingType,
                definition.MaxParkingSpeedKmh,
                definition.AngleToleranceDegrees,
                definition.HoldSeconds,
                definition.RequiredContainment,
                definition.AllowOppositeHeading);

            validator.SetVehicle(vehicle.transform);
            validator.ProgressChanged += OnParkingProgressChanged;
            validator.Validated += OnParkingValidated;
            validator.Begin();

            reporter = vehicle.GetComponent<VehicleCollisionReporter>();

            if (reporter == null)
            {
                reporter = vehicle.gameObject.AddComponent<VehicleCollisionReporter>();
            }

            reporter.Collided += OnVehicleCollided;

            marker.SetIconVisible(false);
            running = true;

            GameFlowManager.Instance?.SetGameMode(GameMode.OpenWorld);
            GameFlowManager.Instance?.SetGameState(GameState.Playing);

            ChallengeStarted?.Invoke(definition);
            return true;
        }

        // The player is allowed to walk away from a challenge at any time; this is free
        // driving, so cancelling is not a failure worth recording.
        public void CancelChallenge()
        {
            if (!running)
            {
                return;
            }

            Finish(false, MissionFailReason.Aborted);
        }

        private void TickActiveChallenge()
        {
            tracker.Tick(Time.deltaTime);

            ChallengeDefinition definition = activeMarker.Definition;

            if (definition.IsTimed && tracker.ElapsedSeconds > definition.TimeLimitSeconds)
            {
                Finish(false, MissionFailReason.TimeExpired);
            }
        }

        private void ScanForNearbyMarker()
        {
            CarController car = ActiveVehicleLocator.Current;

            if (car == null)
            {
                SetMarkerInRange(null);
                return;
            }

            Vector3 position = car.transform.position;
            ChallengeMarker closest = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < markers.Count; i++)
            {
                ChallengeMarker marker = markers[i];
                float distance = marker.DistanceTo(position);

                if (distance <= marker.ActivationRadius && distance < closestDistance)
                {
                    closest = marker;
                    closestDistance = distance;
                }
            }

            SetMarkerInRange(closest);
        }

        private void SetMarkerInRange(ChallengeMarker marker)
        {
            if (markerInRange == marker)
            {
                return;
            }

            markerInRange = marker;
            MarkerInRangeChanged?.Invoke(markerInRange);
        }

        private void OnScoreChanged(int score)
        {
            ScoreChanged?.Invoke(score);
        }

        private void OnParkingProgressChanged(float progress)
        {
            ParkingProgressChanged?.Invoke(progress);
        }

        private void OnParkingValidated()
        {
            if (running)
            {
                Finish(true, MissionFailReason.None);
            }
        }

        private void OnVehicleCollided(VehicleCollisionInfo info)
        {
            if (running)
            {
                tracker.RegisterCollision(info.tag, info.impulse, info.colliderId);
            }
        }

        private void Finish(bool parked, MissionFailReason failReason)
        {
            ChallengeDefinition definition = activeMarker.Definition;

            if (parked)
            {
                tracker.ApplyOvertimePenalty(definition.TimeLimitSeconds);
            }

            bool success = parked && !tracker.IsFailing;

            var result = new ChallengeResult
            {
                challengeId = definition.ChallengeId,
                success = success,
                failReason = success ? MissionFailReason.None : (parked ? MissionFailReason.ScoreTooLow : failReason),
                score = tracker.Score,
                stars = success ? tracker.Stars : 0,
                timeSeconds = tracker.ElapsedSeconds,
                collisions = tracker.Collisions
            };

            if (success)
            {
                RecordSuccess(definition, result);
            }

            Cleanup();
            ChallengeEnded?.Invoke(result);
        }

        private void RecordSuccess(ChallengeDefinition definition, ChallengeResult result)
        {
            SaveData data = SaveManager.Data;
            ChallengeSaveEntry entry = data.GetOrCreateChallenge(definition.ChallengeId);

            result.isFirstCompletion = !entry.completed;
            result.isNewBest = result.score > entry.bestScore || result.stars > entry.bestStars;

            entry.completed = true;
            entry.bestScore = Mathf.Max(entry.bestScore, result.score);
            entry.bestStars = Mathf.Max(entry.bestStars, result.stars);

            result.coinsAwarded = EconomyManager.CalculateReward(
                result.stars,
                definition.CoinReward,
                result.isFirstCompletion,
                result.isNewBest);

            if (result.coinsAwarded > 0)
            {
                EconomyManager.Add(result.coinsAwarded);
            }
            else
            {
                SaveManager.Save();
            }
        }

        // Hands the car back to the player rather than reloading anything.
        private void Cleanup()
        {
            if (validator != null)
            {
                validator.ProgressChanged -= OnParkingProgressChanged;
                validator.Validated -= OnParkingValidated;
                validator.Stop();
                validator = null;
            }

            if (reporter != null)
            {
                reporter.Collided -= OnVehicleCollided;
                reporter = null;
            }

            if (tracker != null)
            {
                tracker.ScoreChanged -= OnScoreChanged;
            }

            activeMarker?.SetIconVisible(true);
            activeMarker = null;
            running = false;
            nextScanTime = 0f;

            vehicle?.SetVehicleEnabled(true);
        }
    }
}
