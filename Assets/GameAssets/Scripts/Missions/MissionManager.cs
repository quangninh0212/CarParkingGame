using System;
using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Parking;
using CarParkingGame.Progression;
using CarParkingGame.Vehicle;
using UnityEngine;

namespace CarParkingGame.Missions
{
    // Runs one practice mission: spawns the car, configures the parking validator from
    // the mission's definition, tracks score and time, then writes the result to the
    // save file and pays out coins exactly once.
    //
    // Not wired into the gameplay scene yet - the legacy GameManager still owns mission
    // flow. See Docs/SETUP_CHECKLIST.md for the handover steps.
    public class MissionManager : MonoBehaviour
    {
        public static MissionManager Instance { get; private set; }

        [SerializeField] private ScoreRules defaultScoreRules;

        [Tooltip("Hide every other mission's environment container while a mission runs.")]
        [SerializeField] private bool isolateActiveEnvironment = true;

        [Tooltip("Which mission the Inspector's context-menu test entry starts. Only used in the Editor.")]
        [SerializeField] private int debugMissionId = 1;

        private readonly Dictionary<int, MissionAuthoring> missionsById = new Dictionary<int, MissionAuthoring>();
        private readonly List<MissionAuthoring> registeredMissions = new List<MissionAuthoring>();

        private MissionAuthoring activeMission;
        private MissionScoreTracker tracker;
        private ParkingValidator validator;
        private VehicleCollisionReporter reporter;
        private CarController vehicle;
        private ParkingTrigger[] legacyTriggers;
        private MissionFailedHandler[] legacyFailHandlers;
        private bool running;

        public event Action<MissionDefinition> MissionStarted;
        public event Action<MissionResult> MissionEnded;
        public event Action<int> ScoreChanged;
        public event Action<float> ParkingProgressChanged;
        public event Action<ParkingState> ParkingStateChanged;

        public bool IsRunning => running;
        public MissionDefinition ActiveMission => activeMission != null ? activeMission.Definition : null;
        public MissionScoreTracker Tracker => tracker;
        public IReadOnlyList<MissionAuthoring> RegisteredMissions => registeredMissions;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            RebuildRegistry();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

#if UNITY_EDITOR
        // Lets a mission be started from the component's context menu while playing, so the
        // new parking/scoring pipeline can be tested before any selection UI exists.
        [ContextMenu("Debug: Start Mission")]
        private void DebugStartMission()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[MissionManager] Enter Play mode first, then use this again.");
                return;
            }

            RebuildRegistry();
            StartMission(debugMissionId);
        }

        // Drops the car dead centre in the bay, facing the parked heading, at rest, so the
        // whole mission -> result -> reward -> next mission loop can be exercised on a
        // laptop without having to drive. It proves the pipeline, not the bay placement:
        // the car is put exactly where the bay is, so it cannot catch a misplaced bay.
        [ContextMenu("Debug: Snap Car Into Bay (P)")]
        private void DebugSnapIntoBay()
        {
            if (!running || activeMission == null || vehicle == null)
            {
                Debug.LogWarning("[MissionManager] Start a mission first, then press P.");
                return;
            }

            ParkingZone zone = activeMission.ParkingZone;

            Vector3 heading = zone.ParkedHeading;
            heading.y = 0f;

            Vector3 position = zone.WorldCenter;
            position.y = vehicle.transform.position.y;

            var body = vehicle.GetComponent<Rigidbody>();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            vehicle.transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading.normalized, Vector3.up));

            if (activeMission.Definition.ParkingType == ParkingType.Reverse && validator != null)
            {
                validator.EditorMarkReversedIn();
            }

            Debug.Log($"[MissionManager] Snapped the car into mission {activeMission.MissionId}'s bay. It should complete after the {activeMission.Definition.HoldSeconds:0.0}s hold.");
        }

        // Checked on both input backends, for the same reason as VehicleInput.ReadKeyboard.
        private static bool SnapKeyPressed()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.pKey.wasPressedThisFrame;
#else
            return false;
#endif
        }

        [ContextMenu("Debug: Log Registered Missions")]
        private void DebugLogRegisteredMissions()
        {
            RebuildRegistry();
            Debug.Log($"[MissionManager] {registeredMissions.Count} mission(s) set up in this scene.");

            foreach (MissionAuthoring mission in registeredMissions)
            {
                Debug.Log($"  Mission {mission.MissionId}: '{mission.Definition.DisplayName}' ({mission.Definition.ParkingType}) on '{mission.name}'", mission);
            }
        }
