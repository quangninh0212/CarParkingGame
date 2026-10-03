using CarParkingGame.Core;
using CarParkingGame.Missions;
using CarParkingGame.Parking;
using CarParkingGame.Vehicle;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.UI
{
    // Points at the bay the player has to reach, with the distance to it.
    //
    // Two states, because one arrow cannot do both jobs: while the bay is off screen the
    // arrow sits on the edge of the screen pointing the way to turn, and once the bay is
    // on screen it parks over the bay itself. Without the on-screen case, the arrow pins
    // itself to a screen edge the moment you line up, which reads as "wrong way".
    public class BayGuideArrow : MonoBehaviour
    {
        [SerializeField] private MissionManager missions;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Text distanceLabel;
        [SerializeField] private Camera worldCamera;

        [Tooltip("How far in from the screen edge the arrow sits while the bay is off screen.")]
        [SerializeField] private float edgePadding = 110f;

        [Tooltip("Hide the arrow once the car is this close; at that point you can see the bay.")]
        [SerializeField] private float hideWithinMetres = 6f;

        [Tooltip("Show the arrow in these modes only. Practice drops you at the bay already.")]
        [SerializeField] private bool challengeOnly = true;

        private RectTransform canvasRect;

        private MissionManager Missions => missions != null ? missions : MissionManager.Instance;

        private void Awake()
        {
            canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (arrow == null || canvasRect == null)
            {
                SetVisible(false);
                return;
            }

            if (challengeOnly && (GameSession.Instance == null || GameSession.Instance.Mode != GameplayMode.Challenge))
            {
                SetVisible(false);
                return;
            }

            MissionManager runner = Missions;
            ParkingZone zone = runner != null && runner.IsRunning ? runner.ActiveZone : null;
            CarController car = ActiveVehicleLocator.Current;
            Camera view = ResolveCamera();

            if (zone == null || car == null || view == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 target = zone.WorldCenter;
            float distance = Vector3.Distance(car.transform.position, target);

            if (distance <= hideWithinMetres)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            Place(view, target);

            if (distanceLabel != null)
            {
                distanceLabel.text = $"{Mathf.RoundToInt(distance)} m";
            }
        }

        private void Place(Camera view, Vector3 target)
        {
            Vector3 viewport = view.WorldToViewportPoint(target);
            bool behind = viewport.z < 0f;

            // Behind the camera, WorldToViewportPoint mirrors the point through the origin,
            // so it has to be flipped before it means anything on screen.
            if (behind)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            Vector2 size = canvasRect.rect.size;
            var centred = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);

            Vector2 half = size * 0.5f - Vector2.one * edgePadding;
            bool offScreen = behind
                             || Mathf.Abs(centred.x) > half.x
                             || Mathf.Abs(centred.y) > half.y;

            if (offScreen)
            {
                // Push the direction out to the edge of the padded rectangle, keeping its
                // angle, so the arrow always sits on the border nearest the bay.
                Vector2 direction = centred.sqrMagnitude < 0.0001f ? Vector2.up : centred.normalized;
                float scale = Mathf.Min(
                    half.x / Mathf.Max(0.0001f, Mathf.Abs(direction.x)),
                    half.y / Mathf.Max(0.0001f, Mathf.Abs(direction.y)));

                centred = direction * scale;
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }
            else
            {
                arrow.localRotation = Quaternion.identity;
            }

            arrow.anchoredPosition = centred;
        }

        private Camera ResolveCamera()
        {
            if (worldCamera != null && worldCamera.isActiveAndEnabled)
            {
                return worldCamera;
            }

            worldCamera = Camera.main;
            return worldCamera;
        }

        private void SetVisible(bool visible)
        {
            if (arrow != null && arrow.gameObject.activeSelf != visible)
            {
                arrow.gameObject.SetActive(visible);
            }
        }
    }
}
