using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ErrorData : IValidable
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        public bool IsValid => Error != null;

        public ErrorData(string error) => Error = error ?? throw new ArgumentNullException(nameof(error));
    }
}
