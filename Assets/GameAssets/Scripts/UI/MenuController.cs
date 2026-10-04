using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The whole front end, as one state machine over a list of screens.
    //
    // The screens used to be independent panels that each switched themselves on, which
    // is how the garage ended up drawn on top of the main menu's buttons. Here exactly
    // one screen is active at a time - Show() turns every other one off - and Back()
    // walks a stack, so there is no combination of taps that can leave two screens up.
    public class MenuController : MonoBehaviour
    {
        [Header("Session")]
        [SerializeField] private GameSession session;

        [Header("Screens")]
        [SerializeField] private GameObject homeScreen;
        [SerializeField] private GameObject modeScreen;
        [SerializeField] private GameObject practiceScreen;
        [SerializeField] private GameObject garageScreen;
        [SerializeField] private GameObject settingsScreen;

        [Tooltip("The container holding every menu screen. Hidden outright while driving.")]
        [SerializeField] private GameObject menuRoot;

        [Tooltip("Aims the menu camera at the showroom car; the garage screen needs it framed differently.")]
        [SerializeField] private ShowroomCameraRig showroom;

        [Header("Home")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button modeButton;
        [SerializeField] private Button garageButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Text coinLabel;

        [Header("Mode select")]
        [SerializeField] private Button practiceModeButton;
        [SerializeField] private Button challengeModeButton;
        [SerializeField] private Button freeRoamModeButton;

        [Header("Back")]
        [Tooltip("Every button that means 'go back one screen'.")]
        [SerializeField] private Button[] backButtons;

        private readonly List<GameObject> screens = new List<GameObject>();
        private readonly Stack<GameObject> history = new Stack<GameObject>();

        private GameObject current;

        // A menu screen opened from the pause menu rather than from the home screen. Back
        // out of it returns to the paused game, not to the main menu.
        private bool overPause;

        public event System.Action ClosedOverPause;

        private GameSession Session => session != null ? session : GameSession.Instance;

        private void Awake()
        {
            CollectScreens();

            Wire(playButton, () => Push(modeScreen));
            Wire(modeButton, () => Push(modeScreen));
            Wire(garageButton, () => Push(garageScreen));
            Wire(settingsButton, () => Push(settingsScreen));
            Wire(quitButton, Quit);

            Wire(practiceModeButton, () => Push(practiceScreen));
            Wire(challengeModeButton, StartChallenge);
            Wire(freeRoamModeButton, StartFreeRoam);

            foreach (Button back in backButtons ?? System.Array.Empty<Button>())
            {
                Wire(back, Back);
            }
        }

        private void OnEnable()
        {
            GameSession active = Session;

            if (active != null)
            {
                active.ModeChanged += OnModeChanged;
            }

            GoHome();
        }

        private void OnDisable()
        {
            GameSession active = Session;

            if (active != null)
            {
                active.ModeChanged -= OnModeChanged;
            }
        }

        private void CollectScreens()
        {
            screens.Clear();

            foreach (GameObject screen in new[] { homeScreen, modeScreen, practiceScreen, garageScreen, settingsScreen })
            {
                if (screen != null)
                {
                    screens.Add(screen);
                }
            }
        }

        public void GoHome()
        {
            history.Clear();
            Show(homeScreen);
            RefreshCoins();
        }

        public void Push(GameObject screen)
        {
            if (screen == null)
            {
                return;
            }

            if (current != null && current != screen)
            {
                history.Push(current);
            }

            Show(screen);
        }

        // Shows one of the menu's screens over a paused game. The caller switches menuRoot
        // on first, which wakes this component and sends it home, so these have to run
        // after that and put it where it actually belongs.
        public void OpenSettingsOverPause() => OpenOverPause(settingsScreen);

        public void OpenGarageOverPause() => OpenOverPause(garageScreen);

        private void OpenOverPause(GameObject screen)
        {
            overPause = true;
            history.Clear();
            Show(screen);
        }

        public void Back()
        {
            if (overPause)
            {
                overPause = false;
                ClosedOverPause?.Invoke();
                return;
            }

            if (history.Count == 0)
            {
                Show(homeScreen);
                return;
            }

            Show(history.Pop());
        }

        private void Show(GameObject screen)
        {
            current = screen;

            foreach (GameObject candidate in screens)
            {
                candidate.SetActive(candidate == screen);
            }

            // The garage needs the showroom whether it was opened from the menu or over a
            // paused level - a garage with no car in it is not a garage. Everything else
            // leaves the camera alone while it sits over a level, because the player is
            // looking at the level.
            if (showroom != null && (!overPause || screen == garageScreen))
            {
                showroom.SetFocus(screen == garageScreen ? ShowroomFocus.Garage : ShowroomFocus.Home);
            }

            RefreshCoins();
        }

        // ----- mode entry points ---------------------------------------------------------

        // Practice launches from the level list itself (MissionSelectionView), because the
        // mission is chosen there; the other two have nothing to choose.
        private void StartChallenge()
        {
            Session?.StartChallenge();
        }

        private void StartFreeRoam()
        {
            Session?.StartFreeRoam();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnModeChanged(GameplayMode mode)
        {
            bool inMenu = mode == GameplayMode.None;
            overPause = false;

            if (menuRoot != null)
            {
                menuRoot.SetActive(inMenu);
            }

            if (inMenu)
            {
                GoHome();
            }
        }

        private void RefreshCoins()
        {
            if (coinLabel != null)
            {
                coinLabel.text = SaveManager.Data.coins.ToString();
            }
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }
    }
}
