using UnityEngine;

namespace CarParkingGame.Garage
{
    [CreateAssetMenu(fileName = "Car", menuName = "Car Parking/Car Definition")]
    public class CarDefinition : ScriptableObject
    {
        [SerializeField] private string carId = "car";
        [SerializeField] private string displayName = "Car";
        [SerializeField] private int purchasePrice;

        [Tooltip("Child index inside the player-car container. This is how the existing " +
                 "CarSelection identifies cars, so ownership in the save file is keyed by it. " +
                 "Re-run the garage validation tool if the container's children are ever reordered.")]
        [SerializeField] private int carIndex;

        [SerializeField] private Sprite thumbnail;
        [SerializeField, Range(1, 5)] private int topSpeedRating = 3;
        [SerializeField, Range(1, 5)] private int accelerationRating = 3;
        [SerializeField, Range(1, 5)] private int brakingRating = 3;
        [SerializeField, Range(1, 5)] private int handlingRating = 3;

        public string CarId => carId;
        public string DisplayName => displayName;
        public int PurchasePrice => purchasePrice;
        public int CarIndex => carIndex;
        public Sprite Thumbnail => thumbnail;
        public int TopSpeedRating => topSpeedRating;
        public int AccelerationRating => accelerationRating;
        public int BrakingRating => brakingRating;
        public int HandlingRating => handlingRating;

        public bool OwnedByDefault => purchasePrice <= 0;

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string name,
            int price,
            int index,
            int topSpeed,
            int acceleration,
            int braking,
            int handling)
        {
            carId = id;
            displayName = name;
            purchasePrice = price;
            carIndex = index;
            topSpeedRating = topSpeed;
            accelerationRating = acceleration;
            brakingRating = braking;
            handlingRating = handling;
        }
#endif
    }
}
