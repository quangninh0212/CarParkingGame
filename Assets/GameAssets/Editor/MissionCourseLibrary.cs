using System;
using System.Collections.Generic;
using CarParkingGame.Parking;
using UnityEngine;
using Writer = CarParkingGame.EditorTools.MissionCourseBuilder.CourseWriter;
using Part = CarParkingGame.EditorTools.MissionCourseKit.Part;
using Car = CarParkingGame.EditorTools.MissionCourseKit.CarModel;

namespace CarParkingGame.EditorTools
{
    // The designs for missions 9 upwards, one method each.
    //
    // Every one of them is a car park: one walled lot, taken in whole from the driving
    // camera, with the car starting inside it. What changes level to level is the shape of
    // the lot, which bays are free, what is in the way, and how many bays have to be
    // filled before the level is done.
    //
    // They used to be journeys - a lane with corners, a ramp to another deck, a route
    // across a site. Those read well from above and badly from the driver's seat: the
    // player could not see where they were being sent, could drive off the end of the
    // world, and one wall in the wrong place could shut the only way through. A lot has
    // none of those failure modes.
    //
    // A layout is written in the lot's own space: +Z is the way the car faces at the
    // start, the lot runs from z = 0 to z = length and from x = -width/2 to +width/2.
    //
    // There are six shapes of lot, below, and the levels work through them in turn so no
    // two in a row look the same.
    public static class MissionCourseLibrary
    {
        public class Course
        {
            public int missionId;
            public string name;
            public string description;
            public ParkingType parkingType;
            public int difficulty;
            public float width;
            public float length;

            // False for a course that builds more than one deck; it lays its own floors
            // and rails. Every lot is one deck.
            public bool singleDeck = true;

            // A closed car park rather than somewhere to drive through, so the wall round
            // it is sealed and striped instead of being left open at an entrance.
            public bool arena;

            public Action<Writer> layout;
        }

        // ----- bay plans -------------------------------------------------------------------
        //
        // A row, column or ring of bays is written as a picture of it, one character per
        // bay:
        //
        //   .  an empty bay
        //   #  a bay with a car already in it
        //   T  a space to fill, nose the way the painted arrow points
        //   t  a space to fill, either way round - no arrow, and no heading check
        //      a space in the string leaves a gap with no bay painted at all
        //
        // Bays are filled in the order they are written, and that order is what the P 1/3
        // counter on the HUD counts.

        // ===== shape 1: the four-row lot ====================================================
        //
        // Two full-width rows against the end walls and an island of two short rows in the
        // middle, with an aisle either side of the island and a lane round each end of it.
        //
        //     z = 46  +-------------------------------------+
        //             |   # # # # # # # # #   far row       |
        //             |                                     |
        //        lane |          # # # # #    island        | lane
        //             |          # # # # #                  |
        //             |                                     |
        //             |   # # # # # # # # #   near row      |
        //     z = 0   +-------------------------------------+

        private const float LotWidth = 34f;
        private const float LotLength = 46f;

        private const float Row1 = 4f;
        private const float Row2 = 19.2f;
        private const float Row3 = 25.6f;
        private const float Row4 = 40.8f;

        private const float NearAisle = 11.6f;
        private const float FarAisle = 33.2f;

        private const float WideLeft = -13.8f;
        private const float IslandLeft = -6.9f;
        private const float SideLane = -12.8f;

        private static void Near(Writer c, string plan) => c.BayRow(WideLeft, Row1, 180f, plan);
        private static void IslandNear(Writer c, string plan) => c.BayRow(IslandLeft, Row2, 0f, plan);
        private static void IslandFar(Writer c, string plan) => c.BayRow(IslandLeft, Row3, 180f, plan);
        private static void Far(Writer c, string plan) => c.BayRow(WideLeft, Row4, 0f, plan);

