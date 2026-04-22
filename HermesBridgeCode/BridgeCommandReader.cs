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
                    //
                    // EXCEPTION: PlayCard. TryManualPlay returns synchronously when the play is
                    // queued, but the card does not actually leave Hand.Cards until a later
                    // main-loop tick (AfterCardPlayed / AfterCardPlayedLate, typically ~500-700ms
                    // later). A PostDispatch:PlayCard snapshot here captures pre-settled state:
                    // the just-played card is still in the hand array, with stale handIndex
                    // values. Clients using Wait-Revision then see this stale snapshot as the
                    // "post-command" state and issue a follow-up PlayCard against a handIndex
                    // that is about to become out-of-range. On ok==true we let the natural
                    // AfterCardPlayed hook produce the first post-dispatch revision bump; on
                    // error (no hook fires), result.json still delivers the error synchronously
                    // and Wait-Revision will (eventually) bump on the next unrelated tick.
                    //
                    // Rw6 EXCEPTION-TO-THE-EXCEPTION: if a hand-select modal is active (e.g.
                    // the player just cast a previous card like Armaments that opened an
                    // UpgradeSelect, or a discard-N-cards prompt is up), the game's combat
                    // action queue is BLOCKED waiting for the modal to resolve. In that state
                    // AfterCardPlayed will never fire for this PlayCard (TryManualPlay may even
                    // return true and silently enqueue against a blocked queue), so skipping
                    // the PostDispatch write would leave Wait-Revision spinning for the full
                    // 30s timeout before returning stalled=true with unchanged state. In that
                    // case we DO emit a PostDispatch write so clients can observe the modal
                    // and recover (clients should pre-check state.handSelect.active before
                    // calling PlayCard, but this guard prevents the 30s hang if they don't).
                    bool skipPostDispatchWrite = capturedType == "PlayCard" && status == "ok"
                        && !Patches.NPlayerHandSelectPatchState.Active;
                    if (!skipPostDispatchWrite)
                    {
                        try { BridgeSnapshotWriter.RequestWrite($"PostDispatch:{capturedType}"); }
                        catch (Exception wex) { BridgeTrace.Log($"post-dispatch RequestWrite threw: {wex.Message}"); }
                    }
                    else
                    {
                        BridgeTrace.Log($"PostDispatch write skipped for PlayCard (awaiting AfterCardPlayed for settled hand; handSelect.Active={Patches.NPlayerHandSelectPatchState.Active})");
                    }
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
