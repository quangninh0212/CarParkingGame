namespace CarParkingGame.Progression
{
    // Whether Practice mode makes the player earn each course, or opens the whole lot.
    //
    // Reviewing thirty courses means driving into each one and looking at it. Playing
    // through twenty-nine to reach the thirtieth is not a way to find a wall in the wrong
    // place, and a reviewer who cannot reach a course cannot report what is wrong with it,
    // so while the courses are being checked every one is open.
    //
    // Set EveryMissionOpen to false and sequential unlocking is back, with nothing else to
    // change. Being open writes nothing to the save file, so progress already earned is
    // still there and still correct when the switch flips.
    public static class MissionUnlocking
    {
        // Not a const: a const would fold the guards below into unreachable code and the
        // compiler would warn about every one of them.
        public static readonly bool EveryMissionOpen = true;

        public static bool IsOpen(int missionId, MissionProgressData progress)
        {
            if (EveryMissionOpen)
            {
                return true;
            }

            return missionId == SaveData.FirstMissionId || (progress != null && progress.unlocked);
        }
    }
}
