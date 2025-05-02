using System.Text;
using MelonLoader;
using UnityEngine;

namespace BusinessIntelligenceMod.Utils;

public static class CsvLogger
{
    private static readonly string DataDir = Path.Combine(Application.persistentDataPath, "WeedAnalytics");
    private static readonly string CsvPath = Path.Combine(DataDir, "weed_analytics_data.csv");

    private const string DataLogHeader = "GameTime,RealTime,EventType,Payload";

    public static void Initialize()
    {
        try
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            if (!File.Exists(CsvPath))
                File.WriteAllText(CsvPath, DataLogHeader + Environment.NewLine);

            MelonLogger.Msg("Data log ready, data will be saved to:");
            MelonLogger.Msg($"{CsvPath}");
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"CsvLogger Initialize failed: {ex}");
        }
    }

    public static string CreatePayload(params KeyValuePair<string, string>[] pairs)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        for (var i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            sb.Append($"\"{pair.Key}\":\"{pair.Value.Replace("\"", "\\\"")}\"");

            if (i < pairs.Length - 1)
                sb.Append(',');
        }

        sb.Append('}');
        return sb.ToString();
    }

    public static void LogEvent(string eventType, string payload)
    {
        try
        {
            var gameTime = DateTime.Now.ToString("HH:mm:ss");
            var realTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Escape any commas in the payload with quotes to maintain CSV integrity
            var escapedPayload = $"\"{payload.Replace("\"", "\"\"")}\"";

            var logEntry = $"{gameTime},{realTime},{eventType},{escapedPayload}";

            // Append to the CSV file
            File.AppendAllText(CsvPath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Error logging event: {ex.Message}");
        }
    }
}