        private static void LotStart(Writer c, float x = 0f)
        {
            // A line down the middle of every lane the course records as drivable. The
            // floor was a black field with white boxes on it; these are what make it read
            // as somewhere cars drive. Written beside the routes so the two cannot drift.
            c.LaneLine(-15f, NearAisle, 15f, NearAisle);
            c.LaneLine(-15f, FarAisle, 15f, FarAisle);
            c.LaneLine(SideLane, NearAisle + 5f, SideLane, FarAisle - 5f);
            c.LaneLine(-SideLane, NearAisle + 5f, -SideLane, FarAisle - 5f);

            c.Start(x, NearAisle);

            c.Route(new Vector2(-15f, NearAisle), new Vector2(15f, NearAisle));
            c.Route(new Vector2(SideLane, NearAisle), new Vector2(SideLane, FarAisle));
            c.Route(new Vector2(-15f, FarAisle), new Vector2(15f, FarAisle));
        }

        // ===== shape 2: the walled yard =====================================================
        //
        // Bays round all four walls facing inward, and nothing at all in the middle. Every
        // bay is reached off the same open square, so the problem is the angle rather than
        // the route.

        private const float YardWidth = 36f;
        private const float YardLength = 38f;

        private const float YardNearZ = 3.6f;
        private const float YardFarZ = 34.4f;
        private const float YardLeftX = -14.6f;
        private const float YardRightX = 14.6f;
        private const float YardColumnZ = 10.5f;

        private static void YardNear(Writer c, string plan) => c.BayRow(WideLeft, YardNearZ, 180f, plan);
        private static void YardFar(Writer c, string plan) => c.BayRow(WideLeft, YardFarZ, 0f, plan);
        private static void YardLeft(Writer c, string plan) => c.BayColumn(YardLeftX, YardColumnZ, 270f, plan);
        private static void YardRight(Writer c, string plan) => c.BayColumn(YardRightX, YardColumnZ, 90f, plan);

        private static void YardStart(Writer c, float x = 0f, float z = 19f)
        {
            Ring(c, 9f, 10f, 28f);
            c.Start(x, z);

            // Once round the open middle: if any of it is shut, a car cannot get round.
            c.Route(
                new Vector2(-9f, 10f), new Vector2(9f, 10f),
                new Vector2(9f, 28f), new Vector2(-9f, 28f),
                new Vector2(-9f, 10f));
        }

        // ===== shape 3: the fountain square =================================================
        //
        // A yard with a fountain in the middle of it, so the open square becomes a loop and
        // the far bays can only be reached round one side or the other.

        private const float SquareWidth = 34f;
        private const float SquareLength = 42f;

        private const float SquareNearZ = 3.6f;
        private const float SquareFarZ = 38.4f;
        private const float SquareLeftX = -13.6f;
        private const float SquareRightX = 13.6f;
        private const float SquareColumnZ = 12f;

        private static void SquareNear(Writer c, string plan) => c.BayRow(WideLeft, SquareNearZ, 180f, plan);
        private static void SquareFar(Writer c, string plan) => c.BayRow(WideLeft, SquareFarZ, 0f, plan);
        private static void SquareLeft(Writer c, string plan) => c.BayColumn(SquareLeftX, SquareColumnZ, 270f, plan);
        private static void SquareRight(Writer c, string plan) => c.BayColumn(SquareRightX, SquareColumnZ, 90f, plan);

        private static void SquareStart(Writer c, float x = 0f)
        {
            Ring(c, 9f, 12f, 30f);
            c.Start(x, 10f);

            c.Route(
                new Vector2(-9f, 12f), new Vector2(9f, 12f),
                new Vector2(9f, 30f), new Vector2(-9f, 30f),
                new Vector2(-9f, 12f));
        }

        // ===== shape 4: the circus ==========================================================
        //
        // Eight bays set round a ring, every one of them square on to a fountain in the
        // middle. Nothing in a circus is parallel to anything else, which is the whole
        // point of it: the car has to be lined up with the bay rather than with the lot.
        //
        // No route is recorded. There is nothing in a circus that can shut a way through -
        // the bays stand clear of each other with seven metres of tarmac between them, and
        // the four corners of the lot are empty.

        private const float CircusWidth = 36f;
        private const float CircusLength = 44f;

        private const float CircusCentreZ = 24f;
        private const float CircusRadius = 13f;

