using System.Collections.Generic;
using CarParkingGame.Garage;
using CarParkingGame.Parking;
using CarParkingGame.Progression;
using CarParkingGame.Settings;
using UnityEditor;
using UnityEngine;

namespace CarParkingGame.EditorTools
{
    // Headless checks for the scoring, economy and parking-geometry maths. Everything
    // here is pure logic or throwaway temporary objects: no scene, no save file and no
    // PlayerPrefs are touched.
    public static class GameplaySelfCheck
    {
        [MenuItem("Tools/Car Parking/Run Gameplay Self-Check")]
        public static void RunFromMenu()
        {
            int failures = RunChecks();

            if (failures == 0)
            {
                Debug.Log("[GameplaySelfCheck] All checks passed.");
            }
        }

        public static void RunFromCommandLine()
        {
            int failures = RunChecks();
            Debug.Log($"[GameplaySelfCheck] Finished with {failures} failure(s).");
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int RunChecks()
        {
            var failures = new List<string>();

            CheckStarThresholds(failures);
            CheckImpulseScaling(failures);
            CheckCollisionDebounce(failures);
            CheckOvertimePenalty(failures);
            CheckRewards(failures);
            CheckParkingGeometry(failures);
            CheckParkingValidatorScenarios(failures);
            CheckScaledLegacyPlate(failures);
            CheckUpgradeRules(failures);
            CheckQualityTiers(failures);
            CheckGarageSaveFields(failures);

            foreach (string failure in failures)
            {
                Debug.LogError("[GameplaySelfCheck] FAILED: " + failure);
            }

            return failures.Count;
        }

        private static void CheckStarThresholds(List<string> failures)
        {
            ScoreRules rules = ScoreRules.CreateDefault();

            Check(failures, rules.StarsFor(100) == 3, "a perfect score should be three stars");
            Check(failures, rules.StarsFor(90) == 3, "90 should be the bottom of three stars");
            Check(failures, rules.StarsFor(89) == 2, "89 should be two stars");
            Check(failures, rules.StarsFor(70) == 2, "70 should be the bottom of two stars");
            Check(failures, rules.StarsFor(69) == 1, "69 should be one star");
            Check(failures, rules.StarsFor(50) == 1, "50 should be the bottom of one star");
            Check(failures, rules.StarsFor(49) == 0, "49 should be no stars");
            Check(failures, rules.IsFailingScore(49), "below 50 should count as a failed mission");
            Check(failures, !rules.IsFailingScore(50), "50 should not count as a failed mission");
        }

        private static void CheckImpulseScaling(List<string> failures)
        {
            ScoreRules rules = ScoreRules.CreateDefault();

            Check(failures, rules.PenaltyForTag("Cone") == 3, "a cone should cost 3 points");
            Check(failures, rules.PenaltyForTag("Barrier") == 5, "a barrier should cost 5 points");
            Check(failures, rules.PenaltyForTag("Vehicle") == 10, "hitting a vehicle should cost 10 points");
            Check(failures, rules.PenaltyForTag("SomethingElse") == rules.DefaultCollisionPenalty, "an unlisted tag should fall back to the default penalty");

            int light = rules.ScaleByImpulse(10, 1f);
            int normal = rules.ScaleByImpulse(10, 5f);
            int heavy = rules.ScaleByImpulse(10, 20f);

            Check(failures, light == 5, "a light brush should cost half");
            Check(failures, normal == 10, "a normal contact should cost the base penalty");
            Check(failures, heavy == 20, "a heavy impact should cost double");
            Check(failures, rules.ScaleByImpulse(1, 0.1f) >= 1, "even the lightest contact should still cost at least a point");
            Check(failures, rules.ScaleByImpulse(0, 5f) == 0, "a zero penalty should stay zero");
        }

        private static void CheckCollisionDebounce(List<string> failures)
        {
            var tracker = new MissionScoreTracker(ScoreRules.CreateDefault());

            Check(failures, tracker.Score == 100, "a mission should start on 100 points");

            bool first = tracker.RegisterCollision("Cone", 5f, 1);

            Check(failures, first, "the first contact with a collider should count");
            Check(failures, tracker.Score == 97, "one cone at normal force should cost 3 points");
            Check(failures, tracker.Collisions == 1, "one contact should count as one collision");

            bool repeat = tracker.RegisterCollision("Cone", 5f, 1);

            Check(failures, !repeat, "scraping the same cone again inside the cooldown should not count");
            Check(failures, tracker.Score == 97, "a debounced contact should not cost points");
            Check(failures, tracker.Collisions == 1, "a debounced contact should not count as a new collision");

            bool otherCollider = tracker.RegisterCollision("Cone", 5f, 2);

            Check(failures, otherCollider, "a different cone should count even inside the cooldown");
            Check(failures, tracker.Score == 94, "a second distinct cone should cost another 3 points");

            tracker.Tick(1f);
            bool afterCooldown = tracker.RegisterCollision("Cone", 5f, 1);

            Check(failures, afterCooldown, "the same cone should count again once the cooldown has passed");
            Check(failures, tracker.Score == 91, "a contact after the cooldown should cost points again");

            tracker.ApplyPenalty(500);

            Check(failures, tracker.Score == 0, "the score should never go below zero");
            Check(failures, tracker.Stars == 0, "a zero score should award no stars");
        }

        private static void CheckOvertimePenalty(List<string> failures)
        {
            var withinTime = new MissionScoreTracker(ScoreRules.CreateDefault());
            withinTime.Tick(4f);

            Check(failures, withinTime.ApplyOvertimePenalty(10f) == 0, "finishing inside the time limit should cost nothing");
            Check(failures, withinTime.ApplyOvertimePenalty(0f) == 0, "an untimed mission should never charge an overtime penalty");
            Check(failures, withinTime.Score == 100, "no overtime penalty should leave the score untouched");

            var late = new MissionScoreTracker(ScoreRules.CreateDefault());
            late.Tick(15f);

            Check(failures, late.ApplyOvertimePenalty(10f) == 5, "five seconds over should cost five points");
            Check(failures, late.Score == 95, "the overtime penalty should come off the score");

            var veryLate = new MissionScoreTracker(ScoreRules.CreateDefault());
            veryLate.Tick(500f);

            Check(failures, veryLate.ApplyOvertimePenalty(10f) == veryLate.Rules.MaxOvertimePenalty, "the overtime penalty should be capped");
        }

        private static void CheckRewards(List<string> failures)
        {
            Check(failures, EconomyManager.BaseRewardFor(3, 300) == 300, "three stars should pay the mission's full reward");
            Check(failures, EconomyManager.BaseRewardFor(2, 300) == 200, "two stars should pay two thirds");
            Check(failures, EconomyManager.BaseRewardFor(1, 300) == 100, "one star should pay a third");
            Check(failures, EconomyManager.BaseRewardFor(0, 300) == 0, "a failed mission should pay nothing");
            Check(failures, EconomyManager.BaseRewardFor(3, 0) == 300, "a mission with no reward set should fall back to 100 coins per star");

            int firstClear = EconomyManager.CalculateReward(3, 300, isFirstCompletion: true, improved: false);
            int improvedReplay = EconomyManager.CalculateReward(3, 300, isFirstCompletion: false, improved: true);
            int plainReplay = EconomyManager.CalculateReward(3, 300, isFirstCompletion: false, improved: false);

            Check(failures, firstClear == 300, "a first clear should pay in full");
            Check(failures, improvedReplay == 300, "beating your own record should pay in full");
            Check(failures, plainReplay == 75, "a replay that improves nothing should pay only a quarter");
            Check(failures, plainReplay < firstClear, "replaying must never pay as much as a first clear");

            int tinyReplay = EconomyManager.CalculateReward(1, 30, isFirstCompletion: false, improved: false);

            Check(failures, tinyReplay == EconomyManager.MinimumRepeatReward, "a very small replay reward should be raised to the minimum");
            Check(failures, EconomyManager.CalculateReward(0, 300, isFirstCompletion: true, improved: true) == 0, "no stars should pay nothing even on a first attempt");
        }

        private static void CheckParkingGeometry(List<string> failures)
        {
            var zoneObject = new GameObject("SelfCheckZone");
            var vehicleObject = new GameObject("SelfCheckVehicle");

            try
            {
                ParkingZone zone = zoneObject.AddComponent<ParkingZone>();
                zone.EditorSetBox(Vector3.zero, new Vector3(3f, 2.5f, 6f));

                Transform vehicle = vehicleObject.transform;
                var halfExtents = new Vector2(0.9f, 2.1f);

                vehicle.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Check(failures, Mathf.Approximately(zone.GetContainment(vehicle, halfExtents), 1f), "a car centred in the bay should be fully contained");

                vehicle.position = new Vector3(0f, 0f, 0.4f);
                Check(failures, Mathf.Approximately(zone.GetContainment(vehicle, halfExtents), 1f), "a car parked slightly forward should still be fully contained");

                vehicle.position = new Vector3(0f, 0f, 3.5f);
                Check(failures, zone.GetContainment(vehicle, halfExtents) < 1f, "a car hanging out of the bay should not be fully contained");

                vehicle.position = new Vector3(12f, 0f, 0f);
                Check(failures, Mathf.Approximately(zone.GetContainment(vehicle, halfExtents), 0f), "a car nowhere near the bay should have no containment");

                vehicle.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
                Check(failures, zone.GetContainment(vehicle, halfExtents) < 1f, "a car parked sideways should not be fully contained");

                vehicle.rotation = Quaternion.Euler(0f, 10f, 0f);
                Check(failures, Mathf.Abs(zone.GetHeadingError(vehicle, false) - 10f) < 0.01f, "a car ten degrees off should report a ten degree heading error");

                vehicle.rotation = Quaternion.Euler(0f, 170f, 0f);
                Check(failures, Mathf.Abs(zone.GetHeadingError(vehicle, true) - 10f) < 0.01f, "a car facing the other way should be ten degrees off when the opposite heading is allowed");
                Check(failures, Mathf.Abs(zone.GetHeadingError(vehicle, false) - 170f) < 0.01f, "a car facing the other way should be fully wrong when the opposite heading is not allowed");
            }
            finally
            {
                Object.DestroyImmediate(vehicleObject);
                Object.DestroyImmediate(zoneObject);
            }
        }

        // Drives the validator's state machine with an explicit time step: the closest this
        // can get to "park the car and see what happens" without a human at the controls.
        private static void CheckParkingValidatorScenarios(List<string> failures)
        {
            RunParkingScenario(
                failures,
                "a car parked correctly should validate",
                ParkingType.Forward,
                Vector3.zero,
                Quaternion.identity,
                Vector3.zero,
                expectComplete: true);

            RunParkingScenario(
                failures,
                "a car outside the bay should never validate",
                ParkingType.Forward,
                new Vector3(12f, 0f, 0f),
                Quaternion.identity,
                Vector3.zero,
                expectComplete: false);

            RunParkingScenario(
                failures,
                "a car parked crooked should never validate",
                ParkingType.Forward,
                Vector3.zero,
                Quaternion.Euler(0f, 40f, 0f),
                Vector3.zero,
                expectComplete: false);

            RunParkingScenario(
                failures,
                "a car still rolling through the bay should never validate",
                ParkingType.Forward,
                Vector3.zero,
                Quaternion.identity,
                new Vector3(0f, 0f, 3f),
                expectComplete: false);

            // Reverse missions require the car to have actually reversed in. Sitting in the
            // bay with no reverse motion must not count.
            RunParkingScenario(
                failures,
                "a reverse mission should not validate without reversing in",
                ParkingType.Reverse,
                Vector3.zero,
                Quaternion.identity,
                Vector3.zero,
                expectComplete: false);
        }

        // Regression test for the real scene: legacy triggers are thin plates scaled to
        // (1, 2.3, 0.19). A bay on such a plate must measure in metres, not in the plate's
        // squashed local units, or no car can ever fit.
        private static void CheckScaledLegacyPlate(List<string> failures)
        {
            var plate = new GameObject("ScaledPlate");
            var vehicle = new GameObject("PlateVehicle");

            try
            {
                plate.transform.localScale = new Vector3(1f, 2.3f, 0.19f);

                ParkingZone zone = plate.AddComponent<ParkingZone>();
                zone.EditorSetBox(new Vector3(0f, 0f, 2.4f), new Vector3(2.8f, 2.5f, 6f));
                zone.EditorSetParkedFacingBackward(true);

                var halfExtents = new Vector2(0.97f, 1.84f);

                // Nose-in at the end line: centre 2.1 m out from the plate, facing it.
                vehicle.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2.1f), Quaternion.Euler(0f, 180f, 0f));

                Check(failures, Mathf.Approximately(zone.GetContainment(vehicle.transform, halfExtents), 1f),
                    "a car parked in a bay on a scaled legacy plate should be fully contained");
                Check(failures, zone.GetHeadingError(vehicle.transform, false) < 1f,
                    "a nose-in car should face the bay's parked heading");

                vehicle.transform.rotation = Quaternion.identity;
                Check(failures, zone.GetHeadingError(vehicle.transform, false) > 179f,
                    "a car facing out of a nose-in bay should be fully wrong when opposite heading is not allowed");
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
                Object.DestroyImmediate(plate);
            }
        }

