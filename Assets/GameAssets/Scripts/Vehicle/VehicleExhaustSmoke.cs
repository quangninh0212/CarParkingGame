using UnityEngine;

namespace CarParkingGame.Vehicle
{
    // Exhaust smoke, heavier when the throttle is open and thinner when the car is
    // coasting.
    //
    // A car idling in a bay puffs gently; one pulling away smokes. Rate rather than an
    // on-off switch, because a constant plume reads as a fire and nothing at all reads as
    // a model rather than a car.
    [RequireComponent(typeof(CarController))]
    public class VehicleExhaustSmoke : MonoBehaviour
    {
        [SerializeField] private Material smokeMaterial;

        [Tooltip("Where the pipe is, in the car's own space. Set by the dressing tool from the body's own bounds.")]
        [SerializeField] private Vector3 pipeLocalPosition = new Vector3(0.35f, 0.25f, -2.1f);

        [SerializeField] private float idlePerSecond = 5f;
        [SerializeField] private float throttlePerSecond = 26f;

        private CarController car;
        private ParticleSystem smoke;
        private ParticleSystem.EmissionModule emission;

        private void Awake()
        {
            car = GetComponent<CarController>();
            Build();
        }

        private void Build()
        {
            var host = new GameObject("ExhaustSmoke");
            host.transform.SetParent(transform, false);
            host.transform.localPosition = pipeLocalPosition;

            // Pointing back and slightly down, the way a pipe does.
            host.transform.localRotation = Quaternion.Euler(100f, 0f, 0f);

            smoke = host.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = smoke.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.8f;
            main.startSize = 0.22f;
            main.startColor = new Color(0.72f, 0.72f, 0.75f, 0.35f);
            main.gravityModifier = -0.05f;

            // World space, so the car drives out from under its own smoke instead of
            // towing it along.
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            ParticleSystem.ShapeModule shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.04f;

            ParticleSystem.SizeOverLifetimeModule size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.6f));

            ParticleSystem.ColorOverLifetimeModule fade = smoke.colorOverLifetime;
            fade.enabled = true;

            var gradient = new Gradient();

            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.4f, 0f), new GradientAlphaKey(0f, 1f) });

            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (smokeMaterial != null)
            {
                renderer.sharedMaterial = smokeMaterial;
            }

            emission = smoke.emission;
            emission.rateOverTime = idlePerSecond;
        }

        private void Update()
        {
            // The engine is off in the menu and while paused, and so is the pipe.
            if (!car.VehicleEnabled || Time.timeScale <= 0f)
            {
                emission.rateOverTime = 0f;
                return;
            }

            float throttle = Mathf.Abs(car.ThrottleInput);
            emission.rateOverTime = Mathf.Lerp(idlePerSecond, throttlePerSecond, throttle);
        }

#if UNITY_EDITOR
        public void EditorSetPipe(Vector3 localPosition)
        {
            pipeLocalPosition = localPosition;
        }
#endif
    }
}
