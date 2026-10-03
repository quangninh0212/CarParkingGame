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

            MissionDefinition definition = missions.ActiveMission;

            if (definition == null || !definition.IsTimed)
            {
                return;
            }

            float remaining = Mathf.Max(0f, definition.TimeLimitSeconds - missions.Tracker.ElapsedSeconds);
            timerLabel.text = FormatTime(remaining);
        }

        private void OnMissionStarted(MissionDefinition definition)
        {
            if (missionNameLabel != null)
            {
                missionNameLabel.text = $"{definition.MissionId:00}  {definition.DisplayName}";
            }

            if (timerContainer != null)
            {
                timerContainer.SetActive(definition.IsTimed);
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
                ParkingState.Partial => "Line the car up inside the bay",
                ParkingState.Holding => "Hold it...",
                ParkingState.Complete => "Parked",
                _ => string.Empty
            };
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