        private static void Circus(Writer c, string plan, bool noseIn = true)
        {
            c.Fountain(0f, CircusCentreZ, 3.5f);
            c.BayRing(0f, CircusCentreZ, CircusRadius, plan, noseIn);
        }

        private static void CircusStart(Writer c, float x = 0f)
        {
            // The circus is driven round rather than along, so it gets a line round the
            // outside of the ring instead of down a lane it does not have.
            Ring(c, 16.5f, 6f, CircusCentreZ * 2f - 6f);
            c.Start(x, 4f);
        }

        // A broken line round the four sides of an open square, set in from the bays.
        private static void Ring(Writer c, float halfWidth, float near, float far)
        {
            c.LaneLine(-halfWidth, near, halfWidth, near);
            c.LaneLine(-halfWidth, far, halfWidth, far);
            c.LaneLine(-halfWidth, near, -halfWidth, far);
            c.LaneLine(halfWidth, near, halfWidth, far);
        }

        // ===== shape 5: the crossroads ======================================================
        //
        // Four bays, one at each point of the compass, all facing the middle. Small, quick,
        // and every bay wants the car at a different angle.

        private const float CrossWidth = 28f;
        private const float CrossLength = 30f;

        private const float CrossCentreZ = 15f;

        private static void Cross(Writer c, char north, char east, char south, char west)
        {
            c.BayRow(0f, CrossCentreZ + 7f, 180f, north.ToString());
            c.BayRow(7f, CrossCentreZ, 270f, east.ToString());
            c.BayRow(0f, CrossCentreZ - 7f, 0f, south.ToString());
            c.BayRow(-7f, CrossCentreZ, 90f, west.ToString());
        }

        private static void CrossStart(Writer c, float x = -10f)
        {
            Ring(c, 11.5f, 4f, 26f);
            c.Start(x, 4f);

            c.Route(
                new Vector2(-11.5f, 4f), new Vector2(11.5f, 4f),
                new Vector2(11.5f, 26f), new Vector2(-11.5f, 26f),
                new Vector2(-11.5f, 4f));
        }

        // ===== shape 6: the alley ===========================================================
        //
        // Long, narrow, and lined down both walls. One aisle, no way round, and every bay
        // entered off the same straight.

        private const float AlleyWidth = 26f;
        private const float AlleyLength = 48f;

        private const float AlleyLeftX = -9.8f;
        private const float AlleyRightX = 9.8f;
        private const float AlleyFirstZ = 6f;

        private static void AlleyLeft(Writer c, string plan) => c.BayColumn(AlleyLeftX, AlleyFirstZ, 270f, plan);
        private static void AlleyRight(Writer c, string plan) => c.BayColumn(AlleyRightX, AlleyFirstZ, 90f, plan);

        private static void AlleyStart(Writer c)
        {
            c.LaneLine(0f, 5f, 0f, 44f);
            c.Start(0f, 6f);
            c.Route(new Vector2(0f, 5f), new Vector2(0f, 44f));
        }

        // ----- the catalogue ----------------------------------------------------------------

        public static IReadOnlyList<Course> All => Courses;

        private static Course Make(int id, string name, string description, int difficulty,
            float width, float length, Action<Writer> layout)
        {
            return new Course
            {
                missionId = id,
                name = name,
                description = description,

                // Forward for all of them. Which way round a bay wants the car is said by
                // the arrow painted in it, not by the mission type, which would demand the
                // same of every bay in the level.
                parkingType = ParkingType.Forward,
                difficulty = difficulty,
                width = width,
                length = length,
                arena = true,
                layout = layout
            };
        }

        private static Course Lot(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, LotWidth, LotLength, l);

        private static Course Yard(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, YardWidth, YardLength, l);

        private static Course Square(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, SquareWidth, SquareLength, l);

        private static Course Ring(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, CircusWidth, CircusLength, l);

        private static Course Crossroads(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, CrossWidth, CrossLength, l);

        private static Course Alley(int id, string name, string text, int d, Action<Writer> l)
            => Make(id, name, text, d, AlleyWidth, AlleyLength, l);