        private static void RunParkingScenario(
            List<string> failures,
            string description,
            ParkingType parkingType,
            Vector3 vehiclePosition,
            Quaternion vehicleRotation,
            Vector3 velocity,
            bool expectComplete)
        {
            var zoneObject = new GameObject("ScenarioZone");
            var vehicleObject = new GameObject("ScenarioVehicle");

            try
            {
                ParkingZone zone = zoneObject.AddComponent<ParkingZone>();
                zone.EditorSetBox(Vector3.zero, new Vector3(3f, 2.5f, 6f));

                var body = vehicleObject.AddComponent<Rigidbody>();
                body.useGravity = false;

                // Must stay non-kinematic: a kinematic body does not keep an assigned
                // linearVelocity, so the "still rolling" case would read as stopped.
                body.isKinematic = false;
                body.linearVelocity = velocity;

                vehicleObject.transform.SetPositionAndRotation(vehiclePosition, vehicleRotation);

                ParkingValidator validator = zoneObject.AddComponent<ParkingValidator>();
                validator.Zone = zone;
                validator.Configure(parkingType, 2f, 15f, 1.5f, 1f, true);
                validator.SetVehicle(vehicleObject.transform);

                bool validatedFired = false;
                validator.Validated += () => validatedFired = true;
                validator.Begin();

                // Two seconds of game time in 0.25s steps, past the 1.5s hold requirement.
                for (int step = 0; step < 8; step++)
                {
                    validator.Tick(0.25f);
                }

                Check(failures, validator.IsComplete == expectComplete, description);
                Check(failures, validatedFired == expectComplete, description + " (Validated event)");
            }
            finally
            {
                Object.DestroyImmediate(vehicleObject);
                Object.DestroyImmediate(zoneObject);
            }
        }

