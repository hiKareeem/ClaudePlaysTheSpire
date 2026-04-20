using System;
using System.IO;

namespace HermesBridge.HermesBridgeCode;

internal static class BridgeTrace
{
    public static void Log(string message)
    {
        var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";

        try
        {
            BridgePaths.EnsureDirectories();
            File.AppendAllText(Path.Combine(BridgePaths.BaseDirectory, "trace.log"), line);
        }
        catch
        {
        }

        MainFile.Logger.Info($"[Trace] {message}");
    }
}