        private static readonly List<Course> Courses = new List<Course>
        {
            Lot(9,         "Open Lot",        "An empty lot and one space. Either way round will do.",          1, OpenLot),
            Lot(10,        "First Row",       "One space in the near row, with an arrow saying which way in.",  1, FirstRow),
            Alley(11,      "The Alley",       "One space off a straight run between two walls.",                1, TheAlley),
            Lot(12,        "Side By Side",    "A space with a car tight against each side of it.",              2, SideBySide),
            Yard(13,       "The Yard",        "Bays round all four walls, and one of them is yours.",           2, TheYard),
            Lot(14,        "The Back Row",    "Past the island and all the way to the far wall.",               2, BackRow),
            Crossroads(15, "Crossroads",      "Two spaces, and they face opposite ways.",                       2, CrossroadsTwo),
            Lot(16,        "Two Stops",       "Two spaces, one at each end of the lot.",                        2, TwoStops),
            Square(17,     "The Fountain",    "A square with water in the middle and one space behind it.",     2, TheFountain),
            Lot(18,        "The Island",      "Both spaces are on the island in the middle.",                   3, TheIsland),
            Alley(19,      "Both Walls",      "Two spaces down the alley, one on each side.",                   3, BothWalls),
            Lot(20,        "Cones Out",       "The near aisle is coned down to half its width.",                3, ConesOut),
            Yard(21,       "Corner Work",     "Two spaces, both of them in a corner of the yard.",              3, CornerWork),
            Lot(22,        "Road Works",      "Barricades across the lot and two spaces behind them.",          3, RoadWorks),
            Ring(23,       "The Circus",      "Eight bays round a fountain. Not one of them is square on.",     3, TheCircus),
            Lot(24,        "Tight Fit",       "The lot is full but for one space, and it is not a wide one.",   3, TightFit),
            Square(25,     "Garden Square",   "Planting round the fountain, and two spaces past it.",           3, GardenSquare),
            Lot(26,        "Both Ends",       "Opposite corners, with the island to get round.",                3, BothEnds),
            Alley(27,      "Long Alley",      "Two spaces at the far end of a full alley.",                     4, LongAlley),
            Lot(28,        "Three Stops",     "Three spaces, and no two of them near each other.",              4, ThreeStops),
            Yard(29,       "Round The Walls", "Three spaces, one on each of three walls.",                      4, RoundTheWalls),
            Lot(30,        "Nose Out",        "Two spaces, both arrows pointing back out. Reverse in.",         4, NoseOut),
            Crossroads(31, "Four Ways",       "All four points of the compass, one after another.",             4, FourWays),
            Lot(32,        "Any Way Round",   "Three spaces, not one of them marked with an arrow.",            3, AnyWayRound),
            Ring(33,       "Fountain Ring",   "Three bays off the circus, and none of them adjacent.",          4, FountainRing),
            Lot(34,        "Read The Paint",  "Three spaces. Two say which way round, one does not.",           4, ReadThePaint),
            Square(35,     "The Promenade",   "A busy square, two spaces, and a fountain in the way.",          4, ThePromenade),
            Lot(36,        "Full House",      "Three spaces in a lot with almost nothing left in it.",          4, FullHouse),
            Alley(37,      "Tight Alley",     "Three spaces, and parked cars the whole length of both walls.",  4, TightAlley),
            Yard(38,       "Walled In",       "Three spaces in a yard with barriers across the middle.",        4, WalledIn),
            Lot(39,        "The Gauntlet",    "Cones, barrels and barricades, and three spaces behind them.",   5, Gauntlet),
            Ring(40,       "Carousel",        "Three bays round the circus, all of them facing outward.",       5, Carousel),
            Lot(41,        "Four Corners",    "One space in each corner of the lot.",                           4, FourCorners),
            Square(42,     "Park Life",       "Three spaces round a square full of planting and people.",       4, ParkLife),
            Crossroads(43, "Compass",         "Four spaces, four headings, and barrels between them.",          5, Compass),
            Lot(44,        "Rush Hour",       "Four spaces in a lot that is otherwise full.",                   5, RushHour),
            Alley(45,      "The Run",         "Four spaces down one long alley, in order.",                     5, TheRun),
            Yard(46,       "Clearing Up",     "Four spaces round a yard with the middle half blocked.",         5, ClearingUp),
            Lot(47,        "Obstacle Course", "Four spaces, with something in the way of every one of them.",   5, ObstacleCourse),
            Ring(48,       "Last Ring",       "Four bays off the circus, mixed arrows, nothing square on.",     5, LastRing),
            Square(49,     "Night Shift",     "Four spaces round a full square. Mind the fountain.",            5, NightShift),
            Lot(50,        "Final Lot",       "Four spaces, mixed arrows, and nowhere to be careless.",         5, FinalLot)
        };