        private static void CheckUpgradeRules(List<string> failures)
        {
            UpgradeRules rules = UpgradeRules.CreateDefault();

            Check(failures, rules.MaxLevel == 3, "upgrades should top out at three levels");
            Check(failures, rules.CostForNextLevel(0) == 1500, "the first upgrade level should cost 1500");
            Check(failures, rules.CostForNextLevel(1) == 2500, "the second upgrade level should cost more than the first");
            Check(failures, rules.CostForNextLevel(2) == 3500, "the third upgrade level should cost more than the second");
            Check(failures, rules.CostForNextLevel(3) == 0, "a maxed part should have no next-level cost");

            Check(failures, Mathf.Approximately(rules.MultiplierFor(UpgradeKind.Engine, 0), 1f), "an unupgraded part should not change the car");
            Check(failures, Mathf.Approximately(rules.MultiplierFor(UpgradeKind.Engine, 3), 1.15f), "a fully upgraded engine should give 15 percent, not more");
            Check(failures, rules.MultiplierFor(UpgradeKind.Engine, 99) <= rules.MultiplierFor(UpgradeKind.Engine, rules.MaxLevel), "levels beyond the maximum must clamp");
            Check(failures, rules.MultiplierFor(UpgradeKind.Handling, 3) < rules.MultiplierFor(UpgradeKind.Engine, 3), "handling gains should stay smaller than engine gains");

            var entry = new CarSaveEntry(0) { engineLevel = 2, brakeLevel = 1, handlingLevel = 3 };

            Check(failures, VehicleUpgradeService.GetLevel(entry, UpgradeKind.Engine) == 2, "engine level should be read back correctly");
            Check(failures, VehicleUpgradeService.GetLevel(entry, UpgradeKind.Brakes) == 1, "brake level should be read back correctly");
            Check(failures, VehicleUpgradeService.GetLevel(entry, UpgradeKind.Handling) == 3, "handling level should be read back correctly");
            Check(failures, VehicleUpgradeService.GetLevel(null, UpgradeKind.Engine) == 0, "a car with no save entry should read as unupgraded");
        }

