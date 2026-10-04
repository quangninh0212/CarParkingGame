using CarParkingGame.Core;
using CarParkingGame.Garage;
using CarParkingGame.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The in-game pause screen, and the button that opens it.
    //
    // Pausing is the one thing the player can always do, so it is tied to GameSession's
    // own pause state rather than to Time.timeScale: the result screens also stop play,
    // and a pause menu that appeared over them would be a dead end.
    public class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Text modeLabel;

        [Header("Menu screens, over the paused game")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private MenuController menu;
        [SerializeField] private GameObject menuRoot;

        private GameSession Session => session != null ? session : GameSession.Instance;

        private void Awake()
        {
            pauseButton?.onClick.AddListener(OnPause);
            resumeButton?.onClick.AddListener(OnResume);
            restartButton?.onClick.AddListener(OnRestart);
            menuButton?.onClick.AddListener(OnMenu);
            settingsButton?.onClick.AddListener(OnSettings);
            garageButton?.onClick.AddListener(OnGarage);

            SetPanel(false);
        }

        private void OnEnable()
        {
            GameSession active = Session;

            if (active != null)
            {
                active.PauseChanged += OnPauseChanged;
            }

            if (menu != null)
            {
                menu.ClosedOverPause += OnMenuScreenClosed;
            }

            if (GarageManager.Instance != null)
            {
                GarageManager.Instance.GarageChanged += OnGarageChanged;
            }

            SetPanel(active != null && active.IsPaused);
        }

        private void OnDisable()
        {
            GameSession active = Session;

            if (active != null)
            {
                active.PauseChanged -= OnPauseChanged;
            }

            if (menu != null)
            {
                menu.ClosedOverPause -= OnMenuScreenClosed;
            }

            if (GarageManager.Instance != null)
            {
                GarageManager.Instance.GarageChanged -= OnGarageChanged;
            }
        }

        // Settings and the garage both belong to the main menu, so this shows the menu's
        // root over the paused game rather than keeping a second copy of either in here.
        // The session stays in play, so the level is still there when the player backs out.
        private void OnSettings() => OpenMenuScreen(false);

        private void OnGarage() => OpenMenuScreen(true);

        private void OpenMenuScreen(bool garage)
        {
            if (menu == null || menuRoot == null)
            {
                return;
            }

            SetPanel(false);
            carChanged = false;

            // The garage has to show the car, which means the showroom, which means the
            // menu camera. Settings does not, and leaving the level on screen behind it is
            // better than cutting away from it.
            if (garage)
            {
                Session?.ShowShowroomOverPause(true);
            }

            // Switching the root on wakes MenuController, which sends itself home, so the
            // screen has to be chosen after that and not before.
            menuRoot.SetActive(true);

            if (garage)
            {
                menu.OpenGarageOverPause();
            }
            else
            {
                menu.OpenSettingsOverPause();
            }
        }

        // A different car, or the same one in a different colour, is a different car in the
        // level - it is a separate object, parked wherever the garage left it. Restarting
        // the level puts the new one on the start line. The level, not the run: a player
        // who repaints at stage thirty keeps stage thirty.
        private void OnGarageChanged()
        {
            GameSession active = Session;

            if (active != null && active.Mode != GameplayMode.None && active.IsPaused)
            {
                carChanged = true;
            }
        }

        private void OnMenuScreenClosed()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
            }

            Session?.RestoreGameplayCamera();

            if (carChanged)
            {
                carChanged = false;
                OnRestart();
                return;
            }

            GameSession active = Session;
            SetPanel(active == null || active.IsPaused);
        }

        private void OnPauseChanged(bool paused)
        {
            SetPanel(paused);

            if (paused && modeLabel != null)
            {
                modeLabel.text = DescribeMode();
            }
        }

        private string DescribeMode()
        {
            GameSession active = Session;

            if (active == null)
            {
                return string.Empty;
            }

            return active.Mode switch
            {
                GameplayMode.Practice => "PRACTICE",
                GameplayMode.Challenge => $"CHALLENGE  STAGE {active.ChallengeStage}/{MissionManager.Instance?.MissionCount}",
                GameplayMode.FreeRoam => "FREE DRIVE",
                _ => string.Empty
            };
        }

        private void OnPause()
        {
            Session?.Pause();
        }

        private void OnResume()
        {
            Session?.Resume();
        }

        // Free roam has nothing to restart, so the button puts the car back at its spawn.
        private void OnRestart()
        {
            GameSession active = Session;

            if (active == null)
            {
                return;
            }

            active.Resume();

            switch (active.Mode)
            {
                case GameplayMode.Challenge:
                    active.RestartChallengeStage();
                    break;
                case GameplayMode.FreeRoam:
                    active.StartFreeRoam();
                    break;
                default:
                    MissionDefinition mission = MissionManager.Instance?.ActiveMission;

                    if (mission != null)
                    {
                        active.StartPractice(mission.MissionId);
                    }

                    break;
            }
        }

        private void OnMenu()
        {
            Session?.ReturnToMenu();
        }

        private bool carChanged;

        private void SetPanel(bool visible)
        {
            if (panel != null && panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }
    }
}
