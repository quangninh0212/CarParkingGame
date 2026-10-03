using System;
using UnityEngine;

namespace CarParkingGame.Settings
{
    public enum GraphicsTier
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    // What a graphics tier means in game terms. Traffic and pedestrian counts live here
    // too, so the systems added later read their budget from the player's setting instead
    // of hardcoding it.
    [Serializable]
    public class GraphicsTierSettings
    {
        public bool shadowsEnabled = true;
        public bool softShadows;
        public float shadowDistance = 35f;
        public ShadowResolution shadowResolution = ShadowResolution.Low;
        public float lodBias = 1f;
        public bool realtimeRearMirror;
        public bool realtimeSideMirrors;
        public int trafficVehicleCount = 4;
        public int pedestrianCount;
    }

    [CreateAssetMenu(fileName = "QualityProfile", menuName = "Car Parking/Quality Profile")]
    public class QualityProfile : ScriptableObject
    {
        [SerializeField]
        private GraphicsTierSettings low = new GraphicsTierSettings
        {
            shadowsEnabled = false,
            softShadows = false,
            shadowDistance = 20f,
            shadowResolution = ShadowResolution.Low,
            lodBias = 0.7f,
            realtimeRearMirror = false,
            realtimeSideMirrors = false,
            trafficVehicleCount = 4,
            pedestrianCount = 0
        };

        [SerializeField]
        private GraphicsTierSettings medium = new GraphicsTierSettings
        {
            shadowsEnabled = true,
            softShadows = false,
            shadowDistance = 35f,
            shadowResolution = ShadowResolution.Low,
            lodBias = 1f,
            realtimeRearMirror = true,
            realtimeSideMirrors = false,
            trafficVehicleCount = 8,
            pedestrianCount = 4
        };

        [SerializeField]
        private GraphicsTierSettings high = new GraphicsTierSettings
        {
            shadowsEnabled = true,
            softShadows = true,
            shadowDistance = 60f,
            shadowResolution = ShadowResolution.Medium,
            lodBias = 1.3f,
            realtimeRearMirror = true,
            realtimeSideMirrors = true,
            trafficVehicleCount = 12,
            pedestrianCount = 8
        };

        public static QualityProfile CreateDefault()
        {
            return CreateInstance<QualityProfile>();
        }

        public GraphicsTierSettings For(GraphicsTier tier)
        {
            switch (tier)
            {
                case GraphicsTier.Low:
                    return low;
                case GraphicsTier.High:
                    return high;
                default:
                    return medium;
            }
        }
    }
}
