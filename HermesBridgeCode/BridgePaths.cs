using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace HermesBridge.HermesBridgeCode;

internal static class BridgePaths
{
    // Diagnostic captured during static init (BridgeTrace cannot be safely
    // called here — it would re-enter BridgePaths.BaseDirectory before the
    // field is initialized and the trace line would be silently dropped).
    // The mod entry point flushes this via FlushInitDiagnostic() once the
    // bridge is fully constructed.
    private static string? _initDiagnostic;

    private static readonly string BaseDirectoryValue = ResolveBaseDirectory(
        envOverride: Environment.GetEnvironmentVariable("HERMES_IPC_DIR"),
        dllDirectory: Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
        appData: Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        diagnostic: out _initDiagnostic);

    /// <summary>
    /// Pure resolution of the IPC base directory. Exposed internal for
    /// testability. Resolution order (first match wins):
    ///   1. envOverride (HERMES_IPC_DIR) — used literally
    ///   2. hermes-instance.cfg next to the DLL — appended as
    ///      "hermesbridge-{instanceId}" under appData/SlayTheSpire2
    ///   3. fallback: appData/SlayTheSpire2/hermesbridge
    ///
    /// The instance id read from hermes-instance.cfg is sanitized: a UTF-8
    /// BOM is stripped, the value is trimmed, and only ASCII letters,
    /// digits, '-' and '_' are accepted. An invalid id falls through to
    /// the fallback (with a diagnostic emitted) — this prevents path
    /// traversal via "../" or unicode tricks.
    /// </summary>
    internal static string ResolveBaseDirectory(
        string? envOverride,
        string? dllDirectory,
        string appData,
        out string? diagnostic)
    {
        diagnostic = null;

        if (!string.IsNullOrEmpty(envOverride))
        {
            diagnostic = $"BridgePaths: HERMES_IPC_DIR override active: {envOverride}";
            return envOverride;
        }

        if (!string.IsNullOrEmpty(dllDirectory))
        {
            var cfgPath = Path.Combine(dllDirectory, "hermes-instance.cfg");
            if (File.Exists(cfgPath))
            {
                string raw;
                try
                {
                    raw = File.ReadAllText(cfgPath, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    diagnostic = $"BridgePaths: failed to read hermes-instance.cfg ({ex.GetType().Name}: {ex.Message}); using default";
                    return DefaultPath(appData);
                }

                var instanceId = SanitizeInstanceId(raw);
                if (!string.IsNullOrEmpty(instanceId))
                {
                    var path = Path.Combine(appData, "SlayTheSpire2", $"hermesbridge-{instanceId}");
                    diagnostic = $"BridgePaths: instance config '{instanceId}' active: {path}";
                    return path;
                }

                diagnostic = $"BridgePaths: hermes-instance.cfg present but contained no valid id (allowed: [A-Za-z0-9_-]+); using default";
            }
        }

        return DefaultPath(appData);
    }

    private static string DefaultPath(string appData)
        => Path.Combine(appData, "SlayTheSpire2", "hermesbridge");

    /// <summary>
    /// Strips UTF-8 BOM, trims whitespace, and validates against
    /// [A-Za-z0-9_-]+. Returns null on any invalid input. Public-internal
    /// for testability.
    /// </summary>
    internal static string? SanitizeInstanceId(string raw)
    {
        if (raw is null) return null;
        var trimmed = raw.TrimStart('\uFEFF').Trim();
        if (trimmed.Length == 0) return null;
        if (!Regex.IsMatch(trimmed, "^[A-Za-z0-9_-]+$")) return null;
        return trimmed;
    }

    public static string BaseDirectory => BaseDirectoryValue;

    /// <summary>
    /// Emit the static-init diagnostic (if any) via the supplied logger.
    /// Called once from the mod entry point after BridgeTrace is safe to
    /// use. No-op if no diagnostic was produced (default fallback path)
    /// or already flushed. The logger argument is decoupled from
    /// BridgeTrace so this class has no dependency on game-side logging
    /// infrastructure (testability).
    /// </summary>
    public static void FlushInitDiagnostic(Action<string> log)
    {
        var msg = _initDiagnostic;
        if (msg is null) return;
        _initDiagnostic = null;
        try { log(msg); } catch { }
    }

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

    /// <summary>
    /// Bridge appends one JSONL row here every time TotalFloor changes
    /// during a run. Schema:
    /// { "t":"&lt;iso8601&gt;", "floor":N, "act":N, "hp":N, "maxHp":N,
    ///   "gold":N, "deckSize":N, "relicCount":N, "potionCount":N,
    ///   "roomType":"Combat|Elite|Shop|Event|Rest|Treasure|Boss|..." }
    /// Used by SpireBench post-hoc analysis to chart HP/gold/deck
    /// curves per run. File is per-game-process; not cleared between
    /// runs (each run's rows are scoped by t/floor=1 boundaries).
    /// </summary>
    public static string FloorHistoryPath => Path.Combine(BaseDirectory, "floor-history.jsonl");
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
    }
}
