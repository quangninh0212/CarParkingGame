using System.Collections.Generic;
using CarParkingGame.Settings;
using UnityEngine;

namespace CarParkingGame.Traffic
{
    // Same fixed-pool approach as TrafficManager. On Low quality the pedestrian budget is
    // zero, so every pedestrian is simply switched off - the manager costs nothing then.
    public class PedestrianManager : MonoBehaviour
    {
        [SerializeField] private Pedestrian[] pedestrianPrefabs;
        [SerializeField] private List<WaypointPath> paths = new List<WaypointPath>();

        [Tooltip("Hard ceiling regardless of graphics setting.")]
        [SerializeField] private int maximumPedestrians = 10;

        [SerializeField] private Transform poolParent;

        private readonly List<Pedestrian> pool = new List<Pedestrian>();
        private int activeCount;

        private void Start()
        {
            if (pedestrianPrefabs == null || pedestrianPrefabs.Length == 0 || FirstUsablePath() == null)
            {
                Debug.LogWarning($"[PedestrianManager] '{name}' has no pedestrian prefabs or no usable paths; pedestrians are disabled.", this);
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
            if (activeCount == 0)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < activeCount; i++)
            {
                pool[i].Tick(deltaTime);
            }
        }

        public void ApplyQualitySetting()
        {
            int wanted = SettingsManager.Instance != null
                ? SettingsManager.Instance.CurrentTierSettings.pedestrianCount
                : 0;

            activeCount = Mathf.Clamp(wanted, 0, pool.Count);

            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(i < activeCount);
            }
        }

        private void BuildPool()
        {
            int capacity = Mathf.Clamp(maximumPedestrians, 0, 20);

            for (int i = 0; i < capacity; i++)
            {
                Pedestrian prefab = pedestrianPrefabs[i % pedestrianPrefabs.Length];
                Pedestrian pedestrian = Instantiate(prefab, poolParent != null ? poolParent : transform);

                WaypointPath path = paths[i % paths.Count];

                if (path == null || !path.IsUsable)
                {
                    path = FirstUsablePath();
                }

                pedestrian.Initialize(path, i * 2 % path.Count, i * 0.041f);
                pool.Add(pedestrian);
            }
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
    }
}