        private static void CheckQualityTiers(List<string> failures)
        {
            QualityProfile profile = QualityProfile.CreateDefault();

            GraphicsTierSettings low = profile.For(GraphicsTier.Low);
            GraphicsTierSettings medium = profile.For(GraphicsTier.Medium);
            GraphicsTierSettings high = profile.For(GraphicsTier.High);

            Check(failures, !low.shadowsEnabled, "low quality should switch shadows off");
            Check(failures, !low.realtimeRearMirror && !low.realtimeSideMirrors, "low quality should use no realtime mirrors");
            Check(failures, medium.realtimeRearMirror && !medium.realtimeSideMirrors, "medium quality should allow the rear mirror only");
            Check(failures, high.realtimeRearMirror && high.realtimeSideMirrors, "high quality may use side mirrors as well");
            Check(failures, low.shadowDistance < high.shadowDistance, "shadow distance should grow with the tier");
            Check(failures, low.trafficVehicleCount <= medium.trafficVehicleCount, "traffic density should not fall as quality rises");
            Check(failures, medium.trafficVehicleCount <= high.trafficVehicleCount, "traffic density should not fall as quality rises");
            Check(failures, low.pedestrianCount == 0, "low quality should spawn no pedestrians");
            Check(failures, high.trafficVehicleCount <= 15, "traffic should stay inside the mobile budget");
        }

