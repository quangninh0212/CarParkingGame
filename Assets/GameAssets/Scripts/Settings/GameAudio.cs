using CarParkingGame.Core;
using CarParkingGame.Progression;

namespace CarParkingGame.Settings
{
    // The player's two volume sliders, as numbers anything making a noise can multiply by.
    //
    // There was a component for this, one per AudioSource, and it was never put on a
    // single source in the scene - so both sliders moved and nothing happened. It could
    // not have covered everything anyway: the engine and the horn build their sources in
    // Awake, and the engine puts four of them on one object, which a per-object component
    // cannot address.
    //
    // Read at the moment a volume is set rather than pushed out on an event, so a source
    // created mid-game is as current as one that has been playing since the menu.
    public static class GameAudio
    {
        public static float Music => Read(true);

        public static float Sfx => Read(false);

        private static float Read(bool music)
        {
            SettingsSaveData settings = SaveManager.Data?.settings;

            if (settings == null)
            {
                return 1f;
            }

            return music ? settings.musicVolume : settings.sfxVolume;
        }
    }
}
