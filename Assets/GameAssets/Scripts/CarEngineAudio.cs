using UnityEngine;

/// <summary>
/// Layered engine audio: an idle bed under two throttle loops whose pitch
/// tracks speed. The sources are built at runtime, so the car objects only
/// need this one component and its clips.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarEngineAudio : MonoBehaviour
{
    public AudioClip startClip;
    public AudioClip idleClip;
    public AudioClip onThrottleClip;
    public AudioClip offThrottleClip;

    [Header("Speed to pitch")]
    public float topSpeed = 25f;
    public float minPitch = 0.85f;
    public float maxPitch = 2f;

    [Header("Mix")]
    public float idleVolume = 0.45f;
    public float engineVolume = 0.6f;
    public float blendSpeed = 4f;

    private Rigidbody carRB;
    private CarController carController;

    private AudioSource startSource;
    private AudioSource idleSource;
    private AudioSource onThrottleSource;
    private AudioSource offThrottleSource;

    private bool wasRunning;

    private void Awake()
    {
        carRB = GetComponent<Rigidbody>();
        carController = GetComponent<CarController>();

        startSource = CreateSource(startClip, false);
        idleSource = CreateSource(idleClip, true);
        onThrottleSource = CreateSource(onThrottleClip, true);
        offThrottleSource = CreateSource(offThrottleClip, true);

        // Skip the ignition sound on the frame the scene loads; the menu sets
        // timeScale to 0 right after, and the first real resume starts the car.
        wasRunning = Time.timeScale > 0f;
    }

    private AudioSource CreateSource(AudioClip clip, bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();

        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = 0f;
        source.spatialBlend = 1f;
        source.minDistance = 4f;
        source.maxDistance = 60f;

        return source;
    }

    private void Update()
    {
        // Menu and pause both freeze time, and the engine goes quiet with them.
        bool running = Time.timeScale > 0f;

        if (running && !wasRunning)
        {
            StartEngine();
        }
        else if (!running && wasRunning)
        {
            StopEngine();
        }

        wasRunning = running;

        if (!running)
            return;

        float speed01 = Mathf.Clamp01(carRB.linearVelocity.magnitude / topSpeed);
        float throttle = carController != null
            ? Mathf.Clamp01(Mathf.Abs(carController.MoveInput))
            : 0f;

        float pitch = Mathf.Lerp(minPitch, maxPitch, speed01);
        onThrottleSource.pitch = pitch;
        offThrottleSource.pitch = pitch;

        float step = blendSpeed * Time.unscaledDeltaTime;

        Fade(idleSource, idleVolume * (1f - speed01), step);
        Fade(onThrottleSource, engineVolume * speed01 * throttle, step);
        Fade(offThrottleSource, engineVolume * speed01 * (1f - throttle), step);
    }

    private static void Fade(AudioSource source, float target, float step)
    {
        source.volume = Mathf.MoveTowards(source.volume, target, step);
    }

    private void StartEngine()
    {
        if (startSource.clip != null)
        {
            startSource.volume = 1f;
            startSource.Play();
        }

        PlayLoop(idleSource);
        PlayLoop(onThrottleSource);
        PlayLoop(offThrottleSource);

        idleSource.volume = idleVolume;
    }

    private static void PlayLoop(AudioSource source)
    {
        if (source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    private void StopEngine()
    {
        startSource.Stop();
        idleSource.Stop();
        onThrottleSource.Stop();
        offThrottleSource.Stop();
    }
}
