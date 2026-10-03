using CarParkingGame.Settings;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    public enum MirrorKind
    {
        Rear,
        Left,
        Right
    }

    // A mirror is a small camera rendering into a low-resolution RenderTexture shown on the
    // mirror's surface. This is not a true planar reflection: the camera is simply aimed
    // backwards by hand, which costs a fraction of the maths and reads the same on a
    // 256x128 mirror.
    //
    // Three things keep it affordable on Android:
    //  - the player's graphics tier decides whether this mirror is allowed at all
    //    (none on Low, rear only on Medium, sides too on High),
    //  - the camera is driven manually every few frames instead of rendering every frame,
    //  - nothing renders while the mirror is off-screen.
    //
    // Put this component on the mirror's own surface renderer, so Unity's visibility
    // callbacks apply to it.
    [RequireComponent(typeof(Renderer))]
    public class MirrorSurface : MonoBehaviour
    {
        [SerializeField] private MirrorKind kind = MirrorKind.Rear;
        [SerializeField] private Camera mirrorCamera;
        [SerializeField] private string textureProperty = "_BaseMap";
        [SerializeField] private Vector2Int mediumResolution = new Vector2Int(256, 128);
        [SerializeField] private Vector2Int highResolution = new Vector2Int(512, 256);

        [Tooltip("1 renders every frame. 2 or 3 is usually indistinguishable on a small mirror and much cheaper.")]
        [SerializeField, Range(1, 4)] private int renderEveryNthFrame = 2;

        private Renderer surfaceRenderer;
        private MaterialPropertyBlock propertyBlock;
        private RenderTexture texture;
        private bool allowedByQuality;
        private bool onScreen = true;
        private int frameCounter;

        private void Awake()
        {
            surfaceRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();

            if (mirrorCamera != null)
            {
                // Driven by hand from Update; never let Unity render it automatically.
                mirrorCamera.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied += ApplyQualitySetting;
            }

            ApplyQualitySetting();
        }

        private void OnDisable()
        {
            if (SettingsManager.Instance != null)
            {
                SettingsManager.Instance.SettingsApplied -= ApplyQualitySetting;
            }

            ReleaseTexture();
        }

        private void OnBecameVisible()
        {
            onScreen = true;
        }

        private void OnBecameInvisible()
        {
            onScreen = false;
        }

        private void Update()
        {
            if (!allowedByQuality || !onScreen || mirrorCamera == null || texture == null)
            {
                return;
            }

            frameCounter++;

            if (frameCounter < renderEveryNthFrame)
            {
                return;
            }

            frameCounter = 0;
            mirrorCamera.Render();
        }

        public void ApplyQualitySetting()
        {
            GraphicsTierSettings tier = SettingsManager.Instance != null
                ? SettingsManager.Instance.CurrentTierSettings
                : null;

            bool wasAllowed = allowedByQuality;

            allowedByQuality = tier != null && (kind == MirrorKind.Rear
                ? tier.realtimeRearMirror
                : tier.realtimeSideMirrors);

            if (!allowedByQuality)
            {
                ReleaseTexture();
                SetSurfaceVisible(false);
                return;
            }

            Vector2Int resolution = SettingsManager.Instance != null
                && SettingsManager.Instance.Tier == GraphicsTier.High
                ? highResolution
                : mediumResolution;

            if (texture == null || texture.width != resolution.x || texture.height != resolution.y || !wasAllowed)
            {
                CreateTexture(resolution);
            }

            SetSurfaceVisible(true);
        }

        private void CreateTexture(Vector2Int resolution)
        {
            ReleaseTexture();

            texture = new RenderTexture(resolution.x, resolution.y, 16, RenderTextureFormat.Default)
            {
                name = $"Mirror_{kind}_{resolution.x}x{resolution.y}",
                antiAliasing = 1,
                useMipMap = false,
                filterMode = FilterMode.Bilinear
            };

            texture.Create();

            if (mirrorCamera != null)
            {
                mirrorCamera.targetTexture = texture;
            }

            propertyBlock.SetTexture(textureProperty, texture);
            surfaceRenderer.SetPropertyBlock(propertyBlock);
        }

        // RenderTextures are native allocations: dropping the reference without releasing
        // it leaks GPU memory for the rest of the session.
        private void ReleaseTexture()
        {
            if (texture == null)
            {
                return;
            }

            if (mirrorCamera != null)
            {
                mirrorCamera.targetTexture = null;
            }

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void SetSurfaceVisible(bool visible)
        {
            if (surfaceRenderer != null)
            {
                surfaceRenderer.enabled = visible;
            }
        }
    }
}
