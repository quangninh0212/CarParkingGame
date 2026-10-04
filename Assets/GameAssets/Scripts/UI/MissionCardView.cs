using System;
using CarParkingGame.Missions;
using CarParkingGame.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // One mission entry in the selection screen. Uses legacy uGUI Text to match the
    // rest of the project's UI.
    public class MissionCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text numberLabel;
        [SerializeField] private Text nameLabel;
        [SerializeField] private Text bestScoreLabel;
        [SerializeField] private Text difficultyLabel;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Color earnedStarColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color emptyStarColor = new Color(1f, 1f, 1f, 0.2f);

        private int missionId;
        private Action<int> selectedCallback;

        public int MissionId => missionId;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(OnClick);
            }
        }

        public void Bind(MissionDefinition definition, MissionProgressData progress, bool playable, Action<int> onSelected)
        {
            missionId = definition.MissionId;
            selectedCallback = onSelected;

            bool unlocked = MissionUnlocking.IsOpen(definition.MissionId, progress);

            int stars = progress != null ? progress.bestStars : 0;
            int bestScore = progress != null ? progress.bestScore : 0;

            if (numberLabel != null)
            {
                numberLabel.text = definition.MissionId.ToString("00");
            }

            if (nameLabel != null)
            {
                nameLabel.text = definition.DisplayName;
            }

            if (difficultyLabel != null)
            {
                difficultyLabel.text = new string('*', Mathf.Clamp(definition.Difficulty, 1, 5));
            }

            if (bestScoreLabel != null)
            {
                bestScoreLabel.text = bestScore > 0 ? $"Best {bestScore}" : string.Empty;
            }

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!unlocked);
            }

            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].color = i < stars ? earnedStarColor : emptyStarColor;
                    }
                }
            }

            if (button != null)
            {
                button.interactable = unlocked && playable;
            }
        }

        private void OnClick()
        {
            selectedCallback?.Invoke(missionId);
        }
    }
}
