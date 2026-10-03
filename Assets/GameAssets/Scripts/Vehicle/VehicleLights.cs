using System;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    public enum IndicatorSide
    {
        None,
        Left,
        Right
    }

    // One set of lights: glow meshes to show/hide plus optional real Lights.
    //
    // Toggling objects rather than pushing an emissive colour is deliberate - a
    // MaterialPropertyBlock cannot enable a shader's emission keyword, so on a material
    // with emission switched off, writing _EmissionColor would silently do nothing.
    [Serializable]
    public class LightGroup
    {
        public GameObject[] glowObjects;
        public Light[] lights;

        public void SetState(bool on)
        {
            if (glowObjects != null)
            {
                foreach (GameObject glow in glowObjects)
                {
                    if (glow != null)
                    {
                        glow.SetActive(on);
                    }
                }
            }

            if (lights == null)
            {
                return;
            }

            foreach (Light light in lights)
            {
                if (light != null)
                {
                    light.enabled = on;
                }
            }
        }
    }

    public class VehicleLights : MonoBehaviour
    {
        [SerializeField] private CarController car;
        [SerializeField] private LightGroup headlights = new LightGroup();
        [SerializeField] private LightGroup brakeLights = new LightGroup();
        [SerializeField] private LightGroup leftIndicator = new LightGroup();
        [SerializeField] private LightGroup rightIndicator = new LightGroup();
        [SerializeField] private float blinkInterval = 0.45f;

        [Tooltip("Deceleration in km/h per second that counts as braking hard enough to light the brake lights.")]
        [SerializeField] private float decelerationThreshold = 12f;

        private bool headlightsOn;
        private bool indicatorVisible;
        private float blinkTimer;
        private float lastSpeedKmh;
        private bool brakeLightsOn;
        private IndicatorSide indicator = IndicatorSide.None;

        public bool HeadlightsOn => headlightsOn;
        public IndicatorSide Indicator => indicator;

        public event Action StateChanged;

        private void Awake()
        {
            if (car == null)
            {
                car = GetComponent<CarController>();
            }

            headlights.SetState(false);
            brakeLights.SetState(false);
            leftIndicator.SetState(false);
            rightIndicator.SetState(false);
        }

        public void ToggleHeadlights()
        {
            SetHeadlights(!headlightsOn);
        }

        public void SetHeadlights(bool on)
        {
            headlightsOn = on;
            headlights.SetState(on);
            StateChanged?.Invoke();
        }

        // Pressing the same side again cancels it; the opposite side replaces it.
        public void ToggleIndicator(IndicatorSide side)
        {
            indicator = indicator == side ? IndicatorSide.None : side;

            indicatorVisible = false;
            blinkTimer = 0f;
            leftIndicator.SetState(false);
            rightIndicator.SetState(false);

            StateChanged?.Invoke();
        }

        public void ToggleLeftIndicator()
        {
            ToggleIndicator(IndicatorSide.Left);
        }

        public void ToggleRightIndicator()
        {
            ToggleIndicator(IndicatorSide.Right);
        }

        private void Update()
        {
            UpdateBrakeLights();
            UpdateIndicators();
        }

        private void UpdateBrakeLights()
        {
            if (car == null)
            {
                return;
            }

            float speedKmh = car.CurrentSpeedKmh;
            float deceleration = Time.deltaTime > 0f ? (lastSpeedKmh - speedKmh) / Time.deltaTime : 0f;
            lastSpeedKmh = speedKmh;

            bool shouldLight = car.IsBraking || deceleration > decelerationThreshold;

            if (shouldLight == brakeLightsOn)
            {
                return;
            }

            brakeLightsOn = shouldLight;
            brakeLights.SetState(shouldLight);
        }

        private void UpdateIndicators()
        {
            if (indicator == IndicatorSide.None)
            {
                return;
            }

            blinkTimer += Time.deltaTime;

            if (blinkTimer < blinkInterval)
            {
                return;
            }

            blinkTimer = 0f;
            indicatorVisible = !indicatorVisible;

            leftIndicator.SetState(indicator == IndicatorSide.Left && indicatorVisible);
            rightIndicator.SetState(indicator == IndicatorSide.Right && indicatorVisible);
        }
    }
}
