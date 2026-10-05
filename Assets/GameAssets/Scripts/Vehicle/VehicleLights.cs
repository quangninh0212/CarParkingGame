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
    // A lamp surface on the car's own bodywork: one renderer and one of its material
    // slots. The cars come with their materials named - Mc_FrontLights, K_RearLights,
    // B_FlickerLights - so the headlight, the tail light and the indicator are each a real
    // part of the model rather than something that has to be added to it.
    [Serializable]
    public class LampSurface
    {
        public Renderer renderer;
        public int materialIndex;
    }

    [Serializable]
    public class LightGroup
    {
        public GameObject[] glowObjects;
        public Light[] lights;

        // The car's own lamp lenses, brightened when the lamp is on and put back to the
        // colour the artist gave them when it is off.
        //
        // This replaced a set of boxes stuck on the nose and tail. Those read as blocks
        // bolted to the car rather than as lamps, which is what a player called them, and
        // no size or shape fixes that while they sit outside the bodywork. Lighting the
        // lens the model already has cannot look stuck on, because it is not.
        public LampSurface[] surfaces;
        public Color litColor = Color.white;

        private MaterialPropertyBlock block;
        private Color[] resting;

        public void SetState(bool on)
        {
            ApplySurfaces(on);

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

        private void ApplySurfaces(bool on)
        {
            if (surfaces == null || surfaces.Length == 0)
            {
                return;
            }

            int baseColor = Shader.PropertyToID("_BaseColor");
            int emission = Shader.PropertyToID("_EmissionColor");

            block ??= new MaterialPropertyBlock();
            resting ??= CaptureRestingColours(baseColor);

            for (int i = 0; i < surfaces.Length; i++)
            {
                LampSurface surface = surfaces[i];

                if (surface?.renderer == null)
                {
                    continue;
                }

                // Per submesh. Without the index this reaches every material on the
                // renderer, which on these cars is the whole car.
                surface.renderer.GetPropertyBlock(block, surface.materialIndex);

                block.SetColor(baseColor, on ? litColor : resting[i]);

                // Set as well as the base colour, not instead of it. A property block
                // cannot switch a shader's emission keyword on, so on a material without
                // emission this does nothing and the base colour carries the whole effect;
                // on one with it, the lamp glows as well.
                block.SetColor(emission, on ? litColor : Color.black);

                surface.renderer.SetPropertyBlock(block, surface.materialIndex);
            }
        }

        // What the lenses look like switched off, read from the materials themselves so
        // nothing has to be written down twice and a repainted car still turns its lamps
        // back to the right colour.
        private Color[] CaptureRestingColours(int baseColor)
        {
            var colours = new Color[surfaces.Length];

            for (int i = 0; i < surfaces.Length; i++)
            {
                LampSurface surface = surfaces[i];
                colours[i] = Color.white;

                if (surface?.renderer == null)
                {
                    continue;
                }

                Material[] materials = surface.renderer.sharedMaterials;

                if (surface.materialIndex < 0 || surface.materialIndex >= materials.Length)
                {
                    continue;
                }

                Material material = materials[surface.materialIndex];

                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty(baseColor))
                {
                    colours[i] = material.GetColor(baseColor);
                }
                else if (material.HasProperty("_Color"))
                {
                    colours[i] = material.color;
                }
            }

            return colours;
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

            // The pedal, not IsBraking. The car holds its brakes whenever there is no
            // throttle, so on IsBraking the tail lights were lit the whole time the car sat
            // still - two red blocks glowing behind a parked car.
            bool shouldLight = car.IsBrakeHeld || deceleration > decelerationThreshold;

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

            // A car with no park lamp indicates on its tail lamps, which means the
            // indicator and the brake can be driving the same lens. The indicator wins
            // while it is lit; when it blinks off, the brake has to be told again, or
            // signalling would leave the brake lights dark.
            if (!indicatorVisible)
            {
                brakeLights.SetState(brakeLightsOn);
            }
        }
    }
}
