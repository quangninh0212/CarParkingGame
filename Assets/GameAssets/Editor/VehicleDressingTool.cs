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
        private const string ImpactSourceName = "ImpactAudio";
        private const string BrakeSourceName = "BrakeAudio";

        private const string IndicatorSourceName = "IndicatorAudio";

        private const string CrashClipPath = "Assets/GameAssets/Audio/car_crash.mp3";
        private const string IndicatorClipPath = "Assets/GameAssets/Audio/tieng-xi-nhan.mp3";
        private const string BrakeClipPath = "Assets/GameAssets/Audio/car-braking.mp3";
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
                DressCar(car, horn);
            }

            DressShowroomCars();
            WireCameraDirector();
        }

        private static void DressCar(
            CarController car,
            AudioClip horn)
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

            // No lamp geometry at all any more.
            //
            // It used to add eight boxes to the car - headlights, tail lights, four
            // indicators. However they were sized or shaped, they sat outside the bodywork
            // and read as blocks bolted to the car, which is what a player called them, and
            // the tail pair glowed behind the car the entire time it stood still.
            //
            // The cars already have their lamps. Every one of these models names the
            // materials: Mc_FrontLights, K_RearLights, B_FlickerLights. Lighting the lens
            // the model already has cannot look stuck on, because it is not stuck on.
            float sideX = halfBodyWidth * 0.56f;

            float lampY = haveBody
                ? body.min.y + body.size.y * 0.34f
                : measurements.wheelY + measurements.halfWidth * 0.55f;

            float frontZ = noseZ + 0.01f;

            // Two real spot lights, so switching the headlights on actually lights the road
            // ahead. They are the one genuinely expensive part of this - drop them first if
            // a low-end device struggles.
            Light spotLeft = CreateHeadlightSpot(lampRoot.transform, new Vector3(centreX - sideX, lampY, frontZ), measurements);
            Light spotRight = CreateHeadlightSpot(lampRoot.transform, new Vector3(centreX + sideX, lampY, frontZ), measurements);

            LampSurface[] headlightSurfaces = FindLampSurfaces(car, HeadlightWords);
            LampSurface[] brakeSurfaces = FindLampSurfaces(car, BrakeLightWords);
            // Indicators have to be one side at a time, and a material cannot do that: the
            // left and right lamps of a car share one, so tinting it flashes both sides at
            // once. A lamp's two halves are separable in the geometry though, so each side
            // gets a marker cut to the shape of the lens it covers.
            LampSurface[] frontIndicatorLens = FindLampSurfaces(car, IndicatorWords);

            if (frontIndicatorLens.Length == 0)
            {
                frontIndicatorLens = headlightSurfaces;
            }

            Material amber = LoadOrCreateLampMaterial("LampIndicator", IndicatorColor);

            var leftMarkers = new List<GameObject>();
            var rightMarkers = new List<GameObject>();

            AddSideMarkers(car, lampRoot.transform, frontIndicatorLens, "IndicatorFront", amber, leftMarkers, rightMarkers);
            AddSideMarkers(car, lampRoot.transform, brakeSurfaces, "IndicatorRear", amber, leftMarkers, rightMarkers);

            VehicleLights lights = car.GetComponent<VehicleLights>();

            if (lights == null)
            {
                lights = car.gameObject.AddComponent<VehicleLights>();
            }

            var serialized = new SerializedObject(lights);
            SetObject(serialized, "car", car);

            SetLightGroup(serialized, "headlights", new[] { spotLeft, spotRight }, headlightSurfaces, HeadlightColor, Array.Empty<GameObject>());
            SetLightGroup(serialized, "brakeLights", Array.Empty<Light>(), brakeSurfaces, BrakeColor, Array.Empty<GameObject>());

            // Markers rather than a tinted material, so a left signal is the left pair and
            // nothing else. They also keep clear of the brake, which owns the tail lens.
            SetLightGroup(serialized, "leftIndicator", Array.Empty<Light>(), Array.Empty<LampSurface>(), IndicatorColor, leftMarkers.ToArray());
            SetLightGroup(serialized, "rightIndicator", Array.Empty<Light>(), Array.Empty<LampSurface>(), IndicatorColor, rightMarkers.ToArray());

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
            FitBodyCollider(car);

            Debug.Log($"[VehicleDressingTool] '{car.name}': {headlightSurfaces.Length} headlight and {brakeSurfaces.Length} tail lens(es) on the model, {leftMarkers.Count} left and {rightMarkers.Count} right indicator marker(s); horn wired.", car);
        }

        // The material slots the garage will paint.
        //
        // This used to collect whole renderers, and the garage then set a colour on each
        // one. A car body is a single mesh with several materials on it, and a property
        // block set on the renderer reaches all of them, so choosing red turned the
        // windscreen, the headlights and the bumpers red as well. A player reported it as
        // "it paints the whole car and looks terrible", which is exactly what it did.
        //
        // The art names its materials honestly - SEDAN_PAINT, K_Body, B_Body against
        // SEDAN_GLASS, B_Tyre, K_Chrome - so the slot is picked by the material's own
        // name, and everything the pack calls something else is left alone.
        private static readonly string[] PaintMaterialWords = { "paint", "body" };

        private static readonly string[] NotPaintMaterialWords =
        {
            "glass", "window", "light", "lamp", "bulb", "chrome", "tyre", "tire", "wheel",
            "rim", "mirror", "plastic", "interior", "grill", "plate", "under", "engine",
            "trim", "vinyl", "hole", "collider", "exhaust", "lod"
        };

        private static void WirePaintTarget(CarController car, Transform lampRoot)
        {
            WirePaintTargetOn(car.transform, lampRoot);
            WireCollisionAudio(car);
        }

        // The noises the car makes when it hits something and when it is stood on the
        // brake.
        //
        // Both listen for themselves rather than going through the mission system, because
        // the reporter the missions attach only exists while a mission is running - and a
        // car that is silent in free roam sounds broken.
        private static void WireCollisionAudio(CarController car)
        {
            AudioClip crash = PrepareClip(CrashClipPath);
            AudioClip braking = PrepareClip(BrakeClipPath);
            AudioClip ticking = PrepareClip(IndicatorClipPath);

            if (crash == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] No crash clip at '{CrashClipPath}'; the car will hit things in silence.");
            }

            if (braking == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] No brake clip at '{BrakeClipPath}'; braking will be silent.");
            }

            var impact = car.GetComponent<VehicleCollisionAudio>();

            if (impact == null)
            {
                impact = car.gameObject.AddComponent<VehicleCollisionAudio>();
            }

            var brake = car.GetComponent<VehicleBrakeAudio>();

            if (brake == null)
            {
                brake = car.gameObject.AddComponent<VehicleBrakeAudio>();
            }

            // A source each. One shared source would mean a crash cutting off the brake,
            // and both of them cutting off the engine loop.
            AudioSource impactSource = MakeAudioSource(car, ImpactSourceName, 0.9f);
            AudioSource brakeSource = MakeAudioSource(car, BrakeSourceName, 0.8f);

            var impactSerialized = new SerializedObject(impact);
            SetObject(impactSerialized, "impact", crash);
            SetObject(impactSerialized, "source", impactSource);
            impactSerialized.ApplyModifiedProperties();

            var brakeSerialized = new SerializedObject(brake);
            SetObject(brakeSerialized, "brakeClip", braking);
            SetObject(brakeSerialized, "source", brakeSource);
            brakeSerialized.ApplyModifiedProperties();

            WireIndicatorAudio(car, ticking);

            EditorUtility.SetDirty(impact);
            EditorUtility.SetDirty(brake);

            Debug.Log($"[VehicleDressingTool] '{car.name}': crash '{(crash != null ? crash.name : "none")}', brake '{(braking != null ? braking.name : "none")}'.", car);
        }

        // The indicator tick lives with VehicleLights, because it has to keep time with
        // the lamps and stop when they do.
        private static void WireIndicatorAudio(CarController car, AudioClip ticking)
        {
            var lights = car.GetComponent<VehicleLights>();

            if (lights == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no VehicleLights, so the indicator cannot tick.", car);
                return;
            }

            var audio = lights.GetComponent<VehicleIndicatorAudio>();

            if (audio == null)
            {
                audio = lights.gameObject.AddComponent<VehicleIndicatorAudio>();
            }

            AudioSource source = MakeAudioSource(car, IndicatorSourceName, 0.55f);

            var serialized = new SerializedObject(audio);
            SetObject(serialized, "tick", ticking);
            SetObject(serialized, "source", source);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(audio);
        }

        // Short effects played often: decoded once at load rather than streamed or
        // decoded on every play, and forced to mono because they are played flat anyway.
        private static AudioClip PrepareClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                return null;
            }

            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;

                // A bang is decoded up front because it has to land the frame it is asked
                // for. A long loop is left compressed: decoding nineteen seconds of
                // ticking into memory costs about two megabytes to save a start-up that
                // nobody is waiting on.
                settings.loadType = clip.length > 3f
                    ? AudioClipLoadType.CompressedInMemory
                    : AudioClipLoadType.DecompressOnLoad;

                settings.preloadAudioData = true;

                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"[VehicleDressingTool] Clip '{clip.name}' is {clip.length:0.00}s, {clip.channels} channel(s).");
            return clip;
        }

        private static AudioSource MakeAudioSource(CarController car, string name, float volume)
        {
            Transform existing = car.transform.Find(name);

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var host = new GameObject(name);
            host.transform.SetParent(car.transform, false);

            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // Flat rather than positioned: the listener is the player's own car, so there
            // is no distance for a 3D blend to express.
            source.spatialBlend = 0f;
            source.volume = volume;

            return source;
        }

        private static bool IsPaintMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            string name = material.name;

            foreach (string word in NotPaintMaterialWords)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            foreach (string word in PaintMaterialWords)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WirePaintTargetOn(Transform car, Transform lampRoot)
        {
            var slots = new List<CarPaintTarget.PaintSlot>();
            var named = new List<string>();

            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                if (lampRoot != null && renderer.transform.IsChildOf(lampRoot))
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (!IsPaintMaterial(materials[i]))
                    {
                        continue;
                    }

                    slots.Add(new CarPaintTarget.PaintSlot { renderer = renderer, materialIndex = i });
                    named.Add(materials[i].name);
                }
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no material called paint or body, so the garage cannot repaint it.", car);
                return;
            }

            CarPaintTarget paint = car.GetComponent<CarPaintTarget>();

            if (paint == null)
            {
                paint = car.gameObject.AddComponent<CarPaintTarget>();
            }

            paint.EditorSetSlots(slots.ToArray());
            EditorUtility.SetDirty(paint);

            // Named in the log, because which materials count as paintwork is a judgement
            // about someone else's art and is worth being able to read back.
            Debug.Log($"[VehicleDressingTool] '{car.name}' repaints {slots.Count} slot(s): {string.Join(", ", named)}.", car);
        }

        // Where the driver sits and where the look-back camera sits, measured now so the
        // cameras do not have to work it out on the first frame of play.
        //
        // The fractions are written explicitly rather than left to the component's own
        // defaults: a component already serialized into the scene keeps its stored values
        // when the field's default changes, so re-running this would otherwise do nothing.
        private static void MeasureViewPoints(CarController car)
        {
            var points = car.GetComponent<CarParkingGame.Vehicle.VehicleViewPoints>();

            if (points == null)
            {
                points = car.gameObject.AddComponent<CarParkingGame.Vehicle.VehicleViewPoints>();
            }

            // A seat set by hand is left exactly as it was found. Where the driver sits is
            // a matter of taste, and taste beats a default - but only if a rebuild does not
            // quietly throw it away.
            if (points.HandTuned)
            {
                Debug.Log($"[VehicleDressingTool] '{car.name}': seat is hand tuned, left alone.", car);
                return;
            }

            // Where a driver's head would be: behind the windscreen, on the driver's side,
            // with the cabin framing the view. Checked by rendering what each of the three
            // cars actually sees from there; see VehicleViewShotTool.
            //
            // The cars do have cabins - pillars, roof, dash - and all of it was being
            // missed, because the eye was never inside one. At 0.3 forward of the middle it
            // sat over the bonnet; raising it to clear that bonnet only took it above the
            // roof, so "inside the car" and "can see the road" read as a contradiction when
            // they are not. Behind the middle of the body and below the roof line, they are
            // the same place: the windscreen frames the road instead of hiding it.
            //
            // Height is the sensitive one. Too high and the roof takes the upper half of
            // the screen; too low and the dash takes the lower half. 0.8 of the body's
            // height centres the opening on all three cars, whose cabins sit at quite
            // different heights.
            points.EditorSetFractions(-0.42f, -0.10f, 0.80f, -0.45f, 1f);
            points.Measure();
            EditorUtility.SetDirty(points);

            Debug.Log($"[VehicleDressingTool] '{car.name}': driver's eye at {points.DriverEyeLocalPosition:0.00}, look-back at {points.RearViewLocalPosition:0.00}.", car);
        }

        // The name the fitted collider is given, so re-running replaces it rather than
        // stacking a second one on the car.
        private const string BodyColliderName = "BodyCollider";

        // Clearance kept under the fitted collider, measured from where the tyres touch the
        // road. The suspension travels 0.3m and rests at the middle of its travel, so the
        // body can drop another 0.15m under load; 0.2m leaves the collider clear of the
        // road even then, and a collider that catches the road is a car that cannot move.
        private const float RideHeightClearance = 0.2f;

        // Gives the car a collision box that matches its bodywork.
        //
        // This is why driving into a cone did nothing. The cars' authored boxes cover only
        // the upper part of the body: on the Classic the box starts 0.61m above the road,
        // on the Hot Rod 0.39m, on the Muscle 0.29m - while the cones are 0.33m tall and
        // the painted bay plates 0.09m. The car was passing over them with nothing of it
        // low enough to touch. It was never a missing collider on the props.
        //
        // The authored boxes are left alone: they carry the Player and Reverse tags the
        // legacy parking triggers match on, and several colliders on one rigidbody is the
        // normal arrangement.
        private static void FitBodyCollider(CarController car)
        {
            if (!CarParkingGame.Vehicle.VehicleViewPoints.TryMeasureBodyBounds(car.transform, out Bounds body))
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no measurable body mesh; its collision box was left as authored.", car);
                return;
            }

            if (!TryMeasureGroundLine(car, out float groundLocalY))
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' has no usable WheelColliders, so its ride height is unknown.", car);
                return;
            }

            Transform existing = car.transform.Find(BodyColliderName);

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            // ClassicCarRed's root is tagged MainCamera, which is a long-standing
            // mistag noted in Docs/SETUP_CHECKLIST.md. Nothing reads it any more, but a
            // car object claiming to be the main camera is a trap worth removing.
            if (car.CompareTag("MainCamera"))
            {
                car.gameObject.tag = "Untagged";
                EditorUtility.SetDirty(car.gameObject);
                Debug.Log($"[VehicleDressingTool] '{car.name}' was tagged MainCamera; cleared it.", car);
            }

            var host = new GameObject(BodyColliderName);
            host.transform.SetParent(car.transform, false);

            // Tagged like the authored body boxes, so the legacy forward-parking trigger
            // would still see it if anything ever switched that path back on.
            host.tag = "Player";

            float bottom = groundLocalY + RideHeightClearance;
            float top = body.max.y;

            if (top <= bottom)
            {
                Debug.LogWarning($"[VehicleDressingTool] '{car.name}' is shorter than its own ride height; collision box skipped.", car);
                UnityEngine.Object.DestroyImmediate(host);
                return;
            }

            var collider = host.AddComponent<BoxCollider>();

            // Inset slightly so the collision box never pokes out past the paintwork.
            collider.center = new Vector3(body.center.x, (bottom + top) * 0.5f, body.center.z);
            collider.size = new Vector3(body.size.x * 0.96f, top - bottom, body.size.z * 0.98f);

            EditorUtility.SetDirty(host);

            Debug.Log(
                $"[VehicleDressingTool] '{car.name}': body collision box fitted from {bottom - groundLocalY:0.00}m above the road " +
                $"up to {top - groundLocalY:0.00}m ({collider.size:0.00}). Cones are 0.33m tall.",
                car);
        }

        // Where the tyres meet the road, in the car's own local space.
        private static bool TryMeasureGroundLine(CarController car, out float groundLocalY)
        {
            groundLocalY = float.MaxValue;
            bool any = false;

            foreach (CarController.Wheel wheel in car.wheels)
            {
                if (wheel.wheelCollider == null)
                {
                    continue;
                }

                Vector3 contact = wheel.wheelCollider.transform.position
                                  + Vector3.down * wheel.wheelCollider.radius;

                groundLocalY = Mathf.Min(groundLocalY, car.transform.InverseTransformPoint(contact).y);
                any = true;
            }

            return any;
        }

        // The showroom cars are copies with no CarController, so nothing had ever given
        // them a paint target - which is why picking a colour in the garage changed
        // nothing on screen.
        private static void DressShowroomCars()
        {
            var garage = UnityEngine.Object.FindFirstObjectByType<GarageManager>(FindObjectsInactive.Include);

            if (garage == null || garage.ShowroomCarContainer == null)
            {
                Debug.LogWarning("[VehicleDressingTool] No showroom car container found; the garage's colour swatches will not show on the showroom car.");
                return;
            }

            Transform container = garage.ShowroomCarContainer.transform;

            for (int i = 0; i < container.childCount; i++)
            {
                Transform car = container.GetChild(i);
                WirePaintTargetOn(car, null);
            }

            Debug.Log($"[VehicleDressingTool] {container.childCount} showroom car(s) can now be painted.", container);
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


        // Parented to the lamp root rather than to the lamp itself.
        //
        // A lamp is a flattened box now, which means a very non-uniform scale, and Unity
        // skews a child transform under one of those. Hung off the lamp, the beam would be
        // aimed somewhere other than where it was pointed.
        private static Light CreateHeadlightSpot(Transform lampRoot, Vector3 localPosition, CarMeasurements measurements)
        {
            var lightObject = new GameObject("Spot");
            lightObject.transform.SetParent(lampRoot, false);
            lightObject.transform.localPosition = localPosition;

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

        // Which of the car's own material slots are lamp lenses, by the name the artist
        // gave them. "Flicker" is what these packs call the indicator.
        private static readonly string[] HeadlightWords = { "frontlight", "headlight" };
        private static readonly string[] BrakeLightWords = { "brakelight", "rearlight", "taillight" };
        // Not "flicker". These packs use FlickerLights for a headlight that flickers, and
        // on two of the three cars lighting it lit the headlamps - or nothing visible at
        // all. The amber lamp beside the headlight is the park light, where the model has
        // one; where it does not, the car indicates on its tail lamps, which is what a car
        // of this age does anyway.
        private static readonly string[] IndicatorWords = { "parklight", "indicator", "turnsignal" };

        private static LampSurface[] FindLampSurfaces(CarController car, string[] words)
        {
            var found = new List<LampSurface>();

            foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                // The LOD meshes carry the same material names and are never the one being
                // drawn when the player is looking at their own car.
                if (renderer.name.IndexOf("LOD", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    foreach (string word in words)
                    {
                        if (materials[i].name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        found.Add(new LampSurface { renderer = renderer, materialIndex = i });
                        break;
                    }
                }
            }

            return found.ToArray();
        }

        // A marker per side of a lamp, cut to the half of the lens it sits on.
        //
        // The lens is one mesh covering both sides of the car, so it cannot be lit one
        // side at a time - but its triangles can be read, split down the middle of the
        // body, and measured. A marker sized and placed from that half sits exactly where
        // the lamp is, which is what keeps it from looking like something bolted on.
        private static void AddSideMarkers(CarController car, Transform lampRoot, LampSurface[] lenses,
            string name, Material material, List<GameObject> left, List<GameObject> right)
        {
            foreach (LampSurface lens in lenses)
            {
                if (!TryMeasureLensHalves(car, lens, out Bounds leftHalf, out Bounds rightHalf))
                {
                    continue;
                }

                left.Add(CreateLensMarker(lampRoot, name + "Left" + left.Count, OuterLamp(leftHalf, -1f), material));
                right.Add(CreateLensMarker(lampRoot, name + "Right" + right.Count, OuterLamp(rightHalf, 1f), material));
            }
        }

        // The lamp's triangles, in the car's own space, split into the half left of the
        // body's centre line and the half right of it.
        private static bool TryMeasureLensHalves(CarController car, LampSurface lens, out Bounds left, out Bounds right)
        {
            left = default;
            right = default;

            if (lens?.renderer == null)
            {
                return false;
            }

            var filter = lens.renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            if (mesh == null || lens.materialIndex < 0 || lens.materialIndex >= mesh.subMeshCount)
            {
                return false;
            }

            float middle = CarParkingGame.Vehicle.VehicleViewPoints.TryMeasureBodyBounds(car.transform, out Bounds body)
                ? body.center.x
                : 0f;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetTriangles(lens.materialIndex);

            bool anyLeft = false;
            bool anyRight = false;

            foreach (int index in triangles)
            {
                Vector3 inCar = car.transform.InverseTransformPoint(
                    lens.renderer.transform.TransformPoint(vertices[index]));

                if (inCar.x < middle)
                {
                    Encapsulate(ref left, ref anyLeft, inCar);
                }
                else
                {
                    Encapsulate(ref right, ref anyRight, inCar);
                }
            }

            return anyLeft && anyRight;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool any, Vector3 point)
        {
            if (!any)
            {
                bounds = new Bounds(point, Vector3.zero);
                any = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        // The outermost lamp of a half, which is where the indicator is on a car with a
        // row of them across the back. A half that is much wider than it is tall is a row
        // rather than a single lamp, and covering the whole row turns four round lamps
        // into one amber blob; a half that is roughly square is one lamp already and is
        // left alone.
        private static Bounds OuterLamp(Bounds half, float outward)
        {
            if (half.size.x <= half.size.y * 1.8f)
            {
                return half;
            }

            const float Share = 0.45f;

            float width = half.size.x * Share;
            float edge = outward > 0f ? half.max.x : half.min.x;
            float centre = edge - outward * width * 0.5f;

            return new Bounds(
                new Vector3(centre, half.center.y, half.center.z),
                new Vector3(width, half.size.y, half.size.z));
        }

        // Sized to the lens and sat on it. A shade larger, so it covers the glass rather
        // than hiding inside it, and never larger than the lamp it belongs to.
        private static GameObject CreateLensMarker(Transform lampRoot, string name, Bounds half, Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.SetParent(lampRoot, false);
            marker.transform.localPosition = half.center;

            Vector3 size = half.size * 1.04f;

            marker.transform.localScale = new Vector3(
                Mathf.Max(0.04f, size.x),
                Mathf.Max(0.04f, size.y),
                Mathf.Max(0.04f, size.z));

            // Primitives ship with a collider; leaving it on would give the car phantom
            // bumpers that collide with the world and register scoring penalties.
            Collider collider = marker.GetComponent<Collider>();

            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            marker.SetActive(false);
            return marker;
        }

        private static Material LoadOrCreateLampMaterial(string name, Color color)
        {
            string path = $"Assets/GameAssets/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };

            material.SetColor("_BaseColor", color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            Directory.CreateDirectory("Assets/GameAssets/Materials");
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        private static void SetLightGroup(SerializedObject serialized, string fieldName,
            Light[] lights, LampSurface[] surfaces, Color litColor, GameObject[] glowObjects)
        {
            SerializedProperty group = serialized.FindProperty(fieldName);

            if (group == null)
            {
                Debug.LogWarning($"[VehicleDressingTool] No field '{fieldName}' on VehicleLights.");
                return;
            }

            SerializedProperty glowArray = group.FindPropertyRelative("glowObjects");
            SerializedProperty lightArray = group.FindPropertyRelative("lights");
            SerializedProperty surfaceArray = group.FindPropertyRelative("surfaces");
            SerializedProperty colorProperty = group.FindPropertyRelative("litColor");

            if (glowArray == null || lightArray == null || surfaceArray == null)
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

            surfaceArray.arraySize = surfaces.Length;

            for (int i = 0; i < surfaces.Length; i++)
            {
                SerializedProperty element = surfaceArray.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("renderer").objectReferenceValue = surfaces[i].renderer;
                element.FindPropertyRelative("materialIndex").intValue = surfaces[i].materialIndex;
            }

            if (colorProperty != null)
            {
                colorProperty.colorValue = litColor;
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

            // Written rather than left to the field's default. A default only applies to a
            // component that does not exist yet, and this one is already in the scene with
            // the last build's number serialised into it.
            SerializedProperty pitch = serialized.FindProperty("seatedPitchDegrees");

            if (pitch != null)
            {
                // Barely tilted. From inside a cabin the windscreen already decides how
                // much road is in shot, and pitching down past this just points the view
                // at the dashboard.
                pitch.floatValue = 2f;
            }

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
