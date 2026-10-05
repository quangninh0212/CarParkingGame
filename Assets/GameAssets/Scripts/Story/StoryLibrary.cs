using System.Collections.Generic;

namespace CarParkingGame.Story
{
    // The thread running through the fifty levels: a trainee works their way from a test
    // pad to a town centre nobody else will park in.
    //
    // Chapters are keyed to the level a chapter opens on, so adding levels in the middle
    // moves a chapter boundary by editing one number. Nothing reads these but the screens
    // that show them.
    public static class StoryLibrary
    {
        public class Chapter
        {
            public int number;
            public int opensOnMission;
            public string title;
            public string line;
        }

        public const string PrologueTitle = "THE LICENCE";

        public static readonly string[] Prologue =
        {
            "You passed the written exam on a Tuesday.",
            "The examiner handed it back without looking up and said the hard part was the other half - that anyone can learn the rules, and almost nobody can put a car between two painted lines without touching either of them.",
            "There is a test pad at the old circuit. Eight bays, a clipboard, and a man who has failed better drivers than you.",
            "Start the engine."
        };

        // The opening as one block of text, so the screen that shows it and the tool that
        // writes it into the scene cannot disagree about how the lines are joined.
        public static string PrologueText => string.Join("\n\n", Prologue);

        public static IReadOnlyList<Chapter> All => Chapters;

        private static readonly List<Chapter> Chapters = new List<Chapter>
        {
            new Chapter
            {
                number = 1,
                opensOnMission = 1,
                title = "THE TEST PAD",
                line = "Eight bays at the old circuit, and a man with a clipboard who has failed better drivers than you."
            },
            new Chapter
            {
                number = 2,
                opensOnMission = 9,
                title = "FIRST JOB",
                line = "The licence is signed. A car park on the edge of town needs somebody who can read a bay before they fill it."
            },
            new Chapter
            {
                number = 3,
                opensOnMission = 17,
                title = "THE SQUARE",
                line = "Fountains, planters, and people who park where they like. The town centre is where valets are made, or found out."
            },
            new Chapter
            {
                number = 4,
                opensOnMission = 27,
                title = "TIGHT QUARTERS",
                line = "Alleys a hand's width wider than the mirrors, and crossroads that want the car at four different angles."
            },
            new Chapter
            {
                number = 5,
                opensOnMission = 37,
                title = "RUSH HOUR",
                line = "Full lots, four spaces at a time, and nowhere left to be careless. This is the job you said you wanted."
            }
        };

        // The chapter a level belongs to, or null if there are no chapters at all.
        public static Chapter For(int missionId)
        {
            Chapter found = null;

            foreach (Chapter chapter in Chapters)
            {
                if (chapter.opensOnMission <= missionId)
                {
                    found = chapter;
                }
            }

            return found;
        }

        // True when this level is the one a chapter opens on, which is when it is worth
        // saying so.
        public static bool Opens(int missionId, out Chapter chapter)
        {
            foreach (Chapter candidate in Chapters)
            {
                if (candidate.opensOnMission == missionId)
                {
                    chapter = candidate;
                    return true;
                }
            }

            chapter = null;
            return false;
        }
    }
}
