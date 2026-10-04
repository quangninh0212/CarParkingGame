using System;
using System.Collections.Generic;
using CarParkingGame.Parking;
using UnityEngine;
using Writer = CarParkingGame.EditorTools.MissionCourseBuilder.CourseWriter;
using Part = CarParkingGame.EditorTools.MissionCourseKit.Part;
using Car = CarParkingGame.EditorTools.MissionCourseKit.CarModel;

namespace CarParkingGame.EditorTools
{
    // The designs for missions 9-30, one method each.
    //
    // Everything is written in the course's own space: the car starts at (0, 0) facing
    // +Z, the pad runs from z = 0 to z = length and from x = -width/2 to +width/2. That
    // makes a layout read as a drawing, and it means a course can be moved anywhere on the
    // map without a single coordinate changing.
    //
    // The project has no models for buses, fuel pumps, forklifts, shipping containers or
    // shopping trolley shelters, so those are built out of sized concrete blocks. The
    // driving problem each level poses - the gaps, the corners, the sight lines - is the
    // real thing; the dressing is a stand-in until there is art for it.
    //
    // There are no ceilings over the underground decks. The follow camera sits four metres
    // up and would spend the level inside the slab; pillars, walls and markings carry the
    // idea without blinding the player.
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

            // False for the courses that build more than one deck; they lay their own
            // floors and rails.
            public bool singleDeck = true;
            public Action<Writer> layout;
        }

        private const float LaneWide = 7f;
        private const float LaneNarrow = 5.2f;

        public static IReadOnlyList<Course> All => Courses;

        private static readonly List<Course> Courses = new List<Course>
        {
            new Course
            {
                missionId = 9,
                name = "Cone Slalom",
                description = "Weave the cones, then park straight at the end.",
                parkingType = ParkingType.Forward,
                difficulty = 2,
                width = 26f,
                length = 72f,
                layout = Slalom
            },
            new Course
            {
                missionId = 10,
                name = "The L Bend",
                description = "A walled training lane with one square corner, and a bay around it.",
                parkingType = ParkingType.Forward,
                difficulty = 2,
                width = 46f,
                length = 46f,
                layout = LBend
            },
            new Course
            {
                missionId = 11,
                name = "The S Bend",
                description = "A narrow S between kerbs and cones, with the bay at the finish.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 36f,
                length = 62f,
                layout = SBend
            },
            new Course
            {
                missionId = 12,
                name = "Between Walls",
                description = "A straight lane with a hand's width either side of the mirrors.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 18f,
                length = 58f,
                layout = BetweenWalls
            },
            new Course
            {
                missionId = 13,
                name = "Tight Corners",
                description = "A service road with two square corners and concrete on both sides.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 48f,
                length = 48f,
                layout = TightCorners
            },
            new Course
            {
                missionId = 14,
                name = "The Courtyard",
                description = "A walled yard with no room to turn and one slot worth having.",
                parkingType = ParkingType.Reverse,
                difficulty = 4,
                width = 32f,
                length = 34f,
                layout = Courtyard
            },
            new Course
            {
                missionId = 15,
                name = "Supermarket",
                description = "Rows of shoppers' cars, trolley shelters and one free bay.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 58f,
                length = 50f,
                layout = Supermarket
            },
            new Course
            {
                missionId = 16,
                name = "Full Car Park",
                description = "Narrow aisles, crossings everywhere, and the only space is at the back.",
                parkingType = ParkingType.Reverse,
                difficulty = 4,
                width = 52f,
                length = 52f,
                layout = FullCarPark
            },
            new Course
            {
                missionId = 17,
                name = "Petrol Station",
                description = "Round the pumps and in beside the shop.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 42f,
                length = 40f,
                layout = PetrolStation
            },
            new Course
            {
                missionId = 18,
                name = "On The Street",
                description = "Kerbside parallel parking between two cars, with traffic behind you.",
                parkingType = ParkingType.Parallel,
                difficulty = 4,
                width = 38f,
                length = 58f,
                layout = OnTheStreet
            },
            new Course
            {
                missionId = 19,
                name = "Bus Station",
                description = "Reverse into the gap between two coaches.",
                parkingType = ParkingType.Reverse,
                difficulty = 4,
                width = 48f,
                length = 52f,
                layout = BusStation
            },
            new Course
            {
                missionId = 20,
                name = "Loading Yard",
                description = "Back up to the warehouse dock through the stacked freight.",
                parkingType = ParkingType.Reverse,
                difficulty = 4,
                width = 50f,
                length = 48f,
                layout = LoadingYard
            },
            new Course
            {
                missionId = 21,
                name = "Container Maze",
                description = "Corridors of containers, square corners, bay at the far end.",
                parkingType = ParkingType.Forward,
                difficulty = 4,
                width = 46f,
                length = 50f,
                layout = ContainerMaze
            },
            new Course
            {
                missionId = 22,
                name = "Road Works",
                description = "Barricades, barrels and cones narrowing a working site.",
                parkingType = ParkingType.Forward,
                difficulty = 4,
                width = 42f,
                length = 50f,
                layout = RoadWorks
            },
            new Course
            {
                missionId = 23,
                name = "Car Park Deck",
                description = "Wide lanes between the pillars and a bay near the entrance.",
                parkingType = ParkingType.Forward,
                difficulty = 3,
                width = 42f,
                length = 40f,
                layout = CarParkDeck
            },
            new Course
            {
                missionId = 24,
                name = "Pillars And Posts",
                description = "The same deck with half the room and a slot on the back wall.",
                parkingType = ParkingType.Reverse,
                difficulty = 5,
                width = 38f,
                length = 42f,
                layout = PillarsAndPosts
            },
            new Course
            {
                missionId = 25,
                name = "Down The Ramp",
                singleDeck = false,
                description = "Follow the ramp to the lower deck and park there.",
                parkingType = ParkingType.Forward,
                difficulty = 4,
                width = 36f,
                length = 58f,
                layout = DownTheRamp
            },
            new Course
            {
                missionId = 26,
                name = "Upper Deck",
                singleDeck = false,
                description = "Up one level, then into a bay among the parked cars.",
                parkingType = ParkingType.Forward,
                difficulty = 4,
                width = 40f,
                length = 64f,
                layout = UpperDeck
            },
            new Course
            {
                missionId = 27,
                name = "Rooftop",
                singleDeck = false,
                description = "Climb to the roof deck and reverse in against the parapet.",
                parkingType = ParkingType.Reverse,
                difficulty = 5,
                width = 40f,
                length = 70f,
                layout = Rooftop
            },
            new Course
            {
                missionId = 28,
                name = "The Driving Test",
                description = "Slalom, S bend, square corner, and reverse in at the end.",
                parkingType = ParkingType.Reverse,
                difficulty = 5,
                width = 42f,
                length = 84f,
                layout = DrivingTest
            },
            new Course
            {
                missionId = 29,
                name = "The Maze",
                description = "Dead ends and shortcuts between you and the last free space.",
                parkingType = ParkingType.Forward,
                difficulty = 5,
                width = 54f,
                length = 54f,
                layout = Maze
            },
            new Course
            {
                missionId = 30,
                name = "Final Examination",
                singleDeck = false,
                description = "Ramp, pillars, parked cars, and a parallel space with nothing to spare.",
                parkingType = ParkingType.Parallel,
                difficulty = 5,
                width = 46f,
                length = 74f,
                layout = FinalExamination
            }
        };

        // ----- shared shapes -------------------------------------------------------------

        // Walls or cones down both sides of a path, which is most of what a training course
        // is. The path is a centre line; the edges are offset perpendicular to each leg.
        private static void Lane(Writer c, Vector2[] path, float halfWidth, bool walls, float wallHeight = 2.2f)
        {
            c.Route(path);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2[] edge = OffsetPath(path, side * halfWidth);

                for (int i = 0; i < edge.Length - 1; i++)
                {
                    Vector2 from = edge[i];
                    Vector2 to = edge[i + 1];

                    if (walls)
                    {
                        c.Wall(from.x, from.y, to.x, to.y, wallHeight);
                    }
                    else
                    {
                        int count = Mathf.Max(2, Mathf.RoundToInt((to - from).magnitude / 3f));
                        c.Cones(from.x, from.y, to.x, to.y, count);
                    }
                }
            }
        }

        // One side of a lane: the centre line walked at a fixed distance, meeting each
        // corner where the two offset legs cross rather than where the centre line turns.
        //
        // Offsetting each leg on its own is what put a wall across the L bend. The inside
        // edge of the leg after a square corner starts half a lane short of the corner, so
        // on its own it runs straight across the leg before it and seals the lane - the
        // wall "sticking out so the car cannot get past" in the report.
        private static Vector2[] OffsetPath(Vector2[] path, float distance)
        {
            var edge = new Vector2[path.Length];
            int last = path.Length - 1;

            edge[0] = path[0] + Perpendicular(path[1] - path[0]) * distance;
            edge[last] = path[last] + Perpendicular(path[last] - path[last - 1]) * distance;

            for (int i = 1; i < last; i++)
            {
                Vector2 incoming = (path[i] - path[i - 1]).normalized;
                Vector2 outgoing = (path[i + 1] - path[i]).normalized;

                Vector2 onIncoming = path[i] + Perpendicular(incoming) * distance;
                Vector2 onOutgoing = path[i] + Perpendicular(outgoing) * distance;

                // Parallel legs, or a reversal: there is no crossing point, and the offset
                // simply carries straight on.
                if (!TryCross(onIncoming, incoming, onOutgoing, outgoing, out edge[i]))
                {
                    edge[i] = onIncoming;
                }
            }

            return edge;
        }

        private static bool TryCross(Vector2 a, Vector2 alongA, Vector2 b, Vector2 alongB, out Vector2 point)
        {
            float denominator = alongA.x * alongB.y - alongA.y * alongB.x;

            if (Mathf.Abs(denominator) < 1e-4f)
            {
                point = a;
                return false;
            }

            Vector2 delta = b - a;
            float distance = (delta.x * alongB.y - delta.y * alongB.x) / denominator;

            point = a + alongA * distance;
            return true;
        }

        private static Vector2 Perpendicular(Vector2 direction)
        {
            Vector2 normalized = direction.normalized;
            return new Vector2(normalized.y, -normalized.x);
        }

        // A wall round the outside with a gap at the entrance, for the enclosed courses.
        private static void Perimeter(Writer c, float inset, float height, float gapHalfWidth, float gapCentreX = 0f)
        {
            float x = c.Width * 0.5f - inset;
            float front = inset;
            float back = c.Length - inset;

            // The gap goes where the course is entered from, which is not always the middle.
            float gapLeft = Mathf.Clamp(gapCentreX - gapHalfWidth, -x, x);
            float gapRight = Mathf.Clamp(gapCentreX + gapHalfWidth, -x, x);

            c.Wall(-x, front, gapLeft, front, height);
            c.Wall(gapRight, front, x, front, height);
            c.Wall(-x, back, x, back, height);
            c.Wall(-x, front, -x, back, height);
            c.Wall(x, front, x, back, height);
        }

        // A shipping container, a coach, a fuel pump: things the project has no model of,
        // standing in as a block of the right size.
        private static void Container(Writer c, float x, float z, float yaw)
        {
            c.Block(x, z, yaw, 2.5f, 2.7f, 6.3f, Part.ConcreteRed);
        }

        private static void Coach(Writer c, float x, float z, float yaw)
        {
            c.Block(x, z, yaw, 2.6f, 3.2f, 11f, Part.Concrete);
        }

        // ----- the courses ----------------------------------------------------------------

        private static void Slalom(Writer c)
        {
            c.Start(0f, 4f);

            c.Kerb(-LaneWide, 6f, -LaneWide, 54f);
            c.Kerb(LaneWide, 6f, LaneWide, 54f);

            c.Slalom(12f, 46f, 3.2f, 6);

            c.Arrow(0f, 8f, 0f);
            c.Cones(-3.4f, 54f, -3.4f, 60f, 3);
            c.Cones(3.4f, 54f, 3.4f, 60f, 3);

            c.Bay(0f, 64f, 0f, 3.4f, 6.5f);
        }

        private static void LBend(Writer c)
        {
            c.Start(0f, 4f);

            Lane(c, new[] { new Vector2(0f, 2f), new Vector2(0f, 30f), new Vector2(20f, 30f) }, LaneNarrow * 0.5f, true);

            c.Cones(-2.2f, 24f, -2.2f, 28f, 3);
            c.Arrow(0f, 10f, 0f);

            c.Bay(20f, 30f, 90f, 3.3f, 6.5f);
        }

        private static void SBend(Writer c)
        {
            c.Start(0f, 4f);

            var path = new[]
            {
                new Vector2(0f, 2f),
                new Vector2(0f, 14f),
                new Vector2(8f, 24f),
                new Vector2(8f, 34f),
                new Vector2(0f, 44f),
                new Vector2(0f, 52f)
            };

            Lane(c, path, 2.9f, false);

            c.Kerb(-6.5f, 4f, -6.5f, 52f);
            c.Kerb(14.5f, 14f, 14.5f, 44f);

            c.Bay(0f, 57f, 0f, 3.2f, 6.5f);
        }

        private static void BetweenWalls(Writer c)
        {
            c.Start(0f, 3f);

            c.Route(new Vector2(0f, 3f), new Vector2(0f, 44f), new Vector2(0f, 48f));

            // 3.9m between the walls for a 1.95m car: under a metre each side.
            c.Wall(-1.95f, 6f, -1.95f, 42f, 2.4f);
            c.Wall(1.95f, 6f, 1.95f, 42f, 2.4f);

            c.Cones(-3.2f, 4f, -3.2f, 5.5f, 2);
            c.Cones(3.2f, 4f, 3.2f, 5.5f, 2);
            c.Arrow(0f, 9f, 0f);

            // Opens into a small yard at the far end. The yard's near wall has to be two
            // walls with the lane's own width between them, or it seals off the only room
            // the course has to park in.
            c.Wall(-8f, 42f, -1.95f, 42f, 2.4f);
            c.Wall(1.95f, 42f, 8f, 42f, 2.4f);
            c.Wall(-8f, 42f, -8f, 54f, 2.4f);
            c.Wall(8f, 42f, 8f, 54f, 2.4f);
            c.Wall(-8f, 54f, 8f, 54f, 2.4f);

            c.Bay(0f, 48f, 0f, 3.3f, 6.5f);
        }

        private static void TightCorners(Writer c)
        {
            c.Start(0f, 4f);

            var path = new[]
            {
                new Vector2(0f, 2f),
                new Vector2(0f, 18f),
                new Vector2(18f, 18f),
                new Vector2(18f, 38f)
            };

            Lane(c, path, 2.95f, true);

            c.Barriers(-6f, 16f, -6f, 22f, 3);
            c.Prop(Part.Barrel, 6f, 14f, 0f, 1.1f);
            c.Arrow(0f, 9f, 0f);

            c.Bay(18f, 42f, 0f, 3.2f, 6.5f);
        }

        private static void Courtyard(Writer c)
        {
            c.Start(0f, 4f);

            Perimeter(c, 1.5f, 3f, 4f);

            // Residents' cars down the left and across the back, leaving one slot.
            c.CarRow(-10f, 10f, 90f, 3, 3.2f, true);
            c.Car(-10f, 24f, 90f, Car.Hatchback);

            c.Car(3.5f, 27f, 180f, Car.Sedan);
            c.Car(10.5f, 27f, 180f, Car.Muscle);

            c.Prop(Part.Barrel, 12f, 8f, 0f, 1.1f);
            c.Prop(Part.Barrel, 12f, 10f, 0f, 1.1f);

            // The gap between the two cars at the back; the yard is too tight to drive in
            // nose first, which is what makes it a reverse.
            c.Bay(7f, 27f, 180f, 3.0f, 6.3f);
        }

        private static void Supermarket(Writer c)
        {
            c.Start(-20f, 4f);

            // The store across the back.
            c.Block(0f, 46f, 0f, 44f, 7f, 8f);

            // The rows leave a cross aisle in the middle, from x = -3.8 to x = 3, and that
            // aisle is the only way to the back of the car park. The row kerbs have to stop
            // either side of it - run across it and the course is sealed at the first row,
            // with every space beyond it unreachable.
            const float AisleLeft = -4.5f;
            const float AisleRight = 3.5f;

            // Three rows of bays, nose to nose, with aisles between them.
            for (int row = 0; row < 3; row++)
            {
                float z = 14f + row * 13f;

                c.CarRow(-24f, z, 0f, 7, 3.2f);
                c.CarRow(4f, z, 0f, 6, 3.2f);

                c.Kerb(-26f, z + 3.4f, AisleLeft, z + 3.4f);
                c.Kerb(AisleRight, z + 3.4f, 26f, z + 3.4f);
            }

            // Along the front of the first row and up the cross aisle: the way in, which
            // is not obvious from the driver's seat with a row of cars across the view.
            c.Route(new Vector2(-20f, 6f), new Vector2(-0.4f, 6f), new Vector2(-0.4f, 27f));

            c.Arrow(-12f, 6.5f, 90f);
            c.Arrow(-0.4f, 13f, 0f);

            // Trolley shelters.
            c.Block(20f, 20f, 0f, 3f, 2.6f, 7f, Part.ConcreteYellow);
            c.Block(20f, 33f, 0f, 3f, 2.6f, 7f, Part.ConcreteYellow);

            // The one free space, in the middle row.
            c.Bay(0f, 27f, 0f, 3.3f, 6.4f);
        }

        private static void FullCarPark(Writer c)
        {
            c.Start(-18f, 4f);

            // As in the supermarket: the cross aisle between the two blocks of each row is
            // the only route to the back, so the kerbs stop either side of it.
            const float AisleLeft = -6f;
            const float AisleRight = 1.5f;

            for (int row = 0; row < 4; row++)
            {
                float z = 12f + row * 11f;

                c.CarRow(-22f, z, 0f, 6, 3.1f);
                c.CarRow(2f, z, 0f, 6, 3.1f);

                c.Kerb(-24f, z + 3.2f, AisleLeft, z + 3.2f);
                c.Kerb(AisleRight, z + 3.2f, 24f, z + 3.2f);
            }

            c.Route(new Vector2(-18f, 6f), new Vector2(-2.25f, 6f), new Vector2(-2.25f, 45f));

            c.Wall(-25f, 2f, -25f, 50f, 2.2f);
            c.Wall(25f, 2f, 25f, 50f, 2.2f);
            c.Wall(-25f, 50f, 25f, 50f, 2.2f);

            c.Arrow(-10f, 6.5f, 90f);
            c.Arrow(-2.25f, 12f, 0f);

            // Deep inside, on the back row, with a car either side. The bay faces back
            // down the aisle: this is a reverse mission, so the nose ends up pointing out.
            //
            // Only the left-hand car is placed here. The right-hand one is already in the
            // back row - a second car on the same spot was two cars inside each other.
            c.Bay(-1.5f, 45f, 180f, 3.0f, 6.3f);
            c.Car(-5f, 45f, 0f, Car.Sedan);
        }

        private static void PetrolStation(Writer c)
        {
            c.Start(-16f, 4f);

            // Two pump islands under a canopy.
            for (int island = 0; island < 2; island++)
            {
                float x = -8f + island * 10f;

                c.Kerb(x - 3f, 16f, x + 3f, 16f);
                c.Block(x - 1.6f, 16f, 0f, 1f, 1.9f, 1f, Part.ConcreteYellow);
                c.Block(x + 1.6f, 16f, 0f, 1f, 1.9f, 1f, Part.ConcreteYellow);

                c.Pillars(x - 3f, 12f, x + 3f, 12f, 2, 4.5f);
                c.Pillars(x - 3f, 20f, x + 3f, 20f, 2, 4.5f);
            }

            // The shop.
            c.Block(14f, 32f, 0f, 14f, 5f, 10f);

            c.Car(-14f, 30f, 0f, Car.Hatchback);
            c.Car(-10f, 30f, 0f, Car.Classic);

            c.Arrow(-16f, 9f, 0f);
            c.Kerb(-20f, 38f, 4f, 38f);

            c.Bay(1f, 32f, 0f, 3.3f, 6.5f);
        }

        private static void OnTheStreet(Writer c)
        {
            c.Start(-2.5f, 4f);

            // Road, pavement and buildings on one side.
            c.Kerb(5f, 2f, 5f, 56f);
            c.Kerb(-9f, 2f, -9f, 56f);

            c.Block(13f, 16f, 0f, 12f, 9f, 16f);
            c.Block(13f, 40f, 0f, 12f, 11f, 18f);

            for (int lamp = 0; lamp < 4; lamp++)
            {
                c.Prop(Part.Lamp, 6.5f, 10f + lamp * 13f, 180f, 5.5f);
            }

            // Kerbside queue with a gap in the middle. The cars are about 5m long, so
            // leaving their centres 14m apart gives a 9m space: room to swing in, and
            // nothing like enough to drive straight into.
            c.Car(2.6f, 17f, 0f, Car.Sedan);
            c.Car(2.6f, 24f, 0f, Car.Hatchback);
            c.Car(2.6f, 38f, 0f, Car.Muscle);
            c.Car(2.6f, 45f, 0f, Car.Classic);

            c.Arrow(-3f, 10f, 0f);

            c.Bay(2.6f, 31f, 0f, 2.9f, 7f);
        }

        private static void BusStation(Writer c)
        {
            c.Start(-14f, 4f);

            // Two coaches with a bay's width between them, and a third parked across the
            // yard so the approach is not a straight run.
            Coach(c, -4.2f, 34f, 0f);
            Coach(c, 4.2f, 34f, 0f);
            Coach(c, 16f, 20f, 90f);

            c.Kerb(-22f, 44f, 22f, 44f);
            c.Kerb(-6f, 14f, 6f, 14f);

            c.Block(0f, 48f, 0f, 20f, 4.5f, 6f);

            c.Prop(Part.PedestrianBarrier, -4f, 14f, 90f, 1.1f);
            c.Prop(Part.PedestrianBarrier, 4f, 14f, 90f, 1.1f);

            c.Arrow(-14f, 10f, 0f);

            // The gap between the two coaches, reversed into, so the nose ends up facing
            // back out of the stand.
            c.Bay(0f, 34f, 180f, 3.1f, 6.6f);
        }

        private static void LoadingYard(Writer c)
        {
            c.Start(-18f, 4f);

            // Warehouse and its dock across the back.
            c.Block(0f, 44f, 0f, 46f, 9f, 8f);
            c.Kerb(-20f, 39.5f, 20f, 39.5f);

            Container(c, -16f, 14f, 0f);
            Container(c, -16f, 21f, 0f);
            Container(c, -9f, 17f, 90f);
            Container(c, 14f, 14f, 0f);
            Container(c, 14f, 21f, 0f);
            Container(c, 20f, 30f, 0f);

            c.Prop(Part.Barrel, -4f, 26f, 0f, 1.1f);
            c.Prop(Part.Barrel, -2.6f, 26f, 0f, 1.1f);
            c.Prop(Part.Barrel, 8f, 30f, 0f, 1.1f);

            c.Barriers(-22f, 32f, -12f, 32f, 4);
            c.Arrow(-18f, 10f, 0f);

            // Backed up to the dock, so the nose ends up facing out into the yard.
            c.Bay(2f, 35f, 180f, 3.1f, 6.5f);
        }

        private static void ContainerMaze(Writer c)
        {
            c.Start(-16f, 4f);

            // Corridors: in along the bottom, right, up, left, and the bay at the top.
            for (int i = 0; i < 5; i++)
            {
                Container(c, -16f + i * 7f, 12f, 90f);
            }

            for (int i = 0; i < 4; i++)
            {
                Container(c, 16f, 16f + i * 7f, 0f);
            }

            for (int i = 0; i < 4; i++)
            {
                Container(c, 8f - i * 7f, 26f, 90f);
            }

            Container(c, -18f, 32f, 0f);
            Container(c, -18f, 39f, 0f);

            c.Wall(-21f, 2f, -21f, 48f, 3f);
            c.Wall(21f, 2f, 21f, 48f, 3f);
            c.Wall(-21f, 48f, 21f, 48f, 3f);

            c.Cones(-12f, 30f, -12f, 36f, 3);
            c.Arrow(-16f, 8f, 0f);

            c.Bay(2f, 42f, 0f, 3.2f, 6.5f);
        }

        private static void RoadWorks(Writer c)
        {
            c.Start(0f, 4f);

            for (int i = 0; i < 6; i++)
            {
                float z = 10f + i * 5f;
                c.Prop(Part.WaterBarricade, -4.5f, z, 0f, 1.1f);
                c.Prop(Part.WaterBarricade, 4.5f, z, 0f, 1.1f);
            }

            c.Prop(Part.Channelizing, -2.5f, 22f, 0f, 1.1f);
            c.Prop(Part.Channelizing, 2.5f, 30f, 0f, 1.1f);

            c.Cones(-3.5f, 40f, -3.5f, 46f, 3);
            c.Cones(3.5f, 40f, 3.5f, 46f, 3);

            c.Prop(Part.Barrel, -8f, 18f, 0f, 1.1f);
            c.Prop(Part.Barrel, 8f, 26f, 0f, 1.1f);
            c.Prop(Part.VerticalPanel, 0f, 8f, 0f, 1.2f);

            // Spoil heaps and plant, as blocks.
            c.Block(-13f, 30f, 0f, 6f, 2f, 10f, Part.ConcreteYellow);
            c.Block(13f, 36f, 0f, 5f, 2.4f, 8f, Part.ConcreteYellow);

            c.Bay(0f, 45f, 0f, 3.2f, 6.5f);
        }

        private static void CarParkDeck(Writer c)
        {
            c.Start(-16f, 4f);

            Perimeter(c, 1.5f, 2.6f, 5f, -16f);

            // Pillar grid, generously spaced.
            for (int row = 0; row < 3; row++)
            {
                c.Pillars(-16f, 12f + row * 12f, 16f, 12f + row * 12f, 5);
            }

            c.CarRow(-18f, 18f, 0f, 4, 3.3f);
            c.CarRow(6f, 30f, 0f, 4, 3.3f);

            c.Arrow(-16f, 9f, 0f);
            c.Kerb(-20f, 36f, 20f, 36f);

            c.Bay(-4f, 12f, 0f, 3.4f, 6.6f);
        }

        private static void PillarsAndPosts(Writer c)
        {
            c.Start(-14f, 4f);

            Perimeter(c, 1.5f, 2.8f, 4.5f, -14f);

            for (int row = 0; row < 4; row++)
            {
                c.Pillars(-15f, 10f + row * 10f, 15f, 10f + row * 10f, 6, 2.8f);
            }

            c.CarRow(-16f, 16f, 0f, 4, 3.1f);
            c.CarRow(4f, 16f, 0f, 4, 3.1f);
            c.CarRow(-16f, 34f, 0f, 3, 3.1f);
            c.Car(6f, 35.5f, 180f, Car.Muscle);
            c.Car(12.5f, 35.5f, 180f, Car.Sedan);

            // Between two cars, clear of the back wall: at z=38 the bay ran into it.
            c.Bay(9.3f, 35.5f, 180f, 2.9f, 6.3f);
        }

        // Two decks: an entrance apron, a ramp down, and the lower level. Each lays its
        // own floor, because one slab over the whole course would roof the lower deck in.
        private static void DownTheRamp(Writer c)
        {
            // The entrance is raised and the lower deck sits on the site, rather than the
            // deck being sunk: the whole training site stands on seven metres of slab, so
            // anything dug below its surface is inside solid concrete.
            const float Entrance = 2.6f;

            c.Level(Entrance);
            c.Start(0f, 4f);

            // Thick enough to reach the site surface it stands on.
            c.PadAt(0f, 7f, 20f, 16f, Entrance + 0.5f);
            c.Rail(0f, 7f, 20f, 16f, 11f);
            c.Arrow(0f, 8f, 0f);

            // Down to the lower deck. 16m of ramp for 2.6m of drop is about nine degrees:
            // a WheelCollider climbs that cleanly and catches on much more.
            c.Ramp(0f, 15f, 0f, 31f, 9f, -Entrance);

            c.Level(0f);
            c.PadAt(0f, 45f, 34f, 28f);

            // Railed on three sides; the fourth is where the ramp arrives.
            c.Wall(-17f, 31f, -17f, 59f, 2.8f);
            c.Wall(17f, 31f, 17f, 59f, 2.8f);
            c.Wall(-17f, 59f, 17f, 59f, 2.8f);
            c.Wall(-17f, 31f, -5.1f, 31f, 2.8f);
            c.Wall(5.1f, 31f, 17f, 31f, 2.8f);

            c.Pillars(-10f, 38f, 10f, 38f, 3, 2.8f);
            c.Pillars(-10f, 52f, 10f, 52f, 3, 2.8f);

            c.CarRow(-14f, 47f, 0f, 3, 3.2f);
            c.Prop(Part.Cone, 6f, 35f, 0f, 0.75f);

            c.Bay(7f, 46f, 0f, 3.3f, 6.5f);
        }

        private static void UpperDeck(Writer c)
        {
            c.Start(0f, 4f);

            c.PadAt(0f, 8f, 22f, 18f);
            c.Rail(0f, 8f, 22f, 18f, 11f);
            c.Arrow(0f, 8f, 0f);

            // Up one level: 20m of ramp for 3.4m, just under ten degrees.
            c.Ramp(0f, 17f, 0f, 37f, 9f, 3.4f);
            c.Wall(-5.1f, 17f, -5.1f, 37f, 1.6f, 0.5f);
            c.Wall(5.1f, 17f, 5.1f, 37f, 1.6f, 0.5f);

            c.Level(3.4f);
            c.PadAt(0f, 51f, 38f, 28f);

            // Parapet round the upper deck, open where the ramp arrives.
            c.Wall(-19f, 37f, -19f, 65f, 1.3f);
            c.Wall(19f, 37f, 19f, 65f, 1.3f);
            c.Wall(-19f, 65f, 19f, 65f, 1.3f);
            c.Wall(-19f, 37f, -5.1f, 37f, 1.3f);
            c.Wall(5.1f, 37f, 19f, 37f, 1.3f);

            c.Pillars(-12f, 44f, 12f, 44f, 4, 2.8f);
            c.Pillars(-12f, 58f, 12f, 58f, 4, 2.8f);

            c.CarRow(-16f, 52f, 0f, 4, 3.2f);
            c.Car(5f, 60f, 0f, Car.Classic);
            c.Car(11.5f, 60f, 0f, Car.Hatchback);

            c.Bay(8.3f, 60f, 0f, 3.2f, 6.5f);
        }

        private static void Rooftop(Writer c)
        {
            c.Start(0f, 4f);

            c.PadAt(0f, 7f, 22f, 16f);
            c.Rail(0f, 7f, 22f, 16f, 11f);
            c.Arrow(0f, 8f, 0f);

            // The building the roof deck sits on, and the ramp climbing its flank.
            c.Block(0f, 52f, 0f, 38f, 3.7f, 32f);
            c.Ramp(0f, 15f, 0f, 37f, 9f, 3.8f);
            c.Wall(-5.1f, 15f, -5.1f, 37f, 1.6f, 0.5f);
            c.Wall(5.1f, 15f, 5.1f, 37f, 1.6f, 0.5f);

            c.Level(3.8f);
            c.PadAt(0f, 52f, 36f, 30f);

            // Parapet all the way round the roof, open where the ramp arrives.
            c.Wall(-18f, 37f, -18f, 67f, 1.3f, 0.5f, Part.ConcreteYellow);
            c.Wall(18f, 37f, 18f, 67f, 1.3f, 0.5f, Part.ConcreteYellow);
            c.Wall(-18f, 67f, 18f, 67f, 1.3f, 0.5f, Part.ConcreteYellow);
            c.Wall(-18f, 37f, -5.1f, 37f, 1.3f, 0.5f, Part.ConcreteYellow);
            c.Wall(5.1f, 37f, 18f, 37f, 1.3f, 0.5f, Part.ConcreteYellow);

            c.CarRow(-15f, 62f, 180f, 4, 3.2f);
            c.Car(4f, 64f, 180f, Car.Muscle);
            c.Car(10.5f, 64f, 180f, Car.Sedan);

            c.Bay(7.3f, 64f, 180f, 3.0f, 6.4f);
        }

        private static void DrivingTest(Writer c)
        {
            c.Start(0f, 4f);

            c.Kerb(-8f, 6f, -8f, 30f);
            c.Kerb(8f, 6f, 8f, 30f);
            c.Slalom(10f, 28f, 3.2f, 4);

            var sBend = new[]
            {
                new Vector2(0f, 32f),
                new Vector2(0f, 40f),
                new Vector2(8f, 48f),
                new Vector2(8f, 56f),
                new Vector2(0f, 64f)
            };

            Lane(c, sBend, 2.9f, false);

            // Square corner out to the reversing bay.
            Lane(c, new[] { new Vector2(0f, 64f), new Vector2(0f, 72f), new Vector2(14f, 72f) }, 2.9f, true);

            c.Arrow(0f, 8f, 0f);
            c.Barriers(-12f, 70f, -12f, 76f, 3);

            // Clear of the lane wall that brackets the final corner at z=74.9.
            c.Bay(14f, 79f, 180f, 2.9f, 6.3f);
        }

        private static void Maze(Writer c)
        {
            c.Start(-20f, 4f);

            Perimeter(c, 1.5f, 2.8f, 5f, -20f);

            // Spine walls with gaps, giving lanes, dead ends and one way through.
            c.Wall(-14f, 10f, 20f, 10f, 2.4f);
            c.Wall(-24f, 18f, 8f, 18f, 2.4f);
            c.Wall(-6f, 26f, 24f, 26f, 2.4f);
            c.Wall(-24f, 34f, 10f, 34f, 2.4f);
            c.Wall(-2f, 42f, 24f, 42f, 2.4f);

            c.Wall(-14f, 10f, -14f, 18f, 2.4f);
            c.Wall(8f, 18f, 8f, 26f, 2.4f);
            c.Wall(-6f, 26f, -6f, 34f, 2.4f);
            c.Wall(10f, 34f, 10f, 42f, 2.4f);

            // Dead ends, so the route is not simply the only corridor.
            c.Car(16f, 14f, 90f, Car.Sedan);
            c.Car(-18f, 22f, 90f, Car.Hatchback);
            c.Car(18f, 30f, 90f, Car.Muscle);

            c.Barriers(-20f, 38f, -14f, 38f, 3);
            c.Arrow(-20f, 7f, 0f);

            c.Bay(-16f, 47f, 0f, 3.1f, 6.4f);
        }

        private static void FinalExamination(Writer c)
        {
            // Raised entrance, deck at site level - same reason as Down The Ramp.
            const float Entrance = 2.4f;

            c.Level(Entrance);
            c.Start(0f, 4f);

            c.PadAt(0f, 7f, 22f, 16f, Entrance + 0.5f);
            c.Rail(0f, 7f, 22f, 16f, 11f);
            c.Arrow(0f, 8f, 0f);

            c.Ramp(0f, 15f, 0f, 31f, 9f, -Entrance);

            c.Level(0f);
            c.PadAt(0f, 52f, 44f, 44f);

            c.Wall(-22f, 30f, -22f, 74f, 3f);
            c.Wall(22f, 30f, 22f, 74f, 3f);
            c.Wall(-22f, 74f, 22f, 74f, 3f);
            c.Wall(-22f, 30f, -5.1f, 30f, 3f);
            c.Wall(5.1f, 30f, 22f, 30f, 3f);

            c.Pillars(-16f, 38f, 16f, 38f, 5, 2.8f);
            c.Pillars(-16f, 52f, 16f, 52f, 5, 2.8f);
            c.Pillars(-16f, 66f, 16f, 66f, 5, 2.8f);

            c.CarRow(-19f, 44f, 0f, 3, 3.1f);
            c.CarRow(8f, 44f, 0f, 3, 3.1f);

            c.Cones(-6f, 34f, 6f, 34f, 5);
            c.Barriers(-12f, 58f, -6f, 58f, 3);

            // The last space: parallel, against the wall, with a car at each end and a
            // metre at either end of the car. Nothing to spare, as advertised.
            c.Car(18f, 56f, 0f, Car.Sedan);
            c.Car(18f, 68f, 0f, Car.Hatchback);
            c.Bay(18f, 62f, 0f, 2.8f, 6.8f);
        }
    }
}
