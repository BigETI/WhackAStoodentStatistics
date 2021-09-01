using Newtonsoft.Json;
using System;

namespace WhackAStoodentStatistics.Data
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ConfigurationData : IValidable
    {
        [JsonProperty("host")]
        public string Host { get; set; } = "localhost";

        [JsonProperty("port")]
        public ushort Port { get; set; } = 3306;

        [JsonProperty("database")]
        public string Database { get; set; } = "mydatabase";

        [JsonProperty("username")]
        public string Username { get; set; } = "myuser";

        [JsonProperty("password")]
        public string Password { get; set; } = "mypassword";

        [JsonProperty("listeingTo")]
        public string ListeningTo { get; set; } = "http://*:57008";

        public bool IsValid =>
            (Host != null) &&
            (Port != 0) &&
            (Database != null) &&
            (Username != null) &&
            (Password != null) &&
            (ListeningTo != null);
        
        public ConfigurationData()
        {
            // ...
        }

        public ConfigurationData(string host, ushort port, string database, string username, string password, string listeningTo)
        {
            if (port == 0)
            {
                throw new ArgumentException("Port must be bigger than zero", nameof(port));
            }
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            Database = database ?? throw new ArgumentNullException(nameof(database));
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            ListeningTo = listeningTo ?? throw new ArgumentNullException(nameof(listeningTo));
        }
    }
}
