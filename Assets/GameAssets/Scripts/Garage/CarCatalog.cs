using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Garage
{
    [CreateAssetMenu(fileName = "CarCatalog", menuName = "Car Parking/Car Catalog")]
    public class CarCatalog : ScriptableObject
    {
        [SerializeField] private List<CarDefinition> cars = new List<CarDefinition>();

        public IReadOnlyList<CarDefinition> Cars => cars;
        public int Count => cars.Count;

        public CarDefinition At(int position)
        {
            return position >= 0 && position < cars.Count ? cars[position] : null;
        }

        public CarDefinition FindByCarIndex(int carIndex)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] != null && cars[i].CarIndex == carIndex)
                {
                    return cars[i];
                }
            }

            return null;
        }

        public CarDefinition FindById(string carId)
        {
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] != null && cars[i].CarId == carId)
                {
                    return cars[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        public void EditorSetCars(List<CarDefinition> orderedCars)
        {
            cars = orderedCars;
        }
#endif
    }
}
