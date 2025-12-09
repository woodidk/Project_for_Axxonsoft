using Avalonia;
using Avalonia.Platform.Storage;
using forAxxon.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace forAxxon;

public static class SessionManager
{
    private static readonly string s_sessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "forAxxon",
        "session.json"
    );

    static SessionManager()
    {
        var dir = Path.GetDirectoryName(s_sessionFile)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    public static string? GetLastFilePath()
    {
        try
        {
            var json = File.ReadAllText(s_sessionFile);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("LastFilePath", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                var path = prop.GetString();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Невозможно прочитать сессию: {ex}");
            return null;
        }
    }

    public static void SetLastFilePath(string? path)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { LastFilePath = path }, JsonSettings.Options);
            File.WriteAllText(s_sessionFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка записи сессии: {ex}");
        }
    }
}