        // ----- the four-row lots --------------------------------------------------------

        private static void OpenLot(Writer c)
        {
            LotStart(c);

            // No arrow in the bay, so the heading is not checked either. The first lot is
            // about getting the car between two painted lines and nothing else.
            Near(c, "..#..t..#");
            Far(c, "..#.....#");
        }

        private static void FirstRow(Writer c)
        {
            LotStart(c);

            Near(c, "#.#.T.#.#");
            Far(c, "..#...#..");
        }

        private static void SideBySide(Writer c)
        {
            LotStart(c);

            Near(c, "#..##T##.");
            IslandNear(c, "#...#");
            Far(c, "..#...#..");
        }

        private static void BackRow(Writer c)
        {
            LotStart(c);

            Near(c, "#.#.#.#.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, ".#.#.");
            Far(c, "..##T##..");
        }

        private static void TwoStops(Writer c)
        {
            LotStart(c);

            Near(c, "T.#.#.#.#");
            IslandNear(c, "#...#");
            Far(c, "#.#.#.#.T");
        }

        private static void TheIsland(Writer c)
        {
            LotStart(c);

            Near(c, "#.#.#.#.#");
            IslandNear(c, "#.T.#");
            IslandFar(c, "#.T.#");
            Far(c, "..#...#..");
        }

        private static void ConesOut(Writer c)
        {
            LotStart(c);

            Near(c, "#.##T##.#");
            IslandNear(c, "#.#.#");
            Far(c, "..#...#..");

            // Off the middle of the aisle rather than across it, so the aisle narrows
            // instead of closing.
            c.Cones(-9f, NearAisle + 3.4f, 13f, NearAisle + 3.4f, 7);
            c.Prop(Part.Barrel, -9f, NearAisle - 3.2f, 0f, 1.1f);
            c.Prop(Part.Barrel, 9f, NearAisle - 3.2f, 0f, 1.1f);
        }

        private static void RoadWorks(Writer c)
        {
            LotStart(c, -11f);

            Near(c, "#.#.#.T.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#...#");
            Far(c, "#.#.T.#.#");

            c.Prop(Part.WaterBarricade, -3f, NearAisle + 3.4f, 0f, 1.1f);
            c.Prop(Part.WaterBarricade, 0.5f, NearAisle + 3.4f, 0f, 1.1f);
            c.Prop(Part.WaterBarricade, 4f, NearAisle + 3.4f, 0f, 1.1f);
            c.Prop(Part.VerticalPanel, 8f, FarAisle - 3.4f, 0f, 1.2f);
            c.Prop(Part.Channelizing, -8f, FarAisle + 3.4f, 0f, 1.1f);
        }

        private static void TightFit(Writer c)
        {
            LotStart(c);

            Near(c, "#####T###");
            IslandNear(c, "#####");
            IslandFar(c, "#####");
            Far(c, "#########");
        }

        private static void BothEnds(Writer c)
        {
            LotStart(c, 8f);

            Near(c, "T.#.#.#.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.#.#");
            Far(c, "#.#.#.#.T");
        }

        private static void ThreeStops(Writer c)
        {
            LotStart(c);

            Near(c, "#.T.#.#.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.T.#");
            Far(c, "#.#.#.#.T");
        }

        private static void NoseOut(Writer c)
        {
            LotStart(c);

            // The base row leaves its slot empty - a space, not an empty bay - so the
            // turned bay is the only thing painted there and no car is parked in it.
            Near(c, "#.#. .#.#");
            c.BayRow(WideLeft, Row1, 0f, "    T    ");

            IslandNear(c, "#.#.#");
            IslandFar(c, "#.#.#");

            Far(c, "#. .#.#.#");
            c.BayRow(WideLeft, Row4, 180f, "  T      ");
        }

        private static void AnyWayRound(Writer c)
        {
            LotStart(c);

            // Three bays, none of them arrowed, so none of them care which way the car
            // finishes facing.
            Near(c, "#.t.#.#.#");
            IslandFar(c, "#.t.#");
            Far(c, "#.#.#.t.#");
        }

        private static void ReadThePaint(Writer c)
        {
            LotStart(c, 10f);

            Near(c, "#.T.#.t.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.#.#");
            Far(c, "#.#.T.#.#");
        }

        private static void FullHouse(Writer c)
        {
            LotStart(c);

            Near(c, "###T#####");
            IslandNear(c, "##T##");
            IslandFar(c, "#####");
            Far(c, "####T####");
        }

        private static void Gauntlet(Writer c)
        {
            LotStart(c, -12f);

            Near(c, "#.#.T.#.#");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.T.#");
            Far(c, "#.#.#.T.#");

            c.Cones(-6f, NearAisle + 3.5f, 10f, NearAisle + 3.5f, 6);
            c.Prop(Part.Barrel, -2f, NearAisle - 3.3f, 0f, 1.1f);
            c.Prop(Part.Barrel, 2f, NearAisle - 3.3f, 0f, 1.1f);
            c.Prop(Part.WaterBarricade, 6f, FarAisle + 3.5f, 0f, 1.1f);
            c.Prop(Part.WaterBarricade, 9.5f, FarAisle + 3.5f, 0f, 1.1f);
            c.Prop(Part.PedestrianBarrier, -9f, FarAisle - 3.5f, 0f, 1.1f);
        }

        private static void FourCorners(Writer c)
        {
            LotStart(c);

            Near(c, "T.#.#.#.T");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.#.#");
            Far(c, "T.#.#.#.T");
        }

        private static void RushHour(Writer c)
        {
            LotStart(c, 11f);

            Near(c, "##T###T##");
            IslandNear(c, "#####");
            IslandFar(c, "##T##");
            Far(c, "###T#####");
        }

        private static void ObstacleCourse(Writer c)
        {
            LotStart(c, -11f);

            Near(c, "#.T.#.#.T");
            IslandNear(c, "#.#.#");
            IslandFar(c, "#.T.#");
            Far(c, "#.#.T.#.#");

            c.Cones(-13f, NearAisle - 3.4f, -2f, NearAisle - 3.4f, 5);
            c.Prop(Part.Barrel, 5f, NearAisle + 3.4f, 0f, 1.1f);
            c.Prop(Part.Barrel, 8.5f, NearAisle + 3.4f, 0f, 1.1f);
            c.Block(-11f, 22f, 0f, 1.6f, 1.2f, 6f, Part.ConcreteYellow);
            c.Hedge(12.5f, 22f, 0f, 10f);
            c.Prop(Part.Channelizing, 0f, FarAisle - 3.4f, 0f, 1.1f);
            c.Prop(Part.Channelizing, 3.5f, FarAisle - 3.4f, 0f, 1.1f);
            c.Prop(Part.PedestrianBarrier, -7f, FarAisle + 3.5f, 0f, 1.1f);
        }

        private static void FinalLot(Writer c)
        {
            LotStart(c);

            Near(c, "##T##t###");
            IslandNear(c, "#####");
            IslandFar(c, "##T##");
            Far(c, "####T####");

            c.Prop(Part.Barrel, -9.5f, NearAisle + 3.4f, 0f, 1.1f);
            c.Prop(Part.Barrel, 9.5f, NearAisle + 3.4f, 0f, 1.1f);
            c.Cones(-4f, FarAisle - 3.4f, 7f, FarAisle - 3.4f, 5);

            // One car abandoned across the lane down the right of the island, because a
            // full lot always has one. It leaves the lane down the left - the one the
            // course records as a way through - clear.
            c.Car(12.5f, 24f, 12f, Car.Muscle);
        }

        // ----- the walled yards ----------------------------------------------------------

        private static void TheYard(Writer c)
        {
            YardStart(c);

            YardNear(c, "#.#.#.#.#");
            YardFar(c, "..#.T.#..");
            YardLeft(c, "#...#");
            YardRight(c, "#...#");
        }

        private static void CornerWork(Writer c)
        {
            YardStart(c, -4f);

            YardNear(c, "T.#.#.#.#");
            YardFar(c, "#.#.#.#.T");
            YardLeft(c, "#.#.#");
            YardRight(c, "#.#.#");
        }

        private static void RoundTheWalls(Writer c)
        {
            YardStart(c);

            // One on each of three walls, so the car is turned through a right angle
            // between every pair of them.
            YardNear(c, "#.#.T.#.#");
            YardFar(c, "#.#.#.#.#");
            YardLeft(c, "#.T.#");
            YardRight(c, "#.T.#");
        }

        private static void WalledIn(Writer c)
        {
            YardStart(c, -6f, 13f);

            YardNear(c, "#.#.#.T.#");
            YardFar(c, "#.T.#.#.#");
            YardLeft(c, "#.#.#");
            YardRight(c, "#.T.#");

            // Across the middle of the yard, well clear of the loop round the outside of
            // it, so the square becomes a circuit rather than an open floor.
            c.WhiteBarriers(-5f, 19f, 5f, 19f, 4);
            c.Prop(Part.Barrel, 0f, 15f, 0f, 1.1f);
            c.Prop(Part.Barrel, 0f, 23f, 0f, 1.1f);
        }

        private static void ClearingUp(Writer c)
        {
            YardStart(c, -6f, 13f);

            YardNear(c, "#.T.#.#.#");
            YardFar(c, "#.#.#.T.#");
            YardLeft(c, "#.T.#");
            YardRight(c, "#.T.#");

            c.WhiteBarriers(-5f, 17f, 5f, 17f, 4);
            c.WhiteBarriers(-5f, 22f, 5f, 22f, 4);
            c.Cones(-6.5f, 19.5f, 6.5f, 19.5f, 5);
            c.Prop(Part.Barrel, 7f, 13f, 0f, 1.1f);
            c.Prop(Part.Barrel, -7f, 25f, 0f, 1.1f);
        }

        // ----- the fountain squares ------------------------------------------------------

        private static void TheFountain(Writer c)
        {
            SquareStart(c);

            c.Fountain(0f, 21f, 4f);

            SquareNear(c, "#.#.#.#.#");
            SquareFar(c, "..#.T.#..");
            SquareLeft(c, "#..#");
            SquareRight(c, "#..#");
        }

        private static void GardenSquare(Writer c)
        {
            SquareStart(c, -6f);

            c.Fountain(0f, 21f, 4f);

            SquareNear(c, "#.#.#.#.#");
            SquareFar(c, "#.T.#.#.#");
            SquareLeft(c, "#.T#");
            SquareRight(c, "#..#");

            c.Hedge(-6.5f, 21f, 0f, 7f);
            c.Hedge(6.5f, 21f, 0f, 7f);
        }

        private static void ThePromenade(Writer c)
        {
            SquareStart(c, 6f);

            c.Fountain(0f, 21f, 4.5f);

            SquareNear(c, "#.#.#.T.#");
            SquareFar(c, "#.#.T.#.#");
            SquareLeft(c, "####");
            SquareRight(c, "####");

            c.Hedge(0f, 15f, 90f, 9f);
            c.Prop(Part.Barrel, -6.5f, 27f, 0f, 1.1f);
            c.Prop(Part.Barrel, 6.5f, 16f, 0f, 1.1f);
        }

        private static void ParkLife(Writer c)
        {
            SquareStart(c, -7f);

            c.Fountain(0f, 21f, 4f);

            SquareNear(c, "#.T.#.#.#");
            SquareFar(c, "#.#.#.T.#");
            SquareLeft(c, "#.T#");
            SquareRight(c, "####");

            c.Hedge(-6.5f, 21f, 0f, 8f);
            c.Hedge(6.5f, 21f, 0f, 8f);
            c.Hedge(0f, 14f, 90f, 8f);
            c.Cones(-5f, 28.5f, 5f, 28.5f, 4);
        }

        private static void NightShift(Writer c)
        {
            SquareStart(c, 7f);

            c.Fountain(0f, 21f, 4f);

            SquareNear(c, "#.T.#.T.#");
            SquareFar(c, "#.#.T.#.#");
            SquareLeft(c, "#T##");
            SquareRight(c, "####");

            c.Hedge(-6.5f, 21f, 0f, 8f);
            c.Hedge(6.5f, 21f, 0f, 8f);
            c.Prop(Part.Barrel, -12f, 8.5f, 0f, 1.1f);
            c.Prop(Part.Barrel, 12f, 8.5f, 0f, 1.1f);
            c.Cones(-4f, 28f, 4f, 28f, 4);
        }

        // ----- the circuses ---------------------------------------------------------------
        //
        // Eight bays, written clockwise from the one furthest from the start.

        private static void TheCircus(Writer c)
        {
            CircusStart(c);
            Circus(c, "#T##.T##");
        }

        private static void FountainRing(Writer c)
        {
            CircusStart(c, -6f);
            Circus(c, "T#T##T##");
        }

        private static void Carousel(Writer c)
        {
            CircusStart(c, 6f);

            // Facing outward: every arrow points away from the fountain, so each one is
            // reversed into off the ring.
            Circus(c, "#T#T#T##", false);
        }

        private static void LastRing(Writer c)
        {
            CircusStart(c);
            Circus(c, "T#t#T#T#");

            c.Prop(Part.Barrel, -14f, 8f, 0f, 1.1f);
            c.Prop(Part.Barrel, 14f, 8f, 0f, 1.1f);
            c.Cones(-15f, 40f, 15f, 40f, 6);
        }

        // ----- the crossroads --------------------------------------------------------------

        private static void CrossroadsTwo(Writer c)
        {
            CrossStart(c);
            Cross(c, 'T', '#', 'T', '#');

            c.Prop(Part.Barrel, -8f, 24f, 0f, 1.1f);
            c.Prop(Part.Barrel, 8f, 6f, 0f, 1.1f);
        }

        private static void FourWays(Writer c)
        {
            CrossStart(c);
            Cross(c, 'T', 'T', 'T', 'T');
        }

        private static void Compass(Writer c)
        {
            CrossStart(c, 10f);
            Cross(c, 'T', 'T', 'T', 'T');

            // In the diagonals, where nothing has to be driven.
            c.Prop(Part.Barrel, -8f, 24f, 0f, 1.1f);
            c.Prop(Part.Barrel, 8f, 24f, 0f, 1.1f);
            c.Prop(Part.Barrel, -8f, 6f, 0f, 1.1f);
            c.Cones(6f, 7f, 9f, 10f, 3);
        }

        // ----- the alleys -------------------------------------------------------------------

        private static void TheAlley(Writer c)
        {
            AlleyStart(c);

            AlleyLeft(c, "#..T..#...");
            AlleyRight(c, "#.....#...");
        }

        private static void BothWalls(Writer c)
        {
            AlleyStart(c);

            AlleyLeft(c, "#.#.T.#.#.");
            AlleyRight(c, "#.#.#.T.#.");
        }

        private static void LongAlley(Writer c)
        {
            AlleyStart(c);

            AlleyLeft(c, "#.#.#.#.T.");
            AlleyRight(c, "#.#.#.#.#T");
        }

        private static void TightAlley(Writer c)
        {
            AlleyStart(c);

            AlleyLeft(c, "###T######");
            AlleyRight(c, "#####T##T#");
        }

        private static void TheRun(Writer c)
        {
            AlleyStart(c);

            AlleyLeft(c, "#T##T#####");
            AlleyRight(c, "###T###T##");

            c.Cones(-5f, 16f, -5f, 24f, 4);
            c.Prop(Part.Barrel, 4f, 31f, 0f, 1.1f);
        }
    }
}
