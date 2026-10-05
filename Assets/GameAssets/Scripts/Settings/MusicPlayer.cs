using UnityEngine;

namespace CarParkingGame.Settings
{
    // The background music, looping for as long as the game is open.
    //
    // One source on one object that survives nothing in particular - the game is a single
    // scene, so there is nothing to survive. It follows the music slider every frame
    // rather than on an event, which costs one multiply and means the slider moves the
    // music while the player is still dragging it.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip track;

        [Tooltip("How loud the track is with the music slider at full.")]
        [SerializeField, Range(0f, 1f)] private float trackVolume = 0.45f;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();

            source.clip = track;
            source.loop = true;
            source.playOnAwake = false;

            // Flat, not positioned. Music does not come from anywhere in the world.
            source.spatialBlend = 0f;
        }

        private void Start()
        {
            if (source.clip != null)
            {
                source.Play();
            }
        }

        private void Update()
        {
            source.volume = trackVolume * GameAudio.Music;
        }
    }
}
