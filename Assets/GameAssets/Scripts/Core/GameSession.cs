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

        public void StartFreeRoam()
        {
            MissionManager runner = Missions;

            EnterPlay(GameplayMode.FreeRoam);

            if (runner != null)
            {
                runner.SetAllEnvironmentsActive(false);
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

            Transform spawn = freeRoamSpawn;

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

            car.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
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
