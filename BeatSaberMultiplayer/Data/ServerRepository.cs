using System;
using System.Collections.Generic;

using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BeatSaberMultiplayer.Data
{
    public partial class ServerRepository
    {
        [JsonProperty("RepositoryName")]
        public string RepositoryName { get; set; }

        [JsonProperty("RepositoryDescription", NullValueHandling = NullValueHandling.Ignore)]
        public string RepositoryDescription { get; set; }

        [JsonProperty("Servers")]
        public List<RepositoryServer> Servers { get; set; }

        public override string ToString()
        {
            return $"{RepositoryName}: {Servers.Count} servers";
        }
    }

    public partial class RepositoryServer
    {
        [JsonProperty("ServerName", NullValueHandling = NullValueHandling.Ignore)]
        public string ServerName { get; set; }

        [JsonProperty("ServerAddress")]
        public string ServerAddress { get; set; }

        [JsonProperty("ServerPort")]
        public int ServerPort { get; set; }

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(ServerAddress))
                    return false;
                if (ServerPort < 1 || ServerPort > 65535)
                    return false;
                return true;
            }
        }

        public override string ToString()
        {
            string retStr = string.Empty;
            if (!string.IsNullOrEmpty(ServerName))
                retStr = ServerName + " | ";
            if (!string.IsNullOrEmpty(ServerAddress))
                retStr = retStr + ServerAddress + ":" + ServerPort;
            else
                retStr = retStr + "<NULL>:" + ServerPort;
            return retStr;
        }
    }

    public partial class ServerRepository
    {
        public static ServerRepository FromJson(string json) => JsonConvert.DeserializeObject<ServerRepository>(json, BeatSaberMultiplayer.Data.Converter.Settings);
    }

    public static class Serialize
    {
        public static string ToJson(this ServerRepository self) => JsonConvert.SerializeObject(self, BeatSaberMultiplayer.Data.Converter.Settings);
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            //Converters =
            //{
            //    new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            //},
        };
    }
}