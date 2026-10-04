using CarParkingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.Vehicle
{
    // The car's three mirrors - door mirror, rear-view, door mirror - drawn across the top
    // of the screen while the player is sitting in the driver's seat.
    //
    // Each one is a real render rather than a picture, so they show what is actually back
    // there: the wall being reversed towards, the bay lines, the car in the next space.
    // Every image is flipped left for right, which is the difference between a mirror and
    // a camera pointed backwards - a car over your right shoulder belongs on the right of
    // the glass.
    //
    // They only appear in the seated view. From a chase camera the player can already see
    // behind the car, mirrors floating over that view would be nonsense, and three extra
    // cameras are not worth rendering for it.
    //
    // The cameras are built here rather than placed in the scene, because the car they
    // attach to is chosen in the garage and swapped at runtime.
    public class RearViewMirror : MonoBehaviour
    {
        [SerializeField] private VehicleCameraDirector director;
        [SerializeField] private GameObject frame;

        [SerializeField] private RawImage centreGlass;
        [SerializeField] private RawImage leftGlass;
        [SerializeField] private RawImage rightGlass;

        [Header("Rear-view mirror")]
        [Tooltip("Lifted off the measured rear-view point so the glass sees over the back of the car rather than into it.")]
        [SerializeField] private Vector3 centreOffset = new Vector3(0f, 0.22f, 0f);
        [SerializeField] private float centreFieldOfView = 30f;
        [SerializeField] private Vector2Int centreResolution = new Vector2Int(448, 224);

        [Header("Door mirrors")]
        [Tooltip("Degrees each door mirror is turned outward from straight back.")]
        [SerializeField] private float doorMirrorSplay = 24f;

        [Tooltip("Height up the body the door mirrors sit at, as a fraction of its height from the floor.")]
        [SerializeField] private float doorMirrorHeight = 0.74f;

        [Tooltip("How far forward of the body's middle the door mirrors sit, as a fraction of its half length.")]
        [SerializeField] private float doorMirrorReach = 0.34f;

        [SerializeField] private float doorFieldOfView = 34f;
        [SerializeField] private Vector2Int doorResolution = new Vector2Int(256, 146);

        [Tooltip("Shorter than the main camera's: nothing a long way behind the car is worth the fill rate.")]
        [SerializeField] private float farClip = 160f;

        private readonly Glass[] mirrors = new Glass[3];
        private CarController attachedTo;

        private sealed class Glass
        {
            public Camera camera;
            public RenderTexture texture;
            public RawImage surface;
        }

        private void OnDisable() => Release();

        private void OnDestroy() => Release();

        private void LateUpdate()
        {
            CarController car = Seated ? ActiveVehicleLocator.Current : null;

            if (car == null)
            {
                Show(false);
                return;
            }

            if (car != attachedTo)
            {
                Attach(car);
            }

            Show(attachedTo != null);
        }

        // Only from the driver's seat, and only while there is a car to be sitting in.
        private bool Seated
        {
            get
            {
                GameSession session = GameSession.Instance;

                if (session != null && session.Mode == GameplayMode.None)
                {
                    return false;
                }

                // With no director to ask, the mirrors stay off rather than hang over a
                // chase camera they make no sense on.
                return director != null && director.Mode == VehicleCameraMode.Cockpit;
            }
        }

        private void Attach(CarController car)
        {
            Release();

            attachedTo = car;

            var points = car.GetComponent<VehicleViewPoints>();

            // Falls back to a point above and behind the middle of the car. A measured
            // view point is better, but a mirror that guesses beats no mirror.
            Vector3 centre = points != null ? points.RearViewLocalPosition : new Vector3(0f, 1.2f, -0.4f);

            mirrors[0] = Build(car, "RearViewCamera", centre + centreOffset, 180f,
                centreFieldOfView, centreResolution, centreGlass);

            if (VehicleViewPoints.TryMeasureBodyBounds(car.transform, out Bounds body))
            {
                float x = body.extents.x * 1.02f;
                float y = body.min.y + body.size.y * doorMirrorHeight;
                float z = body.center.z + body.extents.z * doorMirrorReach;

                // Turned outward, so each one covers the lane beside the car rather than
                // repeating what the rear-view mirror already shows.
                mirrors[1] = Build(car, "LeftMirrorCamera", new Vector3(body.center.x - x, y, z),
                    180f - doorMirrorSplay, doorFieldOfView, doorResolution, leftGlass);

                mirrors[2] = Build(car, "RightMirrorCamera", new Vector3(body.center.x + x, y, z),
                    180f + doorMirrorSplay, doorFieldOfView, doorResolution, rightGlass);
            }
        }

        private Glass Build(CarController car, string name, Vector3 localPosition, float yaw,
            float fieldOfView, Vector2Int resolution, RawImage surface)
        {
            if (surface == null)
            {
                return null;
            }

            var texture = new RenderTexture(Mathf.Max(32, resolution.x), Mathf.Max(32, resolution.y), 16)
            {
                name = name,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };

            var host = new GameObject(name);
            host.transform.SetParent(car.transform, false);
            host.transform.localPosition = localPosition;
            host.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var camera = host.AddComponent<Camera>();
            camera.targetTexture = texture;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = farClip;

            Camera main = Camera.main;

            if (main != null)
            {
                camera.clearFlags = main.clearFlags;
                camera.backgroundColor = main.backgroundColor;

                // The same world the player is looking at, minus the interface: the HUD is
                // drawn over the mirrors, not inside them.
                camera.cullingMask = main.cullingMask & ~(1 << LayerMask.NameToLayer("UI"));
            }

            surface.texture = texture;

            // Left for right. Without this a mirror shows the world the way a reversing
            // camera does, which is the opposite of what a driver reads.
            surface.uvRect = new Rect(1f, 0f, -1f, 1f);

            return new Glass { camera = camera, texture = texture, surface = surface };
        }

        private void Show(bool visible)
        {
            if (frame != null && frame.activeSelf != visible)
            {
                frame.SetActive(visible);
            }

            foreach (Glass glass in mirrors)
            {
                if (glass?.camera != null && glass.camera.enabled != visible)
                {
                    glass.camera.enabled = visible;
                }
            }
        }

        private void Release()
        {
            for (int i = 0; i < mirrors.Length; i++)
            {
                Glass glass = mirrors[i];

                if (glass == null)
                {
                    continue;
                }

                if (glass.camera != null)
                {
                    Destroy(glass.camera.gameObject);
                }

                if (glass.texture != null)
                {
                    glass.texture.Release();
                    Destroy(glass.texture);
                }

                if (glass.surface != null)
                {
                    glass.surface.texture = null;
                }

                mirrors[i] = null;
            }

            attachedTo = null;
        }
    }
}
