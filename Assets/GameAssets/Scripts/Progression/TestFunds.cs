namespace CarParkingGame.Progression
{
    // A floor under the player's money while the game is being demonstrated, so the garage
    // can be shown off - buy a car, repaint it, buy another - without the demo running out
    // of coins halfway through.
    //
    // It is a floor topped up when a save is loaded, not a grant and not a running cheat.
    // Buying a car still takes the money, so a demo shows the price coming off; the next
    // launch puts it back. Those two together are what a demo needs and a shipped game
    // must not have.
    //
    // Set Enabled to false before release. It goes off at the same time as
    // MissionUnlocking.EveryMissionOpen - the two of them are the demo, and neither is
    // meant to outlive it. Nothing else has to change: a save with coins already in it
    // keeps them, and progress already earned is still there.
    public static class TestFunds
    {
        // Not a const: a const would fold the guard that reads it into unreachable code
        // and the compiler would warn about it.
        public static readonly bool Enabled = true;

        public const int Coins = 100000;

        public static bool TryTopUp(SaveData data)
        {
            if (!Enabled || data == null || data.coins >= Coins)
            {
                return false;
            }

            data.coins = Coins;
            return true;
        }
    }
}
