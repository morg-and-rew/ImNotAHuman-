using System;
using System.IO;
using UnityEngine;

/// <summary> Временное NDJSON-логирование для отладочной сессии (файл в корне проекта). </summary>
public static class AgentDebugNdjson
{
    private const string SessionId = "3f85b0";
    private const string RelPath = "debug-3f85b0.log";

    public static void Log(string hypothesisId, string location, string message, string dataJson = "{}")
    {
        try
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, RelPath);
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line =
                "{\"sessionId\":\"" + SessionId + "\",\"hypothesisId\":\"" + hypothesisId
                + "\",\"location\":\"" + Esc(location) + "\",\"message\":\"" + Esc(message) + "\",\"data\":" + dataJson + ",\"timestamp\":" + ts + "}\n";
            File.AppendAllText(path, line);
        }
        catch
        {
            // never break game flow
        }
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
