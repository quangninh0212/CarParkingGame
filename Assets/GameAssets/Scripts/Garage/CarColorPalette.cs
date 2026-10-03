using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Garage
{
    [Serializable]
    public class CarColorOption
    {
        public string displayName = "Colour";
        public Color color = Color.white;
    }

    [CreateAssetMenu(fileName = "CarColorPalette", menuName = "Car Parking/Car Colour Palette")]
    public class CarColorPalette : ScriptableObject
    {
        [SerializeField]
        private List<CarColorOption> colors = new List<CarColorOption>();

        public IReadOnlyList<CarColorOption> Colors => colors;
        public int Count => colors.Count;

        public bool TryGetColor(int index, out Color color)
        {
            if (index >= 0 && index < colors.Count && colors[index] != null)
            {
                color = colors[index].color;
                return true;
            }

            color = Color.white;
            return false;
        }

#if UNITY_EDITOR
        public void EditorSetColors(List<CarColorOption> options)
        {
            colors = options;
        }
#endif
    }
}
