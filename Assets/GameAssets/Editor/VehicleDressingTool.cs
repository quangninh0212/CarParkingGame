using System;
using System.Collections.Generic;
using System.IO;
using CarParkingGame.Garage;
using CarParkingGame.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Gives each car the parts the light and horn systems need, which the art does not
    // provide: the car models contain no separate lamp objects at all (only wheels are
    // exposed), and the project has no horn sound.
    //
    // Nothing is downloaded. The horn is synthesised here as a plain two-tone WAV, and the
    // lamps are placeholder emissive spheres. Positions are not guessed: they are derived
    // from the car's own wheel positions, so each car gets lamps at its own track width,
    // wheelbase and ride height.
    //
    // Replace the spheres with modelled lamps when art exists; the wiring stays valid.
    public static class VehicleDressingTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";
        private const string AudioFolder = "Assets/GameAssets/Audio";
        private const string HornClipPath = AudioFolder + "/HornPlaceholder.wav";
        private const string MaterialFolder = "Assets/GameAssets/Materials";
        private const string LampParentName = "PlaceholderLights";

        private static readonly Color HeadlightColor = new Color(1f, 0.96f, 0.85f);
        private static readonly Color BrakeColor = new Color(1f, 0.15f, 0.12f);
        private static readonly Color IndicatorColor = new Color(1f, 0.6f, 0.05f);

        [MenuItem("Tools/Car Parking/Dress Cars With Lights And Horn")]
        public static void DressInOpenScene()
        {
            Dress();
            Debug.Log("[VehicleDressingTool] Done. Save the scene to keep it.");
        }

        public static void DressFromCommandLine()
        {
            GenerateHornClip();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[VehicleDressingTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            Dress();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[VehicleDressingTool] Saved '{ScenePath}'.");
            EditorApplication.Exit(0);
        }

        private static void Dress()
        {
            AudioClip horn = AssetDatabase.LoadAssetAtPath<AudioClip>(HornClipPath);

            if (horn == null)
            {
                GenerateHornClip();
                horn = AssetDatabase.LoadAssetAtPath<AudioClip>(HornClipPath);
            }

            Material headlightMaterial = LoadOrCreateEmissiveMaterial("LampHeadlight", HeadlightColor);
            Material brakeMaterial = LoadOrCreateEmissiveMaterial("LampBrake", BrakeColor);
            Material indicatorMaterial = LoadOrCreateEmissiveMaterial("LampIndicator", IndicatorColor);

            CarController[] cars = UnityEngine.Object.FindObjectsByType<CarController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (cars.Length == 0)
            {
                Debug.LogError("[VehicleDressingTool] No CarController found in the scene.");
                return;
            }

            foreach (CarController car in cars)
            {
                DressCar(car, horn, headlightMaterial, brakeMaterial, indicatorMaterial);
            }

            WireCameraDirector();
        }

        private static void DressCar(
            CarController car,
            AudioClip horn,
            Material headlightMaterial,
            Material brakeMaterial,
            Material indicatorMaterial)
        {
            if (!TryMeasureCar(car, out CarMeasurements measurements))
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no usable WheelColliders, so lamps could not be placed.", car);
                return;
            }

            Transform existing = car.transform.Find(LampParentName);

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var lampRoot = new GameObject(LampParentName);
            lampRoot.transform.SetParent(car.transform, false);
            lampRoot.transform.localPosition = Vector3.zero;
            lampRoot.transform.localRotation = Quaternion.identity;

            // Lamps are placed on the bodywork, not on the wheel track.
            //
            // Deriving them from the wheels put both lamps symmetrically about the car's
            // pivot, which is not the middle of the body on these models, and guessed the
            // nose as "front axle plus a quarter of the wheelbase" - so the pair sat off
            // centre and floated clear of the bonnet. The body's own bounds give the real
            // nose, tail, waistline and centre line.
            bool haveBody = CarParkingGame.Vehicle.VehicleViewPoints.TryMeasureBodyBounds(car.transform, out Bounds body);

            if (!haveBody)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no measurable body mesh; lamps fall back to the wheel geometry.", car);
            }

            float centreX = haveBody ? body.center.x : 0f;
            float halfBodyWidth = haveBody ? body.extents.x : measurements.halfWidth;
            float noseZ = haveBody ? body.max.z : measurements.frontZ + measurements.overhang;
            float tailZ = haveBody ? body.min.z : measurements.rearZ - measurements.overhang;

            // Lamps sit a little proud of the bodywork so they are not buried in the mesh.
            const float Proud = 0.04f;

            float lampSize = halfBodyWidth * 0.2f;
            float sideX = halfBodyWidth * 0.66f;
            float cornerX = halfBodyWidth * 0.92f;
            float frontZ = noseZ + Proud;
            float rearZ = tailZ - Proud;

            // Headlamp height: a third of the way up the body, which is where a bumper
            // lamp sits on all three of these cars.
            float lampY = haveBody
                ? body.min.y + body.size.y * 0.34f
                : measurements.wheelY + measurements.halfWidth * 0.55f;

            GameObject headlightLeft = CreateLamp(lampRoot.transform, "HeadlightLeft", new Vector3(centreX - sideX, lampY, frontZ), lampSize, headlightMaterial);
            GameObject headlightRight = CreateLamp(lampRoot.transform, "HeadlightRight", new Vector3(centreX + sideX, lampY, frontZ), lampSize, headlightMaterial);

            GameObject brakeLeft = CreateLamp(lampRoot.transform, "BrakeLeft", new Vector3(centreX - sideX, lampY, rearZ), lampSize, brakeMaterial);
            GameObject brakeRight = CreateLamp(lampRoot.transform, "BrakeRight", new Vector3(centreX + sideX, lampY, rearZ), lampSize, brakeMaterial);

            GameObject indicatorFrontLeft = CreateLamp(lampRoot.transform, "IndicatorFrontLeft", new Vector3(centreX - cornerX, lampY, frontZ - lampSize), lampSize * 0.8f, indicatorMaterial);
            GameObject indicatorRearLeft = CreateLamp(lampRoot.transform, "IndicatorRearLeft", new Vector3(centreX - cornerX, lampY, rearZ + lampSize), lampSize * 0.8f, indicatorMaterial);
            GameObject indicatorFrontRight = CreateLamp(lampRoot.transform, "IndicatorFrontRight", new Vector3(centreX + cornerX, lampY, frontZ - lampSize), lampSize * 0.8f, indicatorMaterial);
            GameObject indicatorRearRight = CreateLamp(lampRoot.transform, "IndicatorRearRight", new Vector3(centreX + cornerX, lampY, rearZ + lampSize), lampSize * 0.8f, indicatorMaterial);

            // Two real spot lights only for the headlights. They are switched off unless the
            // player turns the headlights on, and they are the one genuinely expensive part
            // of this - drop them first if a low-end device struggles.
            Light spotLeft = CreateHeadlightSpot(headlightLeft.transform, measurements);
            Light spotRight = CreateHeadlightSpot(headlightRight.transform, measurements);

            VehicleLights lights = car.GetComponent<VehicleLights>();

            if (lights == null)
            {
                lights = car.gameObject.AddComponent<VehicleLights>();
            }

            var serialized = new SerializedObject(lights);
            SetObject(serialized, "car", car);

            SetLightGroup(serialized, "headlights", new[] { headlightLeft, headlightRight }, new[] { spotLeft, spotRight });
            SetLightGroup(serialized, "brakeLights", new[] { brakeLeft, brakeRight }, Array.Empty<Light>());
            SetLightGroup(serialized, "leftIndicator", new[] { indicatorFrontLeft, indicatorRearLeft }, Array.Empty<Light>());
            SetLightGroup(serialized, "rightIndicator", new[] { indicatorFrontRight, indicatorRearRight }, Array.Empty<Light>());

            serialized.ApplyModifiedProperties();

            VehicleHorn hornComponent = car.GetComponent<VehicleHorn>();

            if (hornComponent == null)
            {
                hornComponent = car.gameObject.AddComponent<VehicleHorn>();
            }

            var hornSerialized = new SerializedObject(hornComponent);
            SetObject(hornSerialized, "hornClip", horn);
            hornSerialized.ApplyModifiedProperties();

            WirePaintTarget(car, lampRoot.transform);
            MeasureViewPoints(car);

            Debug.Log($"[VehicleDressingTool] '{car.name}': 8 lamps placed from its own wheel geometry (track {measurements.halfWidth * 2f:0.00}m, wheelbase {measurements.frontZ - measurements.rearZ:0.00}m), horn wired.", car);
        }

        // Picks the renderers the garage will paint: everything on the car except the
        // wheels and the lamps we just added.
        //
        // The car bodies are single meshes, so if a body mesh also covers the windows, the
        // colour will tint those too. That is a limit of the art, not of the code - narrow
        // this list by hand in the Inspector if it looks wrong.
        private static void WirePaintTarget(CarController car, Transform lampRoot)
        {
            var bodyRenderers = new List<Renderer>();

            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(lampRoot))
                {
                    continue;
                }

                if (renderer.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                bodyRenderers.Add(renderer);
            }

            if (bodyRenderers.Count == 0)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no paintable renderers.", car);
                return;
            }

            CarPaintTarget paint = car.GetComponent<CarPaintTarget>();

            if (paint == null)
            {
                paint = car.gameObject.AddComponent<CarPaintTarget>();
            }

            var serialized = new SerializedObject(paint);
            SerializedProperty array = serialized.FindProperty("bodyRenderers");

            if (array == null)
            {
                Debug.LogWarning("[VehicleDressingTool] CarPaintTarget has no 'bodyRenderers' field.");
                return;
            }

            array.arraySize = bodyRenderers.Count;

            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = bodyRenderers[i];
            }

            serialized.ApplyModifiedProperties();

            Debug.Log($"[VehicleDressingTool] '{car.name}': {bodyRenderers.Count} paintable renderer(s) assigned.", car);
        }

        // Where the driver sits, measured now so the cockpit and look-back cameras do not
        // have to work it out on the first frame of play.
        private static void MeasureViewPoints(CarController car)
        {
            var points = car.GetComponent<CarParkingGame.Vehicle.VehicleViewPoints>();

            if (points == null)
            {
                points = car.gameObject.AddComponent<CarParkingGame.Vehicle.VehicleViewPoints>();
            }

            points.Measure();
            EditorUtility.SetDirty(points);

            Debug.Log($"[VehicleDressingTool] '{car.name}': driver's eye at {points.DriverEyeLocalPosition:0.00} in car space.", car);
        }

        private struct CarMeasurements
        {
            public float halfWidth;
            public float frontZ;
            public float rearZ;
            public float wheelY;
            public float overhang;
            public float wheelRadius;
        }

        // Everything is derived from where the wheels actually are, so each car gets lamps
        // that match its own proportions instead of shared magic numbers.
        private static bool TryMeasureCar(CarController car, out CarMeasurements measurements)
        {
            measurements = default;

            if (car.wheels == null || car.wheels.Count == 0)
            {
                return false;
            }

            float maxX = 0f;
            float frontZ = float.MinValue;
            float rearZ = float.MaxValue;
            float sumY = 0f;
            float radius = 0f;
            int counted = 0;

            foreach (CarController.Wheel wheel in car.wheels)
            {
                if (wheel.wheelCollider == null)
                {
                    continue;
                }

                Vector3 local = car.transform.InverseTransformPoint(wheel.wheelCollider.transform.position);

                maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
                frontZ = Mathf.Max(frontZ, local.z);
                rearZ = Mathf.Min(rearZ, local.z);
                sumY += local.y;
                radius = Mathf.Max(radius, wheel.wheelCollider.radius);
                counted++;
            }

            if (counted == 0 || maxX <= 0.01f || frontZ <= rearZ)
            {
                return false;
            }

            float wheelbase = frontZ - rearZ;

            measurements = new CarMeasurements
            {
                halfWidth = maxX,
                frontZ = frontZ,
                rearZ = rearZ,
                wheelY = sumY / counted,
                wheelRadius = radius,

                // Bodywork typically extends about a quarter of the wheelbase past each axle.
                overhang = wheelbase * 0.25f
            };

            return true;
        }

        private static GameObject CreateLamp(Transform parent, string name, Vector3 localPosition, float size, Material material)
        {
            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = name;
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = localPosition;
            lamp.transform.localScale = Vector3.one * Mathf.Max(0.05f, size);

            // Primitives ship with a collider; leaving it on would give the car phantom
            // bumpers that collide with the world and register scoring penalties.
            Collider collider = lamp.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = lamp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            lamp.SetActive(false);
            return lamp;
        }

        private static Light CreateHeadlightSpot(Transform lamp, CarMeasurements measurements)
        {
            var lightObject = new GameObject("Spot");
            lightObject.transform.SetParent(lamp, false);
            lightObject.transform.localPosition = Vector3.zero;

            // Dipped, like a real low beam. Aimed dead level the cone washed out over the
            // horizon and lit nothing the player could see.
            lightObject.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = Mathf.Max(12f, measurements.halfWidth * 20f);
            light.spotAngle = 55f;
            light.intensity = 2.2f;
            light.color = HeadlightColor;
            light.shadows = LightShadows.None;
            light.enabled = false;

            return light;
        }

        private static void SetLightGroup(SerializedObject serialized, string fieldName, GameObject[] glowObjects, Light[] lights)
        {
            SerializedProperty group = serialized.FindProperty(fieldName);

            if (group == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] No field '{fieldName}' on VehicleLights.");
                return;
            }

            SerializedProperty glowArray = group.FindPropertyRelative("glowObjects");
            SerializedProperty lightArray = group.FindPropertyRelative("lights");

            if (glowArray == null || lightArray == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{fieldName}' does not look like a LightGroup.");
                return;
            }

            glowArray.arraySize = glowObjects.Length;

            for (int i = 0; i < glowObjects.Length; i++)
            {
                glowArray.GetArrayElementAtIndex(i).objectReferenceValue = glowObjects[i];
            }

            lightArray.arraySize = lights.Length;

            for (int i = 0; i < lights.Length; i++)
            {
                lightArray.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            }
        }

        private static void WireCameraDirector()
        {
            var legacyCamera = UnityEngine.Object.FindFirstObjectByType<CarCameraController>();

            if (legacyCamera == null)
            {
                Debug.LogWarning("[VehicleDressingTool] No CarCameraController found; the camera director was not wired.");
                return;
            }

            VehicleCameraDirector director = legacyCamera.GetComponent<VehicleCameraDirector>();

            if (director == null)
            {
                director = legacyCamera.gameObject.AddComponent<VehicleCameraDirector>();
            }

            var serialized = new SerializedObject(director);
            SetObject(serialized, "cameraTransform", legacyCamera.transform);
            SetObject(serialized, "legacyFollowCamera", legacyCamera);
            serialized.ApplyModifiedProperties();

            Debug.Log($"[VehicleDressingTool] Camera director wired to '{legacyCamera.name}'.");
        }

        private static void SetObject(SerializedObject serialized, string fieldName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] No field '{fieldName}' on {serialized.targetObject.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
        }

        private static Material LoadOrCreateEmissiveMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/GameAssets", "Materials");
            }

            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogError("[VehicleDressingTool] Could not find an unlit shader for the lamp material.");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);

            AssetDatabase.CreateAsset(material, path);
            Debug.Log($"[VehicleDressingTool] Created lamp material '{path}'.");
            return material;
        }

        // A car horn is two close notes sounded together. Writing the PCM here keeps the
        // project free of downloaded audio and gives something obviously placeholder.
        [MenuItem("Tools/Car Parking/Generate Placeholder Horn Clip")]
        public static void GenerateHornClip()
        {
            const int sampleRate = 22050;
            const float seconds = 0.5f;
            const float lowHz = 440f;
            const float highHz = 554.37f;

            int sampleCount = (int)(sampleRate * seconds);
            var samples = new short[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;

                // Slight square-wave character is what makes it read as a horn rather than
                // a test tone.
                float wave = Mathf.Sin(2f * Mathf.PI * lowHz * t)
                             + Mathf.Sin(2f * Mathf.PI * highHz * t)
                             + 0.25f * Mathf.Sin(2f * Mathf.PI * lowHz * 2f * t);

                float attack = Mathf.Clamp01(t / 0.02f);
                float release = Mathf.Clamp01((seconds - t) / 0.06f);
                float amplitude = wave / 2.25f * attack * release * 0.75f;

                samples[i] = (short)Mathf.Clamp(amplitude * short.MaxValue, short.MinValue, short.MaxValue);
            }

            Directory.CreateDirectory(AudioFolder);
            File.WriteAllBytes(HornClipPath, BuildWavFile(samples, sampleRate));

            AssetDatabase.ImportAsset(HornClipPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[VehicleDressingTool] Wrote a synthesised placeholder horn to '{HornClipPath}'.");
        }

        private static byte[] BuildWavFile(short[] samples, int sampleRate)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * sizeof(short);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (short sample in samples)
            {
                writer.Write(sample);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
