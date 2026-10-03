using CarParkingGame.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // HUD buttons for the car's own controls. They resolve the car the player is currently
    // driving on each press rather than being bound to one car in the Inspector, because
    // the scene swaps between three cars and a bound reference would end up pointing at a
    // car that is switched off.
    public class VehicleControlButtons : MonoBehaviour
    {
        [SerializeField] private Button headlightButton;
        [SerializeField] private Button hornButton;
        [SerializeField] private Button leftIndicatorButton;
        [SerializeField] private Button rightIndicatorButton;
        [SerializeField] private Button cameraButton;

        private VehicleCameraDirector cameraDirector;

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

        private void ToggleHeadlights()
        {
            VehicleLights lights = FindOnActiveCar<VehicleLights>();

            if (lights != null)
            {
                lights.ToggleHeadlights();
            }
        }

        private void SoundHorn()
        {
            VehicleHorn horn = FindOnActiveCar<VehicleHorn>();

            if (horn != null)
            {
                horn.Play();
            }
        }

        private void ToggleLeftIndicator()
        {
            VehicleLights lights = FindOnActiveCar<VehicleLights>();

            if (lights != null)
            {
                lights.ToggleIndicator(IndicatorSide.Left);
            }
        }

        private void ToggleRightIndicator()
        {
            VehicleLights lights = FindOnActiveCar<VehicleLights>();

            if (lights != null)
            {
                lights.ToggleIndicator(IndicatorSide.Right);
            }
        }

        private void CycleCamera()
        {
            if (cameraDirector == null)
            {
                cameraDirector = FindFirstObjectByType<VehicleCameraDirector>();
            }

            if (cameraDirector != null)
            {
                cameraDirector.CycleMode();
            }
        }

        private static T FindOnActiveCar<T>() where T : Component
        {
            CarController car = ActiveVehicleLocator.Current;
            return car != null ? car.GetComponent<T>() : null;
        }
    }
}
