using UnityEngine;

namespace CarParkingGame.Garage
{
    // Marks which renderers on a car are bodywork, so painting it does not also tint the
    // windows, lights and tyres. Colour is pushed through a MaterialPropertyBlock: writing
    // to the shared material would permanently modify the project's material assets.
    public class CarPaintTarget : MonoBehaviour
    {
        [SerializeField] private Renderer[] bodyRenderers;

        [Tooltip("Shader colour property. URP Lit uses _BaseColor; older built-in shaders use _Color.")]
        [SerializeField] private string colorProperty = "_BaseColor";

        private MaterialPropertyBlock propertyBlock;
        private bool warnedAboutMissingRenderers;

        public bool HasTargets => bodyRenderers != null && bodyRenderers.Length > 0;

        public void Apply(Color color)
        {
            if (!HasTargets)
            {
                if (!warnedAboutMissingRenderers)
                {
                    warnedAboutMissingRenderers = true;
                    Debug.LogWarning($"[CarPaintTarget] '{name}' has no body renderers assigned, so it cannot be painted.", this);
                }

                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            int propertyId = Shader.PropertyToID(colorProperty);

            foreach (Renderer renderer in bodyRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(propertyId, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
