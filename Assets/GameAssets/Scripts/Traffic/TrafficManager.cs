using System.Collections.Generic;
using CarParkingGame.Settings;
using UnityEngine;

namespace CarParkingGame.Traffic
{
    // Spawns a fixed set of traffic cars once and reuses them - the pool never grows, so
    // there is no runtime instantiation during play. How many are active comes from the
    // player's graphics setting, and changing that setting adjusts the count live.
    public class TrafficManager : MonoBehaviour
    {
        [SerializeField] private TrafficVehicle[] vehiclePrefabs;
        [SerializeField] private List<WaypointPath> paths = new List<WaypointPath>();

        [Tooltip("Hard ceiling regardless of graphics setting, so a profile cannot ask for more than the scene can afford.")]
        [SerializeField] private int maximumVehicles = 15;

        [SerializeField] private Transform poolParent;

        private readonly List<TrafficVehicle> pool = new List<TrafficVehicle>();
        private int activeCount;

        private void Start()
        {
            if (vehiclePrefabs == null || vehiclePrefabs.Length == 0 || !HasUsablePath())
            {
                Debug.LogWarning($"[TrafficManager] '{name}' has no vehicle prefabs or no usable paths; traffic is disabled.", this);
                enabled = false;
                return;
            }

            BuildPool();
            ApplyQualitySetting();

            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied += ApplyQualitySetting;
            }
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied -= ApplyQualitySetting;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < activeCount; i++)
            {
                pool[i].Tick(deltaTime);
            }
        }

        public void ApplyQualitySetting()
        {
            int wanted = SettingsManager.Instance != null
                ? SettingsManager.Instance.CurrentTierSettings.trafficVehicleCount
                : maximumVehicles;

            SetActiveCount(Mathf.Clamp(wanted, 0, pool.Count));
        }

        private void SetActiveCount(int count)
        {
            activeCount = count;

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(i < count);
            }
        }

        private void BuildPool()
        {
            int capacity = Mathf.Min(maximumVehicles, 30);

            for (int i = 0; i < capacity; i++)
            {
                TrafficVehicle prefab = vehiclePrefabs[i % vehiclePrefabs.Length];
                TrafficVehicle vehicle = Instantiate(prefab, poolParent != null ? poolParent : transform);

                PlaceOnPath(vehicle, i, capacity);
                pool.Add(vehicle);
            }
        }

        // Spread the cars out along the available paths so they do not all start nose to
        // tail at the first waypoint.
        private void PlaceOnPath(TrafficVehicle vehicle, int slot, int totalSlots)
        {
            WaypointPath path = paths[slot % paths.Count];

            if (path == null || !path.IsUsable)
            {
                path = FirstUsablePath();
            }

            int spacing = Mathf.Max(1, path.Count / Mathf.Max(1, totalSlots / Mathf.Max(1, paths.Count)));
            int startIndex = slot * spacing % path.Count;

            vehicle.Initialize(path, startIndex, slot * 0.037f);
        }

        private WaypointPath FirstUsablePath()
        {
            foreach (WaypointPath path in paths)
            {
                if (path != null && path.IsUsable)
                {
                    return path;
                }
            }

            return null;
        }

        private bool HasUsablePath()
        {
            return FirstUsablePath() != null;
        }
    }
}
