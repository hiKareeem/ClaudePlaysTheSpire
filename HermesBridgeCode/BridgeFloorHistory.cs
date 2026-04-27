using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// Per-floor JSONL snapshot writer for SpireBench post-hoc analysis.
/// Appends one line to <see cref="BridgePaths.FloorHistoryPath"/> when
/// <see cref="TryAppendIfFloorAdvanced"/> is called with a floor
/// number that differs from the last logged floor. Idempotent within
/// a single floor: repeated calls with the same floor number do
/// nothing.
///
/// State lives in a static field for the lifetime of the game
/// process. Restarting the game resets the cursor to 0, which is the
/// correct behaviour at run boundaries (operator restarts game
/// between SpireBench runs per protocol.md §Per-run setup).
/// </summary>
internal static class BridgeFloorHistory
{
    private static int _lastLoggedFloor = -1;
    private static readonly object _lock = new();

    public static void TryAppendIfFloorAdvanced(
        int floor,
        int act,
        int hp,
        int maxHp,
        int gold,
        int deckSize,
        int relicCount,
        int potionCount,
        string? roomType)
    {
        if (floor <= 0) return;

        lock (_lock)
        {
            if (floor == _lastLoggedFloor) return;
            _lastLoggedFloor = floor;

            // Hand-rolled JSON: avoids pulling in a serializer just for one line,
            // keeps field order stable for diff-friendliness, and matches the
            // exact shape documented in BridgePaths.FloorHistoryPath.
            var sb = new StringBuilder(160);
            sb.Append('{');
            sb.Append("\"t\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)).Append('"');
            sb.Append(",\"floor\":").Append(floor.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"act\":").Append(act.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"hp\":").Append(hp.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"maxHp\":").Append(maxHp.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"gold\":").Append(gold.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"deckSize\":").Append(deckSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"relicCount\":").Append(relicCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"potionCount\":").Append(potionCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"roomType\":");
            if (string.IsNullOrEmpty(roomType))
            {
                sb.Append("null");
            }
            else
            {
                sb.Append('"').Append(EscapeJson(roomType!)).Append('"');
            }
            sb.Append('}').Append('\n');

            try
            {
                BridgePaths.EnsureDirectories();
                File.AppendAllText(BridgePaths.FloorHistoryPath, sb.ToString());
            }
            catch (Exception ex)
            {
                // Never let a logging failure break state extraction.
                BridgeTrace.Log($"FloorHistory append failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reset the floor cursor. Useful if a future BridgeCommand wants
    /// to force a fresh history segment without restarting the game
    /// (not used currently — game restart between runs is the
    /// documented procedure).
    /// </summary>
    public static void ResetCursor()
    {
        lock (_lock) { _lastLoggedFloor = -1; }
    }

    private static string EscapeJson(string s)
    {
        var sb = new StringBuilder(s.Length + 4);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':  sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20)
                        sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
