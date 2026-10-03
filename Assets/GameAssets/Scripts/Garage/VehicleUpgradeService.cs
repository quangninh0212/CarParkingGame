using CarParkingGame.Core;
using CarParkingGame.Progression;

namespace CarParkingGame.Garage
{
    public static class VehicleUpgradeService
    {
        public static int GetLevel(CarSaveEntry entry, UpgradeKind kind)
        {
            if (entry == null)
            {
                return 0;
            }

            switch (kind)
            {
                case UpgradeKind.Engine:
                    return entry.engineLevel;
                case UpgradeKind.Brakes:
                    return entry.brakeLevel;
                case UpgradeKind.Handling:
                    return entry.handlingLevel;
                default:
                    return 0;
            }
        }

        public static void Apply(CarController car, CarSaveEntry entry, UpgradeRules rules)
        {
            if (car == null)
            {
                return;
            }

            if (entry == null || rules == null)
            {
                car.SetPerformanceMultipliers(1f, 1f, 1f);
                return;
            }

            car.SetPerformanceMultipliers(
                rules.MultiplierFor(UpgradeKind.Engine, entry.engineLevel),
                rules.MultiplierFor(UpgradeKind.Brakes, entry.brakeLevel),
                rules.MultiplierFor(UpgradeKind.Handling, entry.handlingLevel));
        }

        public static bool TryUpgrade(CarSaveEntry entry, UpgradeKind kind, UpgradeRules rules, out int spent)
        {
            spent = 0;

            if (entry == null || rules == null)
            {
                return false;
            }

            int level = GetLevel(entry, kind);

            if (level >= rules.MaxLevel)
            {
                return false;
            }

            int cost = rules.CostForNextLevel(level);

            if (!EconomyManager.TrySpend(cost))
            {
                return false;
            }

            SetLevel(entry, kind, level + 1);
            SaveManager.Save();
            spent = cost;
            return true;
        }

        private static void SetLevel(CarSaveEntry entry, UpgradeKind kind, int level)
        {
            switch (kind)
            {
                case UpgradeKind.Engine:
                    entry.engineLevel = level;
                    break;
                case UpgradeKind.Brakes:
                    entry.brakeLevel = level;
                    break;
                case UpgradeKind.Handling:
                    entry.handlingLevel = level;
                    break;
            }
        }
    }
}
