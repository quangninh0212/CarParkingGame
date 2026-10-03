using System.Collections.Generic;
using CarParkingGame.Core;
using CarParkingGame.Missions;
using CarParkingGame.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Builds the mission list from the catalog instead of eight hand-placed buttons,
    // so growing to thirty missions is a data change rather than a UI change.
    public class MissionSelectionView : MonoBehaviour
    {
        [SerializeField] private MissionCatalog catalog;
        [SerializeField] private MissionCardView cardPrefab;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private Text coinLabel;
        [SerializeField] private GameObject emptyStateMessage;

        private readonly List<MissionCardView> cards = new List<MissionCardView>();

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (catalog == null || cardPrefab == null || cardContainer == null)
            {
                Debug.LogError("[MissionSelectionView] Needs a catalog, a card prefab and a container before it can build the mission list.", this);
                return;
            }

            SaveData save = SaveManager.Data;
            IReadOnlyList<MissionDefinition> missions = catalog.Missions;

            EnsureCardCount(missions.Count);

            for (int i = 0; i < cards.Count; i++)
            {
                bool used = i < missions.Count;
                cards[i].gameObject.SetActive(used);

                if (!used)
                {
                    continue;
                }

                MissionDefinition definition = missions[i];

                cards[i].Bind(
                    definition,
                    save.FindMission(definition.MissionId),
                    IsPlayable(definition.MissionId),
                    OnMissionSelected);
            }

            if (coinLabel != null)
            {
                coinLabel.text = save.coins.ToString();
            }

            if (emptyStateMessage != null)
            {
                emptyStateMessage.SetActive(missions.Count == 0);
            }
        }

        // A mission that exists as data but has no layout in the scene yet stays
        // visible and locked rather than throwing when tapped.
        private static bool IsPlayable(int missionId)
        {
            MissionManager manager = MissionManager.Instance;
            return manager == null || manager.GetMission(missionId) != null;
        }

        private void EnsureCardCount(int required)
        {
            while (cards.Count < required)
            {
                cards.Add(Instantiate(cardPrefab, cardContainer));
            }
        }

        private void OnMissionSelected(int missionId)
        {
            MissionManager manager = MissionManager.Instance;

            if (manager == null)
            {
                Debug.LogError("[MissionSelectionView] No MissionManager in the scene; mission could not be started.");
                return;
            }

            if (manager.StartMission(missionId))
            {
                gameObject.SetActive(false);
                LeaveMenu();
            }
        }

        // The list is opened from the legacy main menu, which pauses with
        // Time.timeScale = 0 and hides the driving controls. Without handing back to
        // it, a mission would start with the car frozen behind the menu.
        private void LeaveMenu()
        {
            MainMenuManager menu = FindFirstObjectByType<MainMenuManager>();

            if (menu == null)
            {
                Time.timeScale = 1f;
                return;
            }

            menu.ResumeGame();

            // ResumeGame shows the legacy GameManager's mission caption, which names
            // the legacy mission rather than the one just started.
            if (menu.missionInformation != null)
            {
                menu.missionInformation.SetActive(false);
            }
        }
    }
}
