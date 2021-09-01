using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WhackAStoodentStatistics
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EPlayerRole
    {
        Invalid = -1,

        Hitter = 0,

        Mole
    }
}
