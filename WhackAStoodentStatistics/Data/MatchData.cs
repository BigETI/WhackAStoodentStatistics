using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class MatchData : IValidable
    {
        [JsonProperty("sessionID")]
        public Guid SessionID { get; set; } = Guid.Empty;

        [JsonProperty("yourName")]
        public string YourName { get; set; } = string.Empty;

        [JsonProperty("yourRole")]
        public EPlayerRole YourRole { get; set; } = EPlayerRole.Invalid;

        [JsonProperty("yourScore")]
        public long YourScore { get; set; }

        [JsonProperty("opponentName")]
        public string OpponentName { get; set; } = string.Empty;

        [JsonProperty("opponentRole")]
        public EPlayerRole OpponentRole { get; set; } = EPlayerRole.Invalid;

        [JsonProperty("opponentScore")]
        public long OpponentScore { get; set; }

        public virtual bool IsValid =>
            (SessionID != Guid.Empty) &&
            !string.IsNullOrWhiteSpace(YourName) &&
            (YourRole != EPlayerRole.Invalid) &&
            !string.IsNullOrWhiteSpace(OpponentName) &&
            (OpponentRole != EPlayerRole.Invalid) &&
            (YourRole != OpponentRole);

        public MatchData()
        {
            // ...
        }

        public MatchData(Guid sessionID, long yourScore, EPlayerRole yourRole, string yourName, long opponentScore, EPlayerRole opponentRole, string opponentName)
        {
            if (sessionID == Guid.Empty)
            {
                throw new ArgumentException("Session ID can't be empty.", nameof(sessionID));
            }
            if (yourRole == EPlayerRole.Invalid)
            {
                throw new ArgumentException("Your role can't be invalid.", nameof(yourRole));
            }
            if (opponentRole == EPlayerRole.Invalid)
            {
                throw new ArgumentException("Opponent's role can't be invalid.", nameof(opponentRole));
            }
            SessionID = sessionID;
            YourScore = yourScore;
            YourRole = yourRole;
            YourName = yourName ?? throw new ArgumentNullException(nameof(yourName));
            OpponentScore = opponentScore;
            OpponentRole = opponentRole;
            OpponentName = opponentName ?? throw new ArgumentNullException(nameof(opponentName));
        }
    }
}
