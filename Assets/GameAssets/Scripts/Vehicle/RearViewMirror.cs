using CarParkingGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CarParkingGame.Vehicle
{
    // The rear-view mirror: a second camera behind the driver, drawn into the strip of
    // glass at the top of the screen.
    //
    // It is a real render rather than a picture, so it shows what is actually back there -
    // the wall the car is reversing towards, the bay lines, the car in the next space. The
    // image is flipped left for right, because that is the difference between a mirror and
    // a camera pointed backwards: a car over your right shoulder belongs on the right of
    // the glass.
    //
    // The camera is built here rather than put in the scene, because the car it has to be
    // attached to is chosen in the garage and swapped at runtime.
    public class RearViewMirror : MonoBehaviour
    {
        [SerializeField] private RawImage glass;
        [SerializeField] private GameObject frame;

        [Tooltip("Pixels across the mirror texture. A mirror is a glance, not a view, so this is deliberately small.")]
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 160;

        [Tooltip("Vertical field of view. The texture is wide, so the horizontal angle comes out far larger than this.")]
        [SerializeField] private float fieldOfView = 30f;

        [Tooltip("Lifted off the measured rear-view point so the glass sees over the back of the car rather than into it.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.22f, 0f);

        [Tooltip("Shorter than the main camera's: nothing a long way behind the car is worth the fill rate.")]
        [SerializeField] private float farClip = 160f;

        private Camera mirrorCamera;
        private RenderTexture texture;
        private CarController attachedTo;

        private void OnDisable()
        {
            Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void LateUpdate()
        {
            CarController car = Driving ? ActiveVehicleLocator.Current : null;

            if (car == null)
            {
                Show(false);
                return;
            }

            if (car != attachedTo)
            {
                Attach(car);
            }

            Show(mirrorCamera != null);
        }

        // Only while there is a car to be behind. In the menu the showroom is on screen
        // and there is nothing to look back at, and a second camera rendering over it
        // would cost frames for an empty strip of glass.
        private bool Driving
        {
            get
            {
                GameSession session = GameSession.Instance;
                return session == null || session.Mode != GameplayMode.None;
            }
        }

        private void Attach(CarController car)
        {
            Release();

            attachedTo = car;

            var points = car.GetComponent<VehicleViewPoints>();

            // Falls back to a point above and behind the middle of the car. Measured
            // view points are better, but a mirror that guesses beats no mirror.
            Vector3 local = points != null ? points.RearViewLocalPosition : new Vector3(0f, 1.2f, -0.4f);

            texture = new RenderTexture(textureWidth, textureHeight, 16)
            {
                name = "RearViewMirror",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };

            var host = new GameObject("RearViewCamera");
            host.transform.SetParent(car.transform, false);
            host.transform.localPosition = local + offset;
            host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            mirrorCamera = host.AddComponent<Camera>();
            mirrorCamera.targetTexture = texture;
            mirrorCamera.fieldOfView = fieldOfView;
            mirrorCamera.nearClipPlane = 0.08f;
            mirrorCamera.farClipPlane = farClip;

            Camera main = Camera.main;

            if (main != null)
            {
                mirrorCamera.clearFlags = main.clearFlags;
                mirrorCamera.backgroundColor = main.backgroundColor;

                // The same world the player is looking at, minus the interface: the HUD is
                // drawn over the mirror, not inside it.
                mirrorCamera.cullingMask = main.cullingMask & ~(1 << LayerMask.NameToLayer("UI"));
            }

            if (glass != null)
            {
                glass.texture = texture;

                // Left for right. Without this the mirror shows the world the way a
                // reversing camera does, which is the opposite of what a driver reads.
                glass.uvRect = new Rect(1f, 0f, -1f, 1f);
            }
        }

        private void Show(bool visible)
        {
            if (frame != null && frame.activeSelf != visible)
            {
                frame.SetActive(visible);
            }

            if (mirrorCamera != null && mirrorCamera.enabled != visible)
            {
                mirrorCamera.enabled = visible;
            }
        }

        private void Release()
        {
            if (mirrorCamera != null)
            {
                Destroy(mirrorCamera.gameObject);
                mirrorCamera = null;
            }

            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
                texture = null;
            }

            if (glass != null)
            {
                glass.texture = null;
            }

            attachedTo = null;
        }
    }
}
