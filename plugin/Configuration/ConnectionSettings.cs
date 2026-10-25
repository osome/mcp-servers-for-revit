using Newtonsoft.Json;
using revit_mcp_plugin.Core;
using System;
using System.IO;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// Per-user connection settings, kept beside connection.json in %LOCALAPPDATA% so each user
    /// can pick a free port without write access to the add-in folder. The MCP server needs no
    /// matching setting: it reads the port the listener actually bound from connection.json.
    /// </summary>
    public class ConnectionSettings
    {
        public const int DefaultPort = 8080;

        // Below 1024 needs admin rights on some machines; 0 would mean "any free port", which the server can't guess.
        public const int MinPort = 1024;
        public const int MaxPort = 65535;

        [JsonProperty("port")]
        public int Port { get; set; } = DefaultPort;

        public static string FilePath => Path.Combine(
            Path.GetDirectoryName(ConnectionInfoFile.FilePath),
            "settings.json");

        public static bool IsValidPort(int port) => port >= MinPort && port <= MaxPort;

        /// <summary>
        /// Returns the saved settings, or the defaults if the file is missing, unreadable or out of range.
        /// </summary>
        public static ConnectionSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var settings = JsonConvert.DeserializeObject<ConnectionSettings>(File.ReadAllText(FilePath));
                    if (settings != null && IsValidPort(settings.Port))
                        return settings;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
            }

            return new ConnectionSettings();
        }

        public void Save()
        {
            if (!IsValidPort(Port))
                throw new ArgumentOutOfRangeException(nameof(Port), $"Port must be between {MinPort} and {MaxPort}.");

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
