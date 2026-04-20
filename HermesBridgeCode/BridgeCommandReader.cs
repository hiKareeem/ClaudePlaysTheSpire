using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using MegaCrit.Sts2.Core.Commands;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// Background poll loop that watches <see cref="BridgePaths.CommandsJsonPath"/> for
/// new command payloads from an external controller (Hermes), then marshals dispatch
/// to the Godot main thread via <see cref="BridgeMainThreadDispatcher"/>.
///
/// Protocol:
///   commands.json: { "id": &lt;monotonic int&gt;, "command": { "type": "...", ...args } }
///   result.json:   { "id", "status": "ok"|"error"|"ignored", "message", "timestampUtc", "revision" }
///
/// The reader keeps an in-memory <see cref="_lastProcessedId"/>; any payload whose id
/// is &lt;= that is silently ignored (idempotent). The reader debounces via file mtime
/// so only fresh writes trigger a re-read.
/// </summary>
internal static class BridgeCommandReader
{
    private static Thread? _thread;
    private static volatile bool _stopRequested;
    private static int _lastProcessedId;
    private static DateTime _lastSeenMtimeUtc = DateTime.MinValue;

    /// <summary>Start the polling thread. Idempotent.</summary>
    public static void Start()
    {
        if (_thread is not null) return;
        _stopRequested = false;
        _thread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name = "HermesBridgeCommandReader",
        };
        _thread.Start();
        BridgeTrace.Log($"BridgeCommandReader started; watching {BridgePaths.CommandsJsonPath}");
    }

    public static void Stop()
    {
        _stopRequested = true;
    }

    private static void PollLoop()
    {
        while (!_stopRequested)
        {
            try
            {
                PollOnce();
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"BridgeCommandReader.PollLoop iteration threw: {ex.Message}");
            }
            Thread.Sleep(100); // 10 Hz poll
        }
    }

    private static void PollOnce()
    {
        var path = BridgePaths.CommandsJsonPath;
        if (!File.Exists(path)) return;

        DateTime mtime;
        try { mtime = File.GetLastWriteTimeUtc(path); }
        catch { return; }

        if (mtime <= _lastSeenMtimeUtc) return;
        _lastSeenMtimeUtc = mtime;

        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex)
        {
            BridgeTrace.Log($"BridgeCommandReader read failed: {ex.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (Exception ex)
        {
            BridgeTrace.Log($"BridgeCommandReader parse failed: {ex.Message}");
            return;
        }

        try
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
            {
                BridgeTrace.Log("BridgeCommandReader: payload missing numeric 'id'");
                return;
            }
            var id = idEl.GetInt32();
            if (id <= _lastProcessedId)
            {
                // Stale or replayed; silently ignore (no result write).
                return;
            }

            if (!root.TryGetProperty("command", out var cmdEl) || cmdEl.ValueKind != JsonValueKind.Object)
            {
                WriteResult(id, "error", "payload missing 'command' object");
                _lastProcessedId = id;
                return;
            }

            string typeName = "<unknown>";
            if (cmdEl.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                typeName = typeEl.GetString() ?? "<null>";
            }

            BridgeTrace.Log($"BridgeCommandReader dispatching id={id} type={typeName}");
            // Snapshot the command JSON text so we can re-parse on the main thread without
            // racing the JsonDocument disposal.
            var cmdText = cmdEl.GetRawText();
            var capturedId = id;
            var capturedType = typeName;

            var enqueued = BridgeMainThreadDispatcher.Enqueue(() =>
            {
                try
                {
                    using var cmdDoc = JsonDocument.Parse(cmdText);
                    var (status, message) = BridgeCommandDispatcher.Dispatch(capturedType, cmdDoc.RootElement);
                    // Force a state snapshot after every dispatch so callers see the post-command
                    // world without having to wait for the next natural hook. Cheap & idempotent.
                    try { BridgeSnapshotWriter.RequestWrite($"PostDispatch:{capturedType}"); }
                    catch (Exception wex) { BridgeTrace.Log($"post-dispatch RequestWrite threw: {wex.Message}"); }
                    WriteResult(capturedId, status, message);
                }
                catch (Exception ex)
                {
                    WriteResult(capturedId, "error", $"dispatch threw: {ex.Message}");
                    BridgeTrace.Log($"BridgeCommandReader dispatch id={capturedId} threw: {ex.Message}");
                }
            });

            if (!enqueued)
            {
                WriteResult(id, "error", "main-thread dispatcher not installed yet");
            }

            _lastProcessedId = id;
        }
        finally
        {
            doc.Dispose();
        }
    }

    private static void WriteResult(int id, string status, string message)
    {
        try
        {
            BridgePaths.EnsureDirectories();
            var payload = new
            {
                id,
                status,
                message,
                timestampUtc = DateTime.UtcNow,
                revision = BridgeSnapshotWriter.CurrentRevision,
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            AtomicFileWriter.WriteText(BridgePaths.ResultJsonPath, json);
            BridgeTrace.Log($"BridgeCommandReader wrote result id={id} status={status}");
        }
        catch (Exception ex)
        {
            BridgeTrace.Log($"BridgeCommandReader.WriteResult threw: {ex.Message}");
        }
    }
}
