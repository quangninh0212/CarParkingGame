using CarParkingGame.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    public class SpeedometerView : MonoBehaviour
    {
        [SerializeField] private Text speedLabel;
        [SerializeField] private Text unitLabel;

        [Tooltip("Optional radial or bar fill driven by speed.")]
        [SerializeField] private Image fill;

        [SerializeField] private float maxDisplaySpeedKmh = 120f;
        [SerializeField] private float smoothing = 8f;

        private float displayedSpeed;

        private void OnEnable()
        {
            if (unitLabel != null)
            {
                unitLabel.text = "km/h";
            }
        }

        private void Update()
        {
            CarController car = ActiveVehicleLocator.Current;
            float target = car != null ? car.CurrentSpeedKmh : 0f;

            displayedSpeed = smoothing > 0f
                ? Mathf.Lerp(displayedSpeed, target, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime))
                : target;

            if (speedLabel != null)
            {
                speedLabel.text = Mathf.RoundToInt(displayedSpeed).ToString();
            }

            if (fill != null && maxDisplaySpeedKmh > 0f)
            {
                fill.fillAmount = Mathf.Clamp01(displayedSpeed / maxDisplaySpeedKmh);
            }
        }
    }
}
