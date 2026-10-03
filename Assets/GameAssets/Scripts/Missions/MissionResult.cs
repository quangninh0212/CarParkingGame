namespace CarParkingGame.Missions
{
    public enum MissionFailReason
    {
        None,
        ScoreTooLow,
        TimeExpired,
        LeftMissionArea,
        Aborted
    }

    public class MissionResult
    {
        public int missionId;
        public bool success;
        public MissionFailReason failReason = MissionFailReason.None;
        public int score;
        public int stars;
        public float timeSeconds;
        public int collisions;
        public int overtimePenalty;
        public int coinsAwarded;
        public bool isNewBest;
        public bool isFirstCompletion;

        public string FailReasonText
        {
            get
            {
                switch (failReason)
                {
                    case MissionFailReason.ScoreTooLow:
                        return "Score Too Low";
                    case MissionFailReason.TimeExpired:
                        return "Time Expired";
                    case MissionFailReason.LeftMissionArea:
                        return "Left Mission Area";
                    case MissionFailReason.Aborted:
                        return "Mission Cancelled";
                    default:
                        return string.Empty;
                }
            }
        }
    }
}
