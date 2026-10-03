using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Finds the car the player is currently driving. The scene keeps all cars as siblings
    // and activates one, so "active in hierarchy" is the selector - the same rule the
    // existing camera already uses.
    //
    // Searching is throttled rather than done per frame, because FindObjectsByType in an
    // Update loop is exactly the kind of cost this project cannot afford on Android.
    public static class ActiveVehicleLocator
    {
        private const float SearchInterval = 0.5f;

        private static CarController cached;
        private static float nextSearchTime;

        public static CarController Current
        {
            get
            {
                if (cached != null && cached.gameObject.activeInHierarchy)
                {
                    return cached;
                }

                if (Time.unscaledTime < nextSearchTime)
                {
                    return null;
                }

                nextSearchTime = Time.unscaledTime + SearchInterval;
                cached = Search();
                return cached;
            }
        }

        public static void Invalidate()
        {
            cached = null;
            nextSearchTime = 0f;
        }

        private static CarController Search()
        {
            CarController[] controllers = Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (CarController controller in controllers)
            {
                if (controller.gameObject.activeInHierarchy)
                {
                    return controller;
                }
            }

            return null;
        }
    }
}
