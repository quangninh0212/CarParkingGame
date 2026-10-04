using System.Collections.Generic;
using CarParkingGame.Parking;
using UnityEngine;

namespace CarParkingGame.Missions
{
    // The scene half of a mission: where the car starts, which bay to park in, and
    // which environment container belongs to this mission. Pairs with a
    // MissionDefinition asset that holds the tuning numbers.
    public class MissionAuthoring : MonoBehaviour
    {
        [SerializeField] private MissionDefinition definition;
        [SerializeField] private Transform startPoint;
        [SerializeField] private ParkingZone parkingZone;

        [Tooltip("The bays after the first, for missions that ask for more than one. The HUD counts them as P x/N and they are parked in this order.")]
        [SerializeField] private ParkingZone[] extraZones = new ParkingZone[0];
        [SerializeField] private GameObject environmentContainer;

        [Tooltip("Optional. When set, leaving this volume fails the mission.")]
        [SerializeField] private Collider allowedArea;

        public MissionDefinition Definition => definition;
        public Transform StartPoint => startPoint;
        public ParkingZone ParkingZone => parkingZone;

        // How many bays this mission asks for, and which one is the nth. Missions authored
        // before there was more than one bay have no extras and answer 1, so nothing that
        // reads ParkingZone on its own has to change.
        public int BayCount
        {
            get
            {
                int count = parkingZone != null ? 1 : 0;

                for (int i = 0; extraZones != null && i < extraZones.Length; i++)
                {
                    if (extraZones[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public ParkingZone GetBay(int index)
        {
            if (index <= 0)
            {
                return parkingZone;
            }

            int seen = 0;

            for (int i = 0; extraZones != null && i < extraZones.Length; i++)
            {
                if (extraZones[i] == null)
                {
                    continue;
                }

                if (++seen == index)
                {
                    return extraZones[i];
                }
            }

            return parkingZone;
        }
        public GameObject EnvironmentContainer => environmentContainer;
        public Collider AllowedArea => allowedArea;

        public int MissionId => definition != null ? definition.MissionId : 0;

        public void SetEnvironmentActive(bool active)
        {
            if (environmentContainer != null)
            {
                environmentContainer.SetActive(active);
            }
        }

        public bool IsInsideAllowedArea(Vector3 worldPosition)
        {
            if (allowedArea == null)
            {
                return true;
            }

            return allowedArea.bounds.Contains(worldPosition);
        }

#if UNITY_EDITOR
        // Used by MissionSceneSetupTool so the runtime API needs no setters.
        public void EditorAssign(
            MissionDefinition missionDefinition,
            Transform missionStartPoint,
            ParkingZone zone,
            GameObject environment)
        {
            definition = missionDefinition;
            startPoint = missionStartPoint;
            parkingZone = zone;
            environmentContainer = environment;
        }

        public void EditorAssignExtraBays(ParkingZone[] bays)
        {
            extraZones = bays ?? new ParkingZone[0];
        }
#endif

        public void CollectIssues(List<string> issues)
        {
            string label = $"'{name}'";

            if (definition == null)
            {
                issues.Add($"{label} has no MissionDefinition assigned.");
            }
            else if (definition.MissionId <= 0)
            {
                issues.Add($"{label} references a definition with an invalid mission id ({definition.MissionId}).");
            }
            else if (definition.CoinReward < 0)
            {
                issues.Add($"{label} references a definition with a negative coin reward.");
            }
            else if (definition.AngleToleranceDegrees <= 0f || definition.AngleToleranceDegrees > 90f)
            {
                issues.Add($"{label} has an angle tolerance outside a usable range ({definition.AngleToleranceDegrees} degrees).");
            }

            if (startPoint == null)
            {
                issues.Add($"{label} has no start point.");
            }

            if (parkingZone == null)
            {
                issues.Add($"{label} has no parking zone.");
            }

            if (environmentContainer == null)
            {
                issues.Add($"{label} has no environment container (mission props will not be toggled).");
            }
        }

        private void OnDrawGizmos()
        {
            if (startPoint != null)
            {
                Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
                Gizmos.DrawSphere(startPoint.position, 0.4f);
                Gizmos.DrawLine(startPoint.position, startPoint.position + startPoint.forward * 2.5f);
            }

            if (startPoint != null && parkingZone != null)
            {
                Gizmos.color = new Color(1f, 1f, 0.3f, 0.5f);
                Gizmos.DrawLine(startPoint.position, parkingZone.WorldCenter);
            }
        }
    }
}
