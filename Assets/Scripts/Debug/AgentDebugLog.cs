using System;
using System.IO;
using UnityEngine;

internal static class AgentDebugLog
{
    private static string LogPath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", ".cursor", "debug.log");

    public static void Log(string location, string message, string dataJson = null, string hypothesisId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(dataJson)) dataJson = "{}";
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            var id = "log_" + timestamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var entry = "{\"id\":\"" + id + "\",\"timestamp\":" + timestamp + ",\"location\":\"" + Escape(location) + "\",\"message\":\"" + Escape(message) + "\",\"data\":" + dataJson + (string.IsNullOrEmpty(hypothesisId) ? "" : ",\"hypothesisId\":\"" + hypothesisId + "\"") + "}\n";
            File.AppendAllText(LogPath, entry);
        }
        catch { /* ignore */ }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
