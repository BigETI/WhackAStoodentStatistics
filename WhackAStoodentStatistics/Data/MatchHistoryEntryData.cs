using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class MatchHistoryEntryData : MatchData
    {
        [JsonProperty("playedDateTime")]
        public DateTime PlayedDateTime { get; set; }

        public MatchHistoryEntryData()
        {
            // ...
        }

        public MatchHistoryEntryData(DateTime playedDateTime, Guid sessionID, long yourScore, EPlayerRole yourRole, string yourName, long opponentScore, EPlayerRole opponentRole, string opponentName) : base(sessionID, yourScore, yourRole, yourName, opponentScore, opponentRole, opponentName) => PlayedDateTime = playedDateTime;
    }
}
