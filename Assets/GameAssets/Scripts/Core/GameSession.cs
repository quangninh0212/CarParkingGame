using System;
using CarParkingGame.Missions;
using CarParkingGame.UI;
using CarParkingGame.Vehicle;
using UnityEngine;

namespace CarParkingGame.Core
{
    public enum GameplayMode
    {
        None,
        Practice,
        Challenge,
        FreeRoam
    }

    // The single owner of "what is the game doing right now".
    //
    // The legacy MainMenuManager did this job by hand from its own button handlers, and
    // everything added since - the mission runner, the HUD, the garage - had to guess at
    // its state from Time.timeScale. One object now switches the cameras, the driving
    // controls, the showroom cars and the clock, and raises an event, so nothing has to
    // infer anything.
    //
    // ParkingTrigger and MissionFailedHandler stand down while this exists: they were
    // written to end a mission by freezing the car directly, which is why driving into a
    // cone used to leave the player stuck with no way to finish.
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Scene wiring")]
        [SerializeField] private MissionManager missions;
        [SerializeField] private GameObject menuCamera;
        [SerializeField] private GameObject gameplayCamera;
        [SerializeField] private GameObject mobileControls;
        [SerializeField] private GameObject showroomCars;
        [SerializeField] private GameObject playerCars;

        [Header("Challenge mode")]
        [Tooltip("Seconds allowed for each stage of a challenge run.")]
        [SerializeField] private float challengeSecondsPerStage = 120f;

        [Header("Free roam")]
        [Tooltip("Optional. Where free roam puts the car; the first mission's start point is used when empty.")]
        [SerializeField] private Transform freeRoamSpawn;

        [Tooltip("The city, built by Tools/Car Parking/Build City Free Drive Map. Switched on only while the city map is being driven.")]
        [SerializeField] private GameObject cityMapRoot;

        [Tooltip("Where the city map puts the car.")]
        [SerializeField] private Transform cityRoamSpawn;

        private GameplayMode mode = GameplayMode.None;
        private bool paused;
        private int challengeStage;

        public static bool Exists => Instance != null;

        public GameplayMode Mode => mode;
        public bool IsPaused => paused;
        public bool InMenu => mode == GameplayMode.None;
        public int ChallengeStage => challengeStage;
        public float ChallengeSecondsPerStage => challengeSecondsPerStage;

        public event Action<GameplayMode> ModeChanged;
        public event Action<bool> PauseChanged;

        private MissionManager Missions => missions != null ? missions : MissionManager.Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            MissionManager runner = Missions;

            if (runner != null)
            {
                runner.MissionEnded += OnMissionEnded;
            }

