using CarParkingGame.Core;
using CarParkingGame.Story;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The opening: who the player is and why they are parking cars.
    //
    // Shown once, on the first run, and afterwards only when the player asks for it from
    // the home screen. A story screen that appears every launch is a story screen that
    // gets skipped every launch.
    public class StoryIntroView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text bodyLabel;
        [SerializeField] private Button continueButton;

        [Tooltip("Optional. The home screen button that opens the story again.")]
        [SerializeField] private Button openButton;

        private void Awake()
        {
            continueButton?.onClick.AddListener(Close);
            openButton?.onClick.AddListener(Open);
            Fill();
            SetPanel(false);
        }

        private void Start()
        {
            // First run only. Checked in Start rather than Awake so the save is loaded.
            if (SaveManager.Data != null && !SaveManager.Data.prologueSeen)
            {
                Open();
            }
        }

        public void Open()
        {
            Fill();
            SetPanel(true);
        }

        private void Fill()
        {
            if (titleLabel != null)
            {
                titleLabel.text = StoryLibrary.PrologueTitle;
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = StoryLibrary.PrologueText;
            }
        }

        private void Close()
        {
            SetPanel(false);

            if (SaveManager.Data == null || SaveManager.Data.prologueSeen)
            {
                return;
            }

            SaveManager.Data.prologueSeen = true;
            SaveManager.Save();
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
