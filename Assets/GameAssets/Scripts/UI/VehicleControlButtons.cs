using CarParkingGame.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // The ring of car controls around the brake pedal: camera, headlights, horn and the
    // two indicators.
    //
    // They resolve the car the player is currently driving on each press rather than
    // being bound to one car in the Inspector, because the scene swaps between three cars
    // and a bound reference would end up pointing at a car that is switched off.
    //
    // Each button also reports its own state by tinting its icon, which a text caption
    // could not do: with the indicators blinking and the headlights latched, "is this on?"
    // is the question the player actually has.
    public class VehicleControlButtons : MonoBehaviour
    {
        [SerializeField] private Button headlightButton;
        [SerializeField] private Button hornButton;
        [SerializeField] private Button leftIndicatorButton;
        [SerializeField] private Button rightIndicatorButton;
        [SerializeField] private Button cameraButton;

        [Header("Icons")]
        [SerializeField] private Image headlightIcon;
        [SerializeField] private Image leftIndicatorIcon;
        [SerializeField] private Image rightIndicatorIcon;
        [SerializeField] private Text cameraModeLabel;

        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color activeColor = new Color(1f, 0.78f, 0.15f, 1f);

        private VehicleCameraDirector cameraDirector;
        private VehicleLights trackedLights;

        private void Awake()
        {
            if (headlightButton != null)
            {
                headlightButton.onClick.AddListener(ToggleHeadlights);
            }

            if (hornButton != null)
            {
                hornButton.onClick.AddListener(SoundHorn);
            }

            if (leftIndicatorButton != null)
            {
                leftIndicatorButton.onClick.AddListener(ToggleLeftIndicator);
            }

            if (rightIndicatorButton != null)
            {
                rightIndicatorButton.onClick.AddListener(ToggleRightIndicator);
            }

            if (cameraButton != null)
            {
                cameraButton.onClick.AddListener(CycleCamera);
            }
        }

        private void OnDisable()
        {
            Untrack();
        }

        private void Update()
        {
            TrackActiveCar();
            RefreshCameraLabel();
        }

        // ----- presses --------------------------------------------------------------------

        private void ToggleHeadlights()
        {
            FindOnActiveCar<VehicleLights>()?.ToggleHeadlights();
        }

        private void SoundHorn()
        {
            FindOnActiveCar<VehicleHorn>()?.Play();
        }

        private void ToggleLeftIndicator()
        {
            FindOnActiveCar<VehicleLights>()?.ToggleIndicator(IndicatorSide.Left);
        }

        private void ToggleRightIndicator()
        {
            FindOnActiveCar<VehicleLights>()?.ToggleIndicator(IndicatorSide.Right);
        }

        private void CycleCamera()
        {
            if (cameraDirector == null)
            {
                cameraDirector = FindFirstObjectByType<VehicleCameraDirector>();
            }

            cameraDirector?.CycleMode();
            RefreshCameraLabel();
        }

        // ----- state feedback -------------------------------------------------------------

        private void TrackActiveCar()
        {
            VehicleLights lights = FindOnActiveCar<VehicleLights>();

            if (lights == trackedLights)
            {
                return;
            }

            Untrack();
            trackedLights = lights;

            if (trackedLights != null)
            {
                trackedLights.StateChanged += RefreshLightState;
            }

            RefreshLightState();
        }

        private void Untrack()
        {
            if (trackedLights != null)
            {
                trackedLights.StateChanged -= RefreshLightState;
                trackedLights = null;
            }
        }

        private void RefreshLightState()
        {
            bool headlightsOn = trackedLights != null && trackedLights.HeadlightsOn;
            IndicatorSide side = trackedLights != null ? trackedLights.Indicator : IndicatorSide.None;

            Tint(headlightIcon, headlightsOn);
            Tint(leftIndicatorIcon, side == IndicatorSide.Left);
            Tint(rightIndicatorIcon, side == IndicatorSide.Right);
        }

        private void RefreshCameraLabel()
        {
            if (cameraModeLabel == null)
            {
                return;
            }

            if (cameraDirector == null)
            {
                cameraDirector = FindFirstObjectByType<VehicleCameraDirector>();
            }

            cameraModeLabel.text = cameraDirector != null ? cameraDirector.ModeCaption : string.Empty;
        }

        private void Tint(Image icon, bool active)
        {
            if (icon != null)
            {
                icon.color = active ? activeColor : idleColor;
            }
        }

        private static T FindOnActiveCar<T>() where T : Component
        {
            CarController car = ActiveVehicleLocator.Current;
            return car != null ? car.GetComponent<T>() : null;
        }
    }
}