            ReturnToMenu();
        }

        private void OnDisable()
        {
            MissionManager runner = Missions;

            if (runner != null)
            {
                runner.MissionEnded -= OnMissionEnded;
            }
        }

        // ----- entering and leaving play ------------------------------------------------

        public void ReturnToMenu()
        {
            Missions?.CancelMission();

            mode = GameplayMode.None;
            paused = false;
            challengeStage = 0;

            Time.timeScale = 0f;
            SetActive(cityMapRoot, false);
            SetActive(menuCamera, true);
            SetActive(gameplayCamera, false);
            SetActive(mobileControls, false);
            SetActive(showroomCars, true);

            FreezeCar();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameFlowManager.Instance?.SetGameMode(GameMode.MainMenu);
            GameFlowManager.Instance?.SetGameState(GameState.Menu);

            ModeChanged?.Invoke(mode);
            PauseChanged?.Invoke(false);
        }

        public bool StartPractice(int missionId)
        {
            MissionManager runner = Missions;

            if (runner == null)
            {
                Debug.LogError("[GameSession] No MissionManager, so practice cannot start.");
                return false;
            }

            EnterPlay(GameplayMode.Practice);

            if (runner.StartMission(missionId, MissionLaunchOptions.Practice))
            {
                return true;
            }

            ReturnToMenu();
            return false;
        }

        // Every level in turn, on a clock, with the car set down at each one.
        public void StartChallenge()
        {
            MissionManager runner = Missions;

            if (runner == null)
            {
                Debug.LogError("[GameSession] No MissionManager, so a challenge cannot start.");
                return;
            }

            EnterPlay(GameplayMode.Challenge);
            challengeStage = 0;

            // Picks up where the last run stopped. A challenge run is fifty levels on a
            // clock; being sent back to stage one for quitting at stage thirty is not a
            // difficulty, it is a punishment for closing the game.
            int highest = runner.HighestMissionId;
            int resume = Mathf.Clamp(SaveManager.Data.challengeStage, 1, Mathf.Max(1, highest));

            StartChallengeStage(resume);
        }

        // Which of the two free-drive maps was last chosen, so restarting from the pause
        // menu puts the player back on the map they were driving rather than the other one.
        // Backed by a named field rather than an auto-property so the city check can
        // set it and then run the real placement, instead of testing a copy of it.
        [SerializeField, HideInInspector] private bool inCity;

        public bool InCity => inCity;

        public void StartFreeRoam()
        {
            StartFreeRoam(InCity);
        }

        public void StartFreeRoam(bool city)
        {
            MissionManager runner = Missions;

            inCity = city && cityMapRoot != null;

            EnterPlay(GameplayMode.FreeRoam);

            if (runner != null)
            {
                runner.SetAllEnvironmentsActive(false);
            }

            // One map at a time. The city is over a million triangles with a collider on
            // all of it, so leaving it switched on while the player drives the circuit
            // would cost the whole of it for nothing.
            if (cityMapRoot != null)
            {
                cityMapRoot.SetActive(InCity);
            }

            PlaceCarAtFreeRoamSpawn();
            ThawCar();
        }

        public void RestartChallengeStage()
        {
            if (mode != GameplayMode.Challenge || challengeStage <= 0)
            {
                return;
            }

            StartChallengeStage(challengeStage);
        }

        public bool AdvanceChallenge()
        {
            if (mode != GameplayMode.Challenge)
            {
                return false;
            }

            MissionManager runner = Missions;
            int next = challengeStage + 1;

            if (runner == null || next > runner.HighestMissionId)
            {
                // The run is over. Clearing it means the next one starts at the beginning
                // instead of resuming onto the last level for ever.
                SaveManager.Data.challengeStage = 0;
                SaveManager.Save();
                return false;
            }

            StartChallengeStage(next);
            return true;
        }

        private void StartChallengeStage(int stage)
        {
            ScreenFade fade = ScreenFade.Instance;

            if (fade == null)
            {
                SwitchToStage(stage);
                return;
            }

            // Behind the black, so the player does not watch the car and the whole lot
            // around it be replaced.
            fade.Cover(() => SwitchToStage(stage));
        }

        private void SwitchToStage(int stage)
        {
            challengeStage = stage;

            // Written before the level starts rather than after it is finished, because
            // what has to survive quitting is which level the player was on, not which one
            // they beat.
            SaveManager.Data.challengeStage = stage;
            SaveManager.Save();

            MissionManager runner = Missions;

            if (runner == null)
            {
                return;
            }

            if (!runner.StartMission(stage, MissionLaunchOptions.Challenge(challengeSecondsPerStage)))
            {
                Debug.LogError($"[GameSession] Challenge stage {stage} has no layout; the run stops here.");
                ReturnToMenu();
            }
        }

        // Puts the menu's showroom over a paused level and takes it away again, without
        // leaving play.
        //
        // The garage is reachable from the pause menu, and a garage with no car in it is
        // not a garage - but the mode must not change, or the session would tear the
        // mission down and the player would lose the level they are standing in.
        public void ShowShowroomOverPause(bool showing)
        {
            if (mode == GameplayMode.None)
            {
                return;
            }

            SetActive(menuCamera, showing);
            SetActive(gameplayCamera, !showing);
            SetActive(showroomCars, showing);

            // Hidden while a menu screen is over the level, and put back when it closes.
            // Switching them off both ways left the player back in the level with no
            // steering wheel and no pedals, and nothing short of restarting brought them
            // back.
            SetActive(mobileControls, !showing);
        }

        // Back to the level after the pause menu is done with it.
        public void RestoreGameplayCamera()
        {
            ShowShowroomOverPause(false);
        }

        private void EnterPlay(GameplayMode next)
        {
            mode = next;
            paused = false;

            // Off by default on every way into play. StartFreeRoam switches it back on
            // after this when the city is the map that was picked, so no other entry point
            // has to remember the city exists.
            SetActive(cityMapRoot, false);

            Time.timeScale = 1f;
            SetActive(menuCamera, false);
            SetActive(gameplayCamera, true);
            SetActive(mobileControls, true);
            SetActive(showroomCars, false);

            ActiveVehicleLocator.Invalidate();

            GameFlowManager.Instance?.SetGameMode(next switch
            {
                GameplayMode.FreeRoam => GameMode.OpenWorld,
                _ => GameMode.Practice
            });

            GameFlowManager.Instance?.SetGameState(GameState.Playing);

            ModeChanged?.Invoke(mode);
            PauseChanged?.Invoke(false);
        }

        // ----- pausing ------------------------------------------------------------------

        public void Pause()
        {
            if (InMenu || paused)
            {
                return;
            }

            paused = true;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameFlowManager.Instance?.SetGameState(GameState.Paused);
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (InMenu || !paused)
            {
                return;
            }

            paused = false;
            Time.timeScale = 1f;

            GameFlowManager.Instance?.SetGameState(GameState.Playing);
            PauseChanged?.Invoke(false);
        }

        public void TogglePause()
        {
            if (paused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        // ----- car handling -------------------------------------------------------------

        private void OnMissionEnded(MissionResult result)
        {
            // The result screen owns what happens next; the session only has to make sure
            // the clock keeps running so the screen's own animations and buttons work.
            Time.timeScale = 1f;
        }

        private void PlaceCarAtFreeRoamSpawn()
        {
            CarController car = ActiveVehicleLocator.Current;

            if (car == null)
            {
                return;
            }

            Transform spawn = InCity && cityRoamSpawn != null ? cityRoamSpawn : freeRoamSpawn;

            if (spawn == null)
            {
                MissionManager runner = Missions;
                MissionAuthoring first = runner != null ? runner.GetMission(1) : null;
                spawn = first != null ? first.StartPoint : null;
            }

            if (spawn == null)
            {
                return;
            }

            var body = car.GetComponent<Rigidbody>();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // The circuit spawn is left exactly as it was. Only the city needs the car
            // stood on the ground, and the circuit has been played enough that changing
            // where it starts the car would be a change nobody asked for.
            Vector3 position = InCity ? StandOnGround(car.transform, spawn.position) : spawn.position;

            car.transform.SetPositionAndRotation(position, spawn.rotation);
        }

        // Where to put a car's pivot so it stands on the road under a spawn marker.
        //
        // The city spawn sits a few centimetres over the tarmac, which is right for a car
        // whose origin is on its wheels - the sedan and the hatchback - and wrong for one
        // whose origin is in the middle of the body. The classic's is 0.97m above its own
        // tyres, so putting its pivot at the marker buried its wheels most of a metre under
        // the road, and the physics engine pushed that overlap apart the only way it can:
        // by throwing the car into the air, to land on its roof.
        //
        // The car park levels do not hit this because the course builder lifts every start
        // point a metre off the floor, which happens to cover the deepest pivot in the car
        // packs. Looking for the ground instead means no height has to happen to be right.
        public static Vector3 StandOnGround(Transform vehicle, Vector3 spawn)
        {
            if (vehicle == null)
            {
                return spawn;
            }

            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                return spawn;
            }

            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float ride = Mathf.Max(0f, vehicle.position.y - bounds.min.y);

            RaycastHit[] hits = Physics.RaycastAll(
                spawn + Vector3.up * 3f, Vector3.down, 28f, ~0, QueryTriggerInteraction.Ignore);

            float road = float.MinValue;

            foreach (RaycastHit hit in hits)
            {
                // Never the car itself. It may already be standing at the spawn, and its own
                // roof would otherwise be taken for the road.
                if (!hit.collider.transform.IsChildOf(vehicle) && hit.point.y > road)
                {
                    road = hit.point.y;
                }
            }

            // A centimetre of air, so it settles onto the road rather than starting the
            // frame already pressed into it.
            return road > float.MinValue
                ? new Vector3(spawn.x, road + ride + 0.01f, spawn.z)
                : spawn;
        }

        private void FreezeCar()
        {
            CarController car = ActiveVehicleLocator.Current;
            car?.SetVehicleEnabled(false);
        }

        private void ThawCar()
        {
            CarController car = ActiveVehicleLocator.Current;
            car?.SetVehicleEnabled(true);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
