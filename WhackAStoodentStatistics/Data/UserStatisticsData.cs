using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class UserStatisticsData : IValidable
    {
        [JsonProperty("userID")]
        public Guid UserID { get; set; } = Guid.Empty;

        [JsonProperty("gamesPlayed")]
        public ulong GamePlayCount { get; set; }

        [JsonProperty("gamesWon")]
        public ulong GameWinCount { get; set; }

        [JsonProperty("gamesLost")]
        public ulong GameLooseCount { get; set; }

        [JsonProperty("gamesTied")]
        public ulong GameTieCount { get; set; }

        [JsonProperty("lastOnline")]
        public DateTime LastOnlineDateTime { get; set; } = DateTime.Now;

        public bool IsValid =>
            (UserID != Guid.Empty) &&
            GamePlayCount == (GameWinCount + GameLooseCount + GameTieCount);

        public UserStatisticsData()
        {
            // ...
        }

        public UserStatisticsData(Guid userID, ulong gamePlayCount, ulong gameWinCount, ulong gameLooseCount, ulong gameTieCount, DateTime lastOnlineDateTime)
        {
            if (userID == Guid.Empty)
            {
                throw new ArgumentException("User ID can't be empty.", nameof(userID));
            }
            if (gamePlayCount != (gameWinCount + gameLooseCount + gameTieCount))
            {
                throw new ArgumentException("Game play count needs to add up to the sum of win count, loose count and tie count.", nameof(userID));
            }
            UserID = userID;
            GamePlayCount = gamePlayCount;
            GameWinCount = gameWinCount;
            GameLooseCount = gameLooseCount;
            LastOnlineDateTime = lastOnlineDateTime;
        }
    }
}
