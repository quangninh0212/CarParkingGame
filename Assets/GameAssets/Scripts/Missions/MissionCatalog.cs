using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Missions
{
    // The ordered list of every mission that exists as data, independent of which
    // missions are currently built in the scene. The selection UI reads this, so the
    // full ladder can be shown (and locked) before every layout has been authored.
    [CreateAssetMenu(fileName = "MissionCatalog", menuName = "Car Parking/Mission Catalog")]
    public class MissionCatalog : ScriptableObject
    {
        [SerializeField] private List<MissionDefinition> missions = new List<MissionDefinition>();

        public IReadOnlyList<MissionDefinition> Missions => missions;

        public MissionDefinition Find(int missionId)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions[i] != null && missions[i].MissionId == missionId)
                {
                    return missions[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorSetMissions(List<MissionDefinition> orderedMissions)
        {
            missions = orderedMissions;
        }
#endif
    }
}
