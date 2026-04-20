using System;
using System.IO;

namespace HermesBridge.HermesBridgeCode;

internal static class BridgePaths
{
    public static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2",
        "hermesbridge");

    public static string StateJsonPath => Path.Combine(BaseDirectory, "state.json");
    public static string ErrorPath => Path.Combine(BaseDirectory, "last-error.txt");

    /// <summary>
    /// Hermes (or any external controller) writes commands here.
    /// Schema: { "id": &lt;monotonic int&gt;, "command": { "type": "...", ...args } }.
    /// The mod ignores any payload whose id is &lt;= the last processed id.
    /// </summary>
    public static string CommandsJsonPath => Path.Combine(BaseDirectory, "commands.json");

    /// <summary>
    /// The mod writes the dispatch result here after processing a command.
    /// Schema: { "id", "status": "ok"|"error"|"ignored", "message", "timestampUtc", "revision" }.
    /// </summary>
    public static string ResultJsonPath => Path.Combine(BaseDirectory, "result.json");
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
    }
}