        private static void CheckGarageSaveFields(List<string> failures)
        {
            SaveData data = SaveData.CreateDefault();

            CarSaveEntry car = data.GetOrCreateCar(1);
            car.engineLevel = 99;
            car.brakeLevel = -5;

            ChallengeSaveEntry challenge = data.GetOrCreateChallenge("supermarket_reverse");
            challenge.bestStars = 99;
            challenge.bestScore = -20;

            data.GetOrCreateChallenge("supermarket_reverse");
            data.Sanitize();

            Check(failures, data.FindCar(1).engineLevel == SaveData.MaxUpgradeLevel, "an out-of-range upgrade level should clamp to the maximum");
            Check(failures, data.FindCar(1).brakeLevel == 0, "a negative upgrade level should clamp to zero");
            Check(failures, data.FindChallenge("supermarket_reverse").bestStars == SaveData.MaxStars, "challenge stars should clamp to three");
            Check(failures, data.FindChallenge("supermarket_reverse").bestScore == 0, "a negative challenge score should clamp to zero");
            Check(failures, data.challenges.Count == 1, "asking for the same challenge twice should not create two records");
        }

        private static void Check(List<string> failures, bool condition, string description)
        {
            if (!condition)
            {
                failures.Add(description);
            }
        }
    }
}
