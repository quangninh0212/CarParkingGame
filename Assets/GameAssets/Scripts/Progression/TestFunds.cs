namespace CarParkingGame.Progression
{
    // A one-off pile of coins, so the garage can be tested without grinding fifty levels
    // for it.
    //
    // It is granted once and recorded in the save, not topped up on every load. Topping up
    // would make buying a car free in effect, and the thing being tested is whether buying
    // one takes the money.
    //
    // Set Enabled to false before release. Coins already granted stay granted; turning it
    // off only stops new saves being given any.
    public static class TestFunds
    {
        // Not a const: a const would fold the guards that read it into unreachable code
        // and the compiler would warn about every one of them.
        public static readonly bool Enabled = true;

        public const int Coins = 100000;

        // Bumped to hand out another round to saves that already had the last one.
        public const int CurrentGrant = 1;

        public static bool TryGrant(SaveData data)
        {
            if (!Enabled || data == null || data.testGrant >= CurrentGrant)
            {
                return false;
            }

            data.testGrant = CurrentGrant;
            data.coins += Coins;
            return true;
        }
    }
}
