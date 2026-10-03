using System;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    public struct VehicleCollisionInfo
    {
        public string tag;
        public float impulse;
        public float relativeSpeed;
        public int colliderId;
        public Vector3 point;
    }

    // Added to the active vehicle at runtime by the scoring system, so no prefab or
    // scene change is needed to start collecting collision data.
    public class VehicleCollisionReporter : MonoBehaviour
    {
        public event Action<VehicleCollisionInfo> Collided;

        private void OnCollisionEnter(Collision collision)
        {
            Report(collision);
        }

        private void Report(Collision collision)
        {
            if (Collided == null || collision.collider == null)
            {
                return;
            }

            Collided.Invoke(new VehicleCollisionInfo
            {
                tag = collision.collider.tag,
                impulse = collision.impulse.magnitude,
                relativeSpeed = collision.relativeVelocity.magnitude,
                colliderId = collision.collider.GetInstanceID(),
                point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position
            });
        }
    }
}