#endif

        public void RebuildRegistry()
        {
            missionsById.Clear();
            registeredMissions.Clear();

            MissionAuthoring[] found = FindObjectsByType<MissionAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MissionAuthoring mission in found)
            {
                int id = mission.MissionId;

                if (id <= 0)
                {
                    Debug.LogWarning($"[MissionManager] '{mission.name}' has no usable mission id and was skipped.", mission);
                    continue;
                }

                if (missionsById.ContainsKey(id))
                {
                    Debug.LogError($"[MissionManager] Duplicate mission id {id} on '{mission.name}'; keeping the first one found.", mission);
                    continue;
                }

                missionsById.Add(id, mission);
                registeredMissions.Add(mission);
            }

            registeredMissions.Sort((a, b) => a.MissionId.CompareTo(b.MissionId));
        }

        public MissionAuthoring GetMission(int missionId)
        {
            return missionsById.TryGetValue(missionId, out MissionAuthoring mission) ? mission : null;
        }

        public bool StartMission(int missionId)
        {
            MissionAuthoring mission = GetMission(missionId);

            if (mission == null)
            {
                Debug.LogError($"[MissionManager] No mission is set up for id {missionId}.");
                return false;
            }

            if (mission.Definition == null || mission.StartPoint == null || mission.ParkingZone == null)
            {
                Debug.LogError($"[MissionManager] Mission {missionId} ('{mission.name}') is missing a definition, start point or parking zone.", mission);
                return false;
            }

            vehicle = ResolveActiveVehicle();

            if (vehicle == null)
            {
                Debug.LogError("[MissionManager] No active CarController found to drive this mission.");
                return false;
            }

            StopActiveMission();
            activeMission = mission;

            if (isolateActiveEnvironment)
            {
                for (int i = 0; i < registeredMissions.Count; i++)
                {
                    registeredMissions[i].SetEnvironmentActive(registeredMissions[i] == mission);
                }
            }
            else
            {
                mission.SetEnvironmentActive(true);
            }

            MissionDefinition definition = mission.Definition;

            PlaceVehicleAtStart(mission.StartPoint);
            SetUpTracker(definition);
            SetUpValidator(mission, definition);
            SetUpCollisionReporting();

            // The legacy trigger would instantly complete the mission on contact and the
            // legacy cone handler would instantly fail it, both bypassing the new
            // validation and scoring. They are switched off for the duration and restored
            // afterwards, so the old menu flow still works as before.
            SetLegacyHandlersEnabled(false);

            running = true;
            vehicle.SetVehicleEnabled(true);

            GameFlowManager.Instance?.SetGameMode(GameMode.Practice);
            GameFlowManager.Instance?.SetGameState(GameState.Playing);

            MissionStarted?.Invoke(definition);
            return true;
        }

        public void AbortMission()
        {
            if (!running)
            {
                return;
            }

            Finish(false, MissionFailReason.Aborted);
        }

        private void Update()
        {
            if (!running || activeMission == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (SnapKeyPressed())
            {
                DebugSnapIntoBay();
            }
#endif

            tracker.Tick(Time.deltaTime);

            MissionDefinition definition = activeMission.Definition;

            if (definition.IsTimed && definition.FailOnTimeout && tracker.ElapsedSeconds > definition.TimeLimitSeconds)
            {
                Finish(false, MissionFailReason.TimeExpired);
                return;
            }

            if (vehicle != null && !activeMission.IsInsideAllowedArea(vehicle.transform.position))
            {
                tracker.ApplyPenalty(tracker.Rules.LeftMissionAreaPenalty);
                Finish(false, MissionFailReason.LeftMissionArea);
            }
        }

        private void SetUpTracker(MissionDefinition definition)
        {
            ScoreRules rules = definition.ScoreRules != null ? definition.ScoreRules : defaultScoreRules;

            tracker = new MissionScoreTracker(rules);
            tracker.ScoreChanged += OnScoreChanged;
        }

        private void SetUpValidator(MissionAuthoring mission, MissionDefinition definition)
        {
            ParkingZone zone = mission.ParkingZone;

            validator = zone.GetComponent<ParkingValidator>();

            if (validator == null)
            {
                validator = zone.gameObject.AddComponent<ParkingValidator>();
            }

            validator.Zone = zone;
            validator.Configure(
                definition.ParkingType,
                definition.MaxParkingSpeedKmh,
                definition.AngleToleranceDegrees,
                definition.HoldSeconds,
                definition.RequiredContainment,
                definition.AllowOppositeHeading);

            validator.SetVehicle(vehicle.transform);
            validator.StateChanged += OnParkingStateChanged;
            validator.ProgressChanged += OnParkingProgressChanged;
            validator.Validated += OnParkingValidated;
            validator.Begin();
        }

        // Added at runtime so no car prefab or scene object needs editing to collect
        // collision penalties.
        private void SetUpCollisionReporting()
        {
            reporter = vehicle.GetComponent<VehicleCollisionReporter>();

            if (reporter == null)
            {
                reporter = vehicle.gameObject.AddComponent<VehicleCollisionReporter>();
            }

            reporter.Collided += OnVehicleCollided;
        }

        private void PlaceVehicleAtStart(Transform startPoint)
        {
            var body = vehicle.GetComponent<Rigidbody>();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            vehicle.transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
        }

        private void OnScoreChanged(int score)
        {
            ScoreChanged?.Invoke(score);
        }

        private void OnParkingStateChanged(ParkingState state)
        {
            ParkingStateChanged?.Invoke(state);
        }

        private void OnParkingProgressChanged(float progress)
        {
            ParkingProgressChanged?.Invoke(progress);
        }

        private void OnParkingValidated()
        {
            if (!running)
            {
                return;
            }

            Finish(true, MissionFailReason.None);
        }

        private void OnVehicleCollided(VehicleCollisionInfo info)
        {
            if (!running)
            {
                return;
            }

            tracker.RegisterCollision(info.tag, info.impulse, info.colliderId);
        }

        private void Finish(bool parked, MissionFailReason failReason)
        {
            MissionDefinition definition = activeMission.Definition;
            int overtimePenalty = parked ? tracker.ApplyOvertimePenalty(definition.TimeLimitSeconds) : 0;

            bool success = parked && !tracker.IsFailing;

            if (parked && tracker.IsFailing)
            {
                failReason = MissionFailReason.ScoreTooLow;
            }

            var result = new MissionResult
            {
                missionId = definition.MissionId,
                success = success,
                failReason = success ? MissionFailReason.None : failReason,
                score = tracker.Score,
                stars = success ? tracker.Stars : 0,
                timeSeconds = tracker.ElapsedSeconds,
                collisions = tracker.Collisions,
                overtimePenalty = overtimePenalty
            };

            if (success)
            {
                RecordSuccess(definition, result);
            }

            StopActiveMission();
            SetLegacyHandlersEnabled(true);
            running = false;

            // Logged so the result is observable before any result UI is wired up.
            Debug.Log(success
                ? $"[MissionManager] Mission {result.missionId} complete - score {result.score}, {result.stars} star(s), {result.timeSeconds:0.0}s, {result.collisions} collision(s), +{result.coinsAwarded} coins{(result.isNewBest ? " (new best)" : string.Empty)}"
                : $"[MissionManager] Mission {result.missionId} failed - {result.FailReasonText} (score {result.score})");

            vehicle?.SetVehicleEnabled(false);
            GameFlowManager.Instance?.SetGameState(GameState.Result);

            MissionEnded?.Invoke(result);
        }

        private void RecordSuccess(MissionDefinition definition, MissionResult result)
        {
            SaveData data = SaveManager.Data;
            MissionProgressData progress = data.GetOrCreateMission(definition.MissionId);

            result.isFirstCompletion = !progress.completed;
            result.isNewBest = progress.SubmitResult(result.score, result.stars, result.timeSeconds, result.collisions);

            progress.unlocked = true;
            data.GetOrCreateMission(definition.MissionId + 1).unlocked = true;

            // Points at the next mission to play, matching how the legacy
            // "CurrentMission" value was migrated.
            data.currentMissionId = definition.MissionId + 1;

            result.coinsAwarded = EconomyManager.CalculateReward(
                result.stars,
                definition.CoinReward,
                result.isFirstCompletion,
                result.isNewBest);

            // Add() persists the whole save, including the progress written above.
            if (result.coinsAwarded > 0)
            {
                EconomyManager.Add(result.coinsAwarded);
            }
            else
            {
                SaveManager.Save();
            }
        }

        private void StopActiveMission()
        {
            if (validator != null)
            {
                validator.StateChanged -= OnParkingStateChanged;
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
        }

        private void SetLegacyHandlersEnabled(bool enabled)
        {
            legacyTriggers ??= FindObjectsByType<ParkingTrigger>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            legacyFailHandlers ??= FindObjectsByType<MissionFailedHandler>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (ParkingTrigger trigger in legacyTriggers)
            {
                if (trigger != null)
                {
                    trigger.enabled = enabled;
                }
            }

            foreach (MissionFailedHandler handler in legacyFailHandlers)
            {
                if (handler != null)
                {
                    handler.enabled = enabled;
                }
            }
        }

        private static CarController ResolveActiveVehicle()
        {
            CarController[] controllers = FindObjectsByType<CarController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (CarController controller in controllers)
            {
                if (controller.gameObject.activeInHierarchy)
                {
                    return controller;
                }
            }

            return null;
        }
    }
}
