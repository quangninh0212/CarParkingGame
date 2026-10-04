using CarParkingGame.Core;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Score, timer and the parking feedback indicator. Colour goes neutral -> amber ->
    // green as the car gets the bay right, and the progress bar fills over the hold time
    // so the player can see the park being confirmed rather than guessing.
    public class GameplayHudView : MonoBehaviour
    {
        [SerializeField] private MissionManager missionManager;
        [SerializeField] private Text missionNameLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text coinLabel;
        [SerializeField] private Text timerLabel;
        [SerializeField] private GameObject timerContainer;

        [Header("Parking feedback")]
        [SerializeField] private Image parkingStateIndicator;
        [SerializeField] private Image parkingProgressBar;
        [SerializeField] private Text parkingHintLabel;
        [SerializeField] private Color outsideColor = new Color(0.8f, 0.25f, 0.25f);
        [SerializeField] private Color partialColor = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] private Color validColor = new Color(0.3f, 0.8f, 0.35f);

        private ParkingState lastState = ParkingState.Outside;

        private int StageCount => Missions != null ? Missions.MissionCount : 0;

        private MissionManager Missions => missionManager != null ? missionManager : MissionManager.Instance;

        private void OnEnable()
        {
            MissionManager missions = Missions;

            if (missions != null)
            {
                missions.MissionStarted += OnMissionStarted;
                missions.MissionEnded += OnMissionEnded;
                missions.ScoreChanged += OnScoreChanged;
                missions.ParkingStateChanged += OnParkingStateChanged;
                missions.ParkingProgressChanged += OnParkingProgressChanged;
            }

            RefreshCoins();
            SetParkingFeedback(ParkingState.Outside, 0f);

            // The HUD is hidden while the menu is up, and a mission is started from the
            // menu, so MissionStarted usually fires before this object is enabled. Catch
            // up from the manager's current state instead of waiting for an event that
            // has already happened.
            if (missions != null && missions.IsRunning && missions.ActiveMission != null)
            {
                OnMissionStarted(missions.ActiveMission);
            }
        }

        private void OnDisable()
        {
            MissionManager missions = Missions;

            if (missions == null)
            {
                return;
            }

            missions.MissionStarted -= OnMissionStarted;
            missions.MissionEnded -= OnMissionEnded;
            missions.ScoreChanged -= OnScoreChanged;
            missions.ParkingStateChanged -= OnParkingStateChanged;
            missions.ParkingProgressChanged -= OnParkingProgressChanged;
        }

        private void Update()
        {
            MissionManager missions = Missions;

            if (missions == null || !missions.IsRunning || timerLabel == null)
            {
                return;
            }

            // The limit comes from the manager, not the definition: challenge mode puts the
            // same mission on a clock the definition knows nothing about.
            float remaining = missions.RemainingSeconds;

            if (remaining < 0f)
            {
                return;
            }

            timerLabel.text = FormatTime(remaining);
        }

        // The hint names whichever condition is currently blocking the park, and that
        // changes while the player manoeuvres without the parking state changing, so it is
        // refreshed per frame rather than only on the state event.
        private void LateUpdate()
        {
            if (parkingHintLabel == null || lastState != ParkingState.Partial)
            {
                return;
            }

            MissionManager missions = Missions;

            if (missions != null && missions.IsRunning)
            {
                parkingHintLabel.text = DescribeWhatIsMissing();
            }
        }

        private void OnMissionStarted(MissionDefinition definition)
        {
            MissionManager missions = Missions;

            if (missionNameLabel != null)
            {
                string prefix = GameSession.Instance != null && GameSession.Instance.Mode == GameplayMode.Challenge
                    ? $"STAGE {GameSession.Instance.ChallengeStage}/{StageCount}  "
                    : $"{definition.MissionId:00}  ";

                missionNameLabel.text = prefix + definition.DisplayName;
            }

            if (timerContainer != null)
            {
                timerContainer.SetActive(missions != null && missions.TimeLimitSeconds > 0f);
            }

            OnScoreChanged(Missions != null && Missions.Tracker != null ? Missions.Tracker.Score : 0);
            SetParkingFeedback(ParkingState.Outside, 0f);
        }

        private void OnMissionEnded(MissionResult result)
        {
            SetParkingFeedback(ParkingState.Outside, 0f);
            RefreshCoins();
        }

        private void OnScoreChanged(int score)
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = score.ToString();
            }
        }

        private void OnParkingStateChanged(ParkingState state)
        {
            SetParkingFeedback(state, state == ParkingState.Complete ? 1f : 0f);
        }

        private void OnParkingProgressChanged(float progress)
        {
            if (parkingProgressBar != null)
            {
                parkingProgressBar.fillAmount = progress;
            }
        }

        private void SetParkingFeedback(ParkingState state, float progress)
        {
            lastState = state;

            if (parkingStateIndicator != null)
            {
                parkingStateIndicator.color = state switch
                {
                    ParkingState.Partial => partialColor,
                    ParkingState.Holding => validColor,
                    ParkingState.Complete => validColor,
                    _ => outsideColor
                };
            }

            // While holding, the progress event owns the bar; otherwise reset or fill it.
            if (parkingProgressBar != null && state != ParkingState.Holding)
            {
                parkingProgressBar.fillAmount = progress;
            }

            if (parkingHintLabel == null)
            {
                return;
            }

            parkingHintLabel.text = state switch
            {
                ParkingState.Partial => DescribeWhatIsMissing(),
                ParkingState.Holding => "Hold it...",
                ParkingState.Complete => "Parked",
                _ => string.Empty
            };
        }

        // "Line the car up inside the bay" was true but useless: it said nothing about
        // which of the three conditions was failing, so a car that was inside and straight
        // but still rolling looked like a bug. One sentence naming the actual blocker.
        private string DescribeWhatIsMissing()
        {
            ParkingValidator validator = Missions != null ? Missions.ActiveValidator : null;

            if (validator == null)
            {
                return "Line the car up inside the bay";
            }

            if (validator.LastContainment < 0.999f)
            {
                return "Get the whole car inside the bay";
            }

            if (validator.LastHeadingError > 20f)
            {
                return "Line the car up with the arrow";
            }

            if (validator.LastSpeedKmh > 3f)
            {
                return "Come to a stop";
            }

            return "Almost - hold it still";
        }

        private void RefreshCoins()
        {
            if (coinLabel != null)
            {
                coinLabel.text = SaveManager.Data.coins.ToString();
            }
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(seconds);
            return $"{whole / 60:0}:{whole % 60:00}";
        }
    }
}
