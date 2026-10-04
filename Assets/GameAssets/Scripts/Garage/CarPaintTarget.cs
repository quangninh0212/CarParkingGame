using System;
using UnityEngine;

namespace CarParkingGame.Garage
{
    // Which material slots on a car are its paintwork, so the garage repaints the panels
    // and leaves the glass, lights, chrome, tyres and interior alone.
    //
    // This used to hold whole renderers. A car body is one mesh with several materials on
    // it - paint, glass, lamps, trim - and a property block set on the renderer applies to
    // every one of them, so choosing a colour turned the windscreen, the headlights and
    // the bumpers that colour too. A slot is a renderer *and* a submesh index, which is
    // the level the paint actually lives at.
    //
    // Colour goes through a MaterialPropertyBlock: writing to the shared material would
    // permanently modify the project's material assets.
    public class CarPaintTarget : MonoBehaviour
    {
        [Serializable]
        public struct PaintSlot
        {
            public Renderer renderer;
            public int materialIndex;
        }

        [SerializeField] private PaintSlot[] paintSlots = Array.Empty<PaintSlot>();

        [Tooltip("Shader colour property. URP Lit uses _BaseColor; older built-in shaders use _Color.")]
        [SerializeField] private string colorProperty = "_BaseColor";

        private MaterialPropertyBlock propertyBlock;
        private bool warnedAboutMissingSlots;

        public bool HasTargets => paintSlots != null && paintSlots.Length > 0;

        public void Apply(Color color)
        {
            if (!HasTargets)
            {
                if (!warnedAboutMissingSlots)
                {
                    warnedAboutMissingSlots = true;
                    Debug.LogWarning($"[CarPaintTarget] '{name}' has no paint slots assigned, so it cannot be painted.", this);
                }

                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            int propertyId = Shader.PropertyToID(colorProperty);

            foreach (PaintSlot slot in paintSlots)
            {
                if (slot.renderer == null)
                {
                    continue;
                }

                // Per submesh. Without the index this writes to every material on the
                // renderer, which is the bug this class exists to avoid.
                slot.renderer.GetPropertyBlock(propertyBlock, slot.materialIndex);
                propertyBlock.SetColor(propertyId, color);
                slot.renderer.SetPropertyBlock(propertyBlock, slot.materialIndex);
            }
        }

#if UNITY_EDITOR
        public void EditorSetSlots(PaintSlot[] slots)
        {
            paintSlots = slots ?? Array.Empty<PaintSlot>();
        }
#endif
    }
}
