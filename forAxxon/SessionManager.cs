// SessionManager.cs
using forAxxon.Models;
using System;
using System.IO;
using System.Text.Json;

namespace forAxxon;

public static class SessionManager
{
    private static readonly string SessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "forAxxon",
        "session.json"
    );

    static SessionManager()
    {
        var dir = Path.GetDirectoryName(SessionFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public static string? GetLastFilePath()
    {
        if (!File.Exists(SessionFile))
            return null;

        try
        {
            var json = File.ReadAllText(SessionFile);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("LastFilePath", out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var path = prop.GetString();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка чтения сессии: {ex}");
        }
        return null;
    }

    public static void SetLastFilePath(string? path)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { LastFilePath = path }, JsonSettings.Options);
            File.WriteAllText(SessionFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи сессии: {ex}");
        }
    }
}