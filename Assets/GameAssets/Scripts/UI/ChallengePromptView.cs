using CarParkingGame.Core;
using CarParkingGame.OpenWorld;
using CarParkingGame.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The "PARKING CHALLENGE / difficulty / reward / START" panel that appears when the
    // player drives up to a marker. Cancelling just hides it and free driving continues.
    public class ChallengePromptView : MonoBehaviour
    {
        [SerializeField] private OpenWorldChallengeManager manager;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text difficultyLabel;
        [SerializeField] private Text rewardLabel;
        [SerializeField] private Text bestScoreLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button cancelButton;

        private OpenWorldChallengeManager Manager => manager != null ? manager : OpenWorldChallengeManager.Instance;

        private void Awake()
        {
            startButton?.onClick.AddListener(OnStart);
            cancelButton?.onClick.AddListener(Hide);
            Hide();
        }

        private void OnEnable()
        {
            OpenWorldChallengeManager challengeManager = Manager;

            if (challengeManager == null)
            {
                return;
            }

            challengeManager.MarkerInRangeChanged += OnMarkerInRangeChanged;
            challengeManager.ChallengeStarted += OnChallengeStarted;
        }

        private void OnDisable()
        {
            OpenWorldChallengeManager challengeManager = Manager;

            if (challengeManager != null)
            {
                challengeManager.MarkerInRangeChanged -= OnMarkerInRangeChanged;
                challengeManager.ChallengeStarted -= OnChallengeStarted;
            }
        }

        private void OnChallengeStarted(ChallengeDefinition definition)
        {
            Hide();
        }

        private void OnMarkerInRangeChanged(ChallengeMarker marker)
        {
            if (marker == null)
            {
                Hide();
                return;
            }

            Show(marker.Definition);
        }

        private void Show(ChallengeDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }

            if (difficultyLabel != null)
            {
                difficultyLabel.text = new string('*', Mathf.Clamp(definition.Difficulty, 1, 5));
            }

            if (rewardLabel != null)
            {
                rewardLabel.text = $"{definition.CoinReward} Coins";
            }

            if (bestScoreLabel != null)
            {
                ChallengeSaveEntry entry = SaveManager.Data.FindChallenge(definition.ChallengeId);
                bestScoreLabel.text = entry != null && entry.completed ? $"Best {entry.bestScore}" : string.Empty;
            }

            if (panel != null)
            {
                panel.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnStart()
        {
            Manager?.StartNearbyChallenge();
            Hide();
        }
    }
}
