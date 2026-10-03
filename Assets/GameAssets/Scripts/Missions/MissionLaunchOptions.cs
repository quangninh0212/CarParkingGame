namespace CarParkingGame.Missions
{
    // How a mission is being played, as opposed to what the mission is.
    //
    // The same bay and the same validation serve three modes: practice drops you at the
    // start line with only that mission's props in the world, challenge leaves the whole
    // map standing and expects you to drive to the next bay yourself, and free roam runs
    // no mission at all.
    public struct MissionLaunchOptions
    {
        // Put the car on the mission's start point. Off, the car keeps driving from
        // wherever it already is.
        public bool teleportToStart;

        // Hide every other mission's props. Off, the whole map stays dressed.
        public bool isolateEnvironment;

        // Seconds, overriding the definition's own limit. 0 or less keeps the definition's.
        public float timeLimitOverride;

        // Count a timeout as a failure. Only consulted when there is a limit to run out of.
        public bool failOnTimeout;

        public static MissionLaunchOptions Practice => new MissionLaunchOptions
        {
            teleportToStart = true,
            isolateEnvironment = true,
            timeLimitOverride = 0f,
            failOnTimeout = true
        };

        public static MissionLaunchOptions Challenge(float seconds) => new MissionLaunchOptions
        {
            teleportToStart = false,
            isolateEnvironment = false,
            timeLimitOverride = seconds,
            failOnTimeout = true
        };
    }
}
