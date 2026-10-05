using System.Collections.Generic;
using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Smoke off the tyres and black marks on the road, both driven by how hard the wheels
    // are actually slipping rather than by which button is held.
    //
    // Slip is what makes a tyre smoke, so slip is what the effect reads: a car that locks
    // up at speed smokes, and the same car dragged against a kerb at walking pace does
    // not. WheelCollider reports it per wheel and per frame, which is exactly the right
    // granularity - the inside wheel in a tight turn can be sliding while the outside one
    // is gripping.
    [RequireComponent(typeof(CarController))]
    public class VehicleTyreEffects : MonoBehaviour
    {
        [Tooltip("Combined slip above which the tyre starts to smoke and mark the road.")]
        [SerializeField] private float slipThreshold = 0.42f;

        [Tooltip("Slip at which the smoke is at full rate.")]
        [SerializeField] private float slipAtFull = 1.1f;

        [Tooltip("Below this the car is manoeuvring, and a tyre scrubbing at walking pace neither smokes nor marks.")]
        [SerializeField] private float needsKmh = 7f;

        [SerializeField] private float maxSmokePerSecond = 48f;

        [Tooltip("How long a skid mark stays on the road.")]
        [SerializeField] private float markSeconds = 14f;

        [SerializeField] private Material markMaterial;
        [SerializeField] private Material smokeMaterial;

        private CarController car;
        private readonly List<Tyre> tyres = new List<Tyre>();

        private sealed class Tyre
        {
            public WheelCollider wheel;
            public ParticleSystem smoke;
            public ParticleSystem.EmissionModule emission;
            public TrailRenderer mark;
        }

        private void Awake()
        {
            car = GetComponent<CarController>();
            Build();
        }

        private void Build()
        {
            if (car.wheels == null)
            {
                return;
            }

            foreach (CarController.Wheel wheel in car.wheels)
            {
                if (wheel.wheelCollider == null)
                {
                    continue;
                }

                tyres.Add(CreateTyre(wheel.wheelCollider));
            }
        }

        private Tyre CreateTyre(WheelCollider wheel)
        {
            // Both effects hang off the car rather than off the wheel, and are moved to the
            // contact patch each frame. Parented to a wheel, a skid mark would turn and
            // steer with it and draw a curl on the road instead of a line.
            var host = new GameObject("Tyre " + wheel.name);
            host.transform.SetParent(transform, false);

            var smoke = host.AddComponent<ParticleSystem>();
            ShapeSmoke(smoke, wheel.radius);

            var mark = host.AddComponent<TrailRenderer>();
            ShapeMark(mark, wheel.radius);

            ParticleSystem.EmissionModule emission = smoke.emission;
            emission.rateOverTime = 0f;

            return new Tyre { wheel = wheel, smoke = smoke, emission = emission, mark = mark };
        }

        private void ShapeSmoke(ParticleSystem smoke, float radius)
        {
            ParticleSystem.MainModule main = smoke.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.9f;
            main.startSpeed = 0.6f;
            main.startSize = radius * 2.4f;
            main.startColor = new Color(0.82f, 0.82f, 0.84f, 0.5f);
            main.gravityModifier = -0.04f;

            // World space, so a puff left behind stays where the tyre was rather than
            // being dragged along with the car.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.4f;

            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

            ParticleSystem.ColorOverLifetimeModule fade = smoke.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());

            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (smokeMaterial != null)
            {
                renderer.sharedMaterial = smokeMaterial;
            }
        }

        private static Gradient Fade()
        {
            var gradient = new Gradient();

            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });

            return gradient;
        }

        private void ShapeMark(TrailRenderer mark, float radius)
        {
            mark.time = markSeconds;
            mark.startWidth = radius * 0.55f;
            mark.endWidth = radius * 0.55f;
            mark.minVertexDistance = 0.12f;
            mark.autodestruct = false;
            mark.emitting = false;
            mark.numCapVertices = 2;
            mark.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mark.receiveShadows = false;

            mark.startColor = new Color(0.05f, 0.05f, 0.06f, 0.75f);
            mark.endColor = new Color(0.05f, 0.05f, 0.06f, 0f);

            if (markMaterial != null)
            {
                mark.sharedMaterial = markMaterial;
            }
        }

        private void Update()
        {
            float speed = car.CurrentSpeedKmh;

            foreach (Tyre tyre in tyres)
            {
                Drive(tyre, speed);
            }
        }

        private void Drive(Tyre tyre, float speedKmh)
        {
            // Off the ground: nothing to smoke against and nothing to mark.
            if (!tyre.wheel.GetGroundHit(out WheelHit hit) || speedKmh < needsKmh)
            {
                Stop(tyre);
                return;
            }

            float slip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);

            if (slip < slipThreshold)
            {
                Stop(tyre);
                return;
            }

            // Just above the road. On it, the mark z-fights with the tarmac.
            tyre.smoke.transform.position = hit.point + Vector3.up * 0.04f;
            tyre.mark.transform.position = hit.point + Vector3.up * 0.02f;

            float hard = Mathf.InverseLerp(slipThreshold, slipAtFull, slip);

            tyre.emission.rateOverTime = maxSmokePerSecond * hard;
            tyre.mark.emitting = true;
        }

        private static void Stop(Tyre tyre)
        {
            tyre.emission.rateOverTime = 0f;

            // Broken rather than stopped: emitting false with the trail left where it is
            // would join this skid to the next one with a straight line across the car park.
            if (tyre.mark.emitting)
            {
                tyre.mark.emitting = false;
            }
        }
    }
}
