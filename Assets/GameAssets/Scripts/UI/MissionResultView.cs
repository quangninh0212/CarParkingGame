using CarParkingGame.Missions;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Both end-of-mission screens. One component with two panels rather than two scripts:
    // they share the same result data, the same star widgets and the same buttons, and
    // wiring them twice in the Inspector is how references get missed.
    public class MissionResultView : MonoBehaviour
    {
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private MissionCatalog catalog;
        [SerializeField] private MainMenuManager mainMenu;

        [Header("Panels")]
        [SerializeField] private GameObject completePanel;
        [SerializeField] private GameObject failedPanel;

        [Header("Complete")]
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Text collisionsLabel;
        [SerializeField] private Text penaltyLabel;
        [SerializeField] private Text coinsLabel;
        [SerializeField] private GameObject newBestBadge;
        [SerializeField] private GameObject allMissionsCompleteMessage;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Color earnedStarColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color emptyStarColor = new Color(1f, 1f, 1f, 0.2f);
        [SerializeField] private Button nextButton;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button menuButton;

        [Header("Failed")]
        [SerializeField] private Text failReasonLabel;
        [SerializeField] private Text failScoreLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button failMenuButton;

        private int lastMissionId;

        private MissionManager Missions => missionManager != null ? missionManager : MissionManager.Instance;

        private void Awake()
        {
            nextButton?.onClick.AddListener(StartNextMission);
            replayButton?.onClick.AddListener(ReplayMission);
            retryButton?.onClick.AddListener(ReplayMission);
            menuButton?.onClick.AddListener(ReturnToMenu);
            failMenuButton?.onClick.AddListener(ReturnToMenu);

            HideAll();
        }

        private void OnEnable()
        {
            MissionManager missions = Missions;

            if (missions != null)
            {
                missions.MissionEnded += OnMissionEnded;
                missions.MissionStarted += OnMissionStarted;
            }
        }

        private void OnDisable()
        {
            MissionManager missions = Missions;

            if (missions == null)
            {
                return;
            }

            missions.MissionEnded -= OnMissionEnded;
            missions.MissionStarted -= OnMissionStarted;
        }

        private void OnMissionStarted(MissionDefinition definition)
        {
            HideAll();
        }

        private void OnMissionEnded(MissionResult result)
        {
            lastMissionId = result.missionId;

            // Driving locks the cursor on desktop; the result buttons need it back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (result.success)
            {
                ShowComplete(result);
            }
            else
            {
                ShowFailed(result);
            }
        }

        private void ShowComplete(MissionResult result)
        {
            SetPanel(completePanel, true);
            SetPanel(failedPanel, false);

            SetText(scoreLabel, result.score.ToString());
            SetText(timeLabel, FormatTime(result.timeSeconds));
            SetText(collisionsLabel, result.collisions.ToString());
            SetText(penaltyLabel, result.overtimePenalty > 0 ? $"-{result.overtimePenalty}" : "0");
            SetText(coinsLabel, $"+{result.coinsAwarded}");

            if (newBestBadge != null)
            {
                newBestBadge.SetActive(result.isNewBest);
            }

            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].color = i < result.stars ? earnedStarColor : emptyStarColor;
                    }
                }
            }

            // There is no mission 31: past the end of the ladder, offer no Next button.
            bool hasNext = HasMission(result.missionId + 1);

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(hasNext);
            }

            if (allMissionsCompleteMessage != null)
            {
                allMissionsCompleteMessage.SetActive(!hasNext);
            }
        }

        private void ShowFailed(MissionResult result)
        {
            SetPanel(completePanel, false);
            SetPanel(failedPanel, true);

            SetText(failReasonLabel, result.FailReasonText);
            SetText(failScoreLabel, result.score.ToString());
        }

        private bool HasMission(int missionId)
        {
            if (catalog != null && catalog.Find(missionId) == null)
            {
                return false;
            }

            MissionManager missions = Missions;
            return missions != null && missions.GetMission(missionId) != null;
        }

        private void StartNextMission()
        {
            HideAll();
            Missions?.StartMission(lastMissionId + 1);
        }

        private void ReplayMission()
        {
            HideAll();
            Missions?.StartMission(lastMissionId);
        }

        private void ReturnToMenu()
        {
            HideAll();

            if (mainMenu != null)
            {
                mainMenu.ShowMainMenu();
            }
        }

        private void HideAll()
        {
            SetPanel(completePanel, false);
            SetPanel(failedPanel, false);
        }

        private static void SetPanel(GameObject panel, bool visible)
        {
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{whole / 60:0}:{whole % 60:00}";
        }
    }
}
