namespace CarParkingGame.Missions
{
    // How a mission is being played, as opposed to what the mission is.
    //
    // The same bay and the same validation serve three modes: practice and challenge both
    // drop you at the start line with only that level's props in the world, and free roam
    // runs no mission at all. What separates practice from challenge is the clock and who
    // picks the level, not how the level is entered.
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

        // Challenge used to leave the whole map dressed and let the player drive from one
        // bay to the next. That worked while every level was a patch of the circuit. It
        // cannot work now: the levels are sealed car parks on their own site, with no road
        // between them and no way into one from outside. So challenge sets the car down at
        // each level in turn, the same as practice, and ScreenFade covers the cut.
        public static MissionLaunchOptions Challenge(float seconds) => new MissionLaunchOptions
        {
            teleportToStart = true,
            isolateEnvironment = true,
            timeLimitOverride = seconds,
            failOnTimeout = true
        };
    }
}
