using CarParkingGame.OpenWorld;
using CarParkingGame.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Lightweight navigation: an arrow pointing at the nearest challenge plus the distance
    // to it. Deliberately not a minimap - a second camera rendering the world is not worth
    // the frame budget on the devices this game targets.
    public class ChallengeCompassView : MonoBehaviour
    {
        [SerializeField] private OpenWorldChallengeManager manager;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Text distanceLabel;
        [SerializeField] private GameObject container;
        [SerializeField] private float refreshInterval = 0.2f;

        private Transform cameraTransform;
        private float nextRefreshTime;

        private OpenWorldChallengeManager Manager => manager != null ? manager : OpenWorldChallengeManager.Instance;

        private void Update()
        {
            if (Time.time < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.time + refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            OpenWorldChallengeManager challengeManager = Manager;
            CarController car = ActiveVehicleLocator.Current;

            if (challengeManager == null || car == null || challengeManager.IsRunning)
            {
                SetVisible(false);
                return;
            }

            ChallengeMarker nearest = FindNearest(challengeManager, car.transform.position);

            if (nearest == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            Vector3 toTarget = nearest.transform.position - car.transform.position;
            float distance = toTarget.magnitude;
            toTarget.y = 0f;

            if (distanceLabel != null)
            {
                distanceLabel.text = distance >= 1000f
                    ? $"{distance / 1000f:0.0} km"
                    : $"{Mathf.RoundToInt(distance)} m";
            }

            if (arrow == null)
            {
                return;
            }

            Vector3 reference = GetCameraForward(car.transform.forward);
            float bearing = Vector3.SignedAngle(reference, toTarget.normalized, Vector3.up);
            arrow.localRotation = Quaternion.Euler(0f, 0f, -bearing);
        }

        private Vector3 GetCameraForward(Vector3 fallback)
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform == null)
            {
                return fallback;
            }

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;

            return forward.sqrMagnitude < 0.0001f ? fallback : forward.normalized;
        }

        private static ChallengeMarker FindNearest(OpenWorldChallengeManager challengeManager, Vector3 position)
        {
            ChallengeMarker nearest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < challengeManager.Markers.Count; i++)
            {
                ChallengeMarker marker = challengeManager.Markers[i];
                float distance = marker.DistanceTo(position);

                if (distance < nearestDistance)
                {
                    nearest = marker;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void SetVisible(bool visible)
        {
            if (container != null)
            {
                container.SetActive(visible);
            }
        }
    }
}
