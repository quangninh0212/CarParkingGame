using System.Collections.Generic;
using CarParkingGame.Missions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Prints the heights that decide whether the car can hit a prop at all.
    //
    // "The car drives through it" and "the car drives over it" look identical from the
    // driving seat and have completely different causes, so this measures both sides: how
    // low the car's own collision boxes reach, and how tall each kind of prop is. A prop
    // whose top is below the car's underside can never be touched however solid it is.
    //
    // Read-only.
    public static class CollisionProbeTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[CollisionProbeTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            ProbeCars();
            ProbeProps();

            int failures = VerifyContact() + VerifyMissionFloors();

            Debug.Log(failures == 0
                ? "[CollisionProbeTool] Every car makes contact with every kind of prop it should."
                : $"[CollisionProbeTool] {failures} car/prop pair(s) cannot touch.");

            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void ProbeCars()
        {
            foreach (CarController car in Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool wasActive = car.gameObject.activeSelf;
                car.gameObject.SetActive(true);

                float lowestWheel = float.MaxValue;
                float wheelRadius = 0f;

                foreach (CarController.Wheel wheel in car.wheels)
                {
                    if (wheel.wheelCollider == null)
                    {
                        continue;
                    }

                    lowestWheel = Mathf.Min(lowestWheel, wheel.wheelCollider.transform.position.y - wheel.wheelCollider.radius);
                    wheelRadius = Mathf.Max(wheelRadius, wheel.wheelCollider.radius);
                }

                foreach (CarController.Wheel wheel in car.wheels)
                {
                    if (wheel.wheelCollider == null)
                    {
                        continue;
                    }

                    Debug.Log(
                        $"[CollisionProbeTool] WHEEL '{car.name}'/'{wheel.wheelCollider.name}' radius={wheel.wheelCollider.radius:0.000} " +
                        $"suspensionDistance={wheel.wheelCollider.suspensionDistance:0.000} " +
                        $"targetPosition={wheel.wheelCollider.suspensionSpring.targetPosition:0.00} " +
                        $"centreY(local)={wheel.wheelCollider.center.y:0.000}");

                    break;
                }

                var body = car.GetComponent<Rigidbody>();

                if (body != null)
                {
                    Debug.Log($"[CollisionProbeTool] BODY '{car.name}' mass={body.mass:0} centreOfMass={car.centerOfMass:0.00} rootY={car.transform.position.y:0.000}");
                }

                if (CarParkingGame.Vehicle.VehicleViewPoints.TryMeasureBodyBounds(car.transform, out Bounds bodyBounds))
                {
                    Debug.Log($"[CollisionProbeTool] BODYMESH '{car.name}' local min={bodyBounds.min:0.000} max={bodyBounds.max:0.000}");
                }

                foreach (Collider collider in car.GetComponentsInChildren<Collider>(true))
                {
                    if (collider is WheelCollider)
                    {
                        continue;
                    }

                    Bounds bounds = collider.bounds;

                    Debug.Log(
                        $"[CollisionProbeTool] CAR '{car.name}' collider '{collider.name}' ({collider.GetType().Name}) " +
                        $"bottom={bounds.min.y:0.000} top={bounds.max.y:0.000} size={bounds.size:0.00}  " +
                        $"ground(lowest wheel)={lowestWheel:0.000} -> clearance under the collider = {bounds.min.y - lowestWheel:0.000} m");
                }

                car.gameObject.SetActive(wasActive);
            }
        }

        private static void ProbeProps()
        {
            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager != null)
            {
                manager.RebuildRegistry();

                foreach (MissionAuthoring mission in manager.RegisteredMissions)
                {
                    mission.SetEnvironmentActive(true);
                }
            }

            var seen = new Dictionary<string, int>();

            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string kind = Classify(filter.name);

                if (kind == null)
                {
                    continue;
                }

                seen.TryGetValue(kind, out int count);
                seen[kind] = count + 1;

                // One example of each kind is enough to see the shape of the problem.
                if (count > 0)
                {
                    continue;
                }

                Collider collider = filter.GetComponent<Collider>();
                Renderer renderer = filter.GetComponent<Renderer>();

                string colliderText = collider == null
                    ? "NO COLLIDER"
                    : $"{collider.GetType().Name}{(collider is MeshCollider mesh ? (mesh.convex ? " convex" : " concave") : string.Empty)} " +
                      $"bottom={collider.bounds.min.y:0.000} top={collider.bounds.max.y:0.000} height={collider.bounds.size.y:0.000}";

                Debug.Log(
                    $"[CollisionProbeTool] PROP '{kind}' example '{filter.name}' scale={filter.transform.lossyScale:0.00}  " +
                    $"renderer size={(renderer != null ? renderer.bounds.size : Vector3.zero):0.000}  {colliderText}");
            }

            foreach (KeyValuePair<string, int> entry in seen)
            {
                Debug.Log($"[CollisionProbeTool] COUNT '{entry.Key}' = {entry.Value}");
            }
        }

        // Arithmetic on bounding boxes says a car and a cone overlap; this asks the physics
        // engine the same question with the real collider shapes, by standing each car on
        // top of each kind of prop and testing for penetration. Nothing is moved: the
        // positions are passed in.
        private static int VerifyContact()
        {
            var props = new Dictionary<string, Collider>();

            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string kind = Classify(filter.name);
                var collider = filter.GetComponent<Collider>();

                if (kind == null || collider == null || props.ContainsKey(kind))
                {
                    continue;
                }

                props.Add(kind, collider);
            }

            int failures = 0;

            foreach (CarController car in Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool wasActive = car.gameObject.activeSelf;
                car.gameObject.SetActive(true);

                Transform body = car.transform.Find("BodyCollider");
                var carCollider = body != null ? body.GetComponent<Collider>() : null;

                if (carCollider == null)
                {
                    Debug.LogError($"[CollisionProbeTool] '{car.name}' has no BodyCollider; run Dress Cars With Lights And Horn.");
                    failures++;
                    car.gameObject.SetActive(wasActive);
                    continue;
                }

                float groundLocalY = GroundLine(car);

                foreach (KeyValuePair<string, Collider> prop in props)
                {
                    Bounds propBounds = prop.Value.bounds;

                    // The car standing on the same ground as the prop, straddling it.
                    var carPosition = new Vector3(
                        propBounds.center.x,
                        propBounds.min.y - groundLocalY,
                        propBounds.center.z);

                    bool touches = Physics.ComputePenetration(
                        carCollider, carPosition + carCollider.transform.localPosition, Quaternion.identity,
                        prop.Value, prop.Value.transform.position, prop.Value.transform.rotation,
                        out Vector3 _, out float depth);

                    // The painted bay plates are floor markings 9cm thick; the car is meant
                    // to drive over them, so no contact is the right answer there.
                    bool shouldTouch = prop.Key != "ParkingBlock";

                    if (touches == shouldTouch)
                    {
                        Debug.Log($"[CollisionProbeTool] OK '{car.name}' vs '{prop.Key}': {(touches ? $"contact, {depth:0.000}m deep" : "drives over it, as intended")}.");
                        continue;
                    }

                    Debug.LogError($"[CollisionProbeTool] '{car.name}' vs '{prop.Key}': expected {(shouldTouch ? "contact" : "no contact")} and got the opposite.");
                    failures++;
                }

                car.gameObject.SetActive(wasActive);
            }

            return failures;
        }

        // Every mission's start point and bay must have solid ground under them.
        //
        // Mission 9 dropped the car through the world the moment it loaded, because the
        // course pads were spawned from the kit's flat-marking part and the kit strips
        // colliders from markings. Nothing in the build caught it: the course was built
        // correctly, looked correct from above, and had no floor.
        private static int VerifyMissionFloors()
        {
            var manager = Object.FindFirstObjectByType<MissionManager>(FindObjectsInactive.Include);

            if (manager == null)
            {
                return 0;
            }

            manager.RebuildRegistry();

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                mission.SetEnvironmentActive(true);
            }

            Physics.SyncTransforms();

            int failures = 0;

            foreach (MissionAuthoring mission in manager.RegisteredMissions)
            {
                if (mission.StartPoint != null && !HasFloorUnder(mission.StartPoint.position))
                {
                    Debug.LogError($"[CollisionProbeTool] Mission {mission.MissionId}: nothing solid under the start point - the car will fall through.");
                    failures++;
                }

                if (mission.ParkingZone != null && !HasFloorUnder(mission.ParkingZone.WorldCenter + Vector3.up))
                {
                    Debug.LogError($"[CollisionProbeTool] Mission {mission.MissionId}: nothing solid under the parking bay.");
                    failures++;
                }
            }

            if (failures == 0)
            {
                Debug.Log($"[CollisionProbeTool] All {manager.RegisteredMissions.Count} missions have solid ground under their start point and bay.");
            }

            return failures;
        }

        private static bool HasFloorUnder(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 2f, Vector3.down, 12f);
        }

        private static float GroundLine(CarController car)
        {
            float lowest = 0f;

            foreach (CarController.Wheel wheel in car.wheels)
            {
                if (wheel.wheelCollider == null)
                {
                    continue;
                }

                Vector3 contact = wheel.wheelCollider.transform.position + Vector3.down * wheel.wheelCollider.radius;
                lowest = Mathf.Min(lowest, car.transform.InverseTransformPoint(contact).y);
            }

            return lowest;
        }

        private static string Classify(string name)
        {
            if (name.StartsWith("Cone_Clean", System.StringComparison.Ordinal) || name.StartsWith("Cone", System.StringComparison.Ordinal))
            {
                return "Cone";
            }

            if (name.StartsWith("Block_Barrier_1_YellowStripes", System.StringComparison.Ordinal) || name.StartsWith("Side", System.StringComparison.Ordinal) || name == "BackStop" || name == "CarAhead" || name == "CarBehind")
            {
                return "BarrierBlock";
            }

            if (name.StartsWith("Block_Parking_1_RedStripes", System.StringComparison.Ordinal))
            {
                return "ParkingBlock";
            }

            if (name.StartsWith("Block_Barrier_1_RedStripes", System.StringComparison.Ordinal))
            {
                return "RedBarrier";
            }

            return null;
        }
    }
}
