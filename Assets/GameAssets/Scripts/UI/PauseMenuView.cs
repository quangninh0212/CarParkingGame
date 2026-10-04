using CarParkingGame.Core;
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

        [Header("Settings, over the paused game")]
        [SerializeField] private Button settingsButton;
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
                menu.SettingsClosedOverPause += OnSettingsClosed;
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
                menu.SettingsClosedOverPause -= OnSettingsClosed;
            }
        }

        // The settings screen belongs to the main menu, so this shows the menu's root over
        // the paused game rather than keeping a second copy of every slider in here. The
        // session stays in play, so the level is still behind it when the player backs out.
        private void OnSettings()
        {
            if (menu == null || menuRoot == null)
            {
                return;
            }

            SetPanel(false);

            // Switching the root on wakes MenuController, which sends itself home, so the
            // screen has to be chosen after that and not before.
            menuRoot.SetActive(true);
            menu.OpenSettingsOverPause();
        }

        private void OnSettingsClosed()
        {
            if (menuRoot != null)
            {
                menuRoot.SetActive(false);
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

        private void SetPanel(bool visible)
        {
            if (panel != null && panel.activeSelf != visible)
            {
                panel.SetActive(visible);
            }
        }
    }
}
