using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class PostMatchData : IValidable
    {
        [JsonProperty("userID")]
        public Guid UserID { get; set; } = Guid.Empty;

        [JsonProperty("matchData")]
        public MatchData MatchData { get; set; }

        public bool IsValid =>
            (UserID != Guid.Empty) &&
            Protection.IsValid(MatchData);
    }
}
