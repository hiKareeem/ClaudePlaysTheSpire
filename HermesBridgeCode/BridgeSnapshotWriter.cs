using System;
using System.IO;
using System.Text.Json;

namespace HermesBridge.HermesBridgeCode;

internal static class BridgeSnapshotWriter
{
    private static int _revision;
    private static int _stateVersion;

    /// <summary>Current revision counter (last successfully written snapshot).</summary>
    public static int CurrentRevision => _revision;

    /// <summary>
    /// Agent-facing monotonic state-change counter. Increments on every
    /// successful write that produced a new JSON payload (skip-on-unchanged
    /// writes do not advance it). Exposed in state.json as `stateVersion`
    /// per protocol-v1 §Bridge changes #1. Distinct from <see cref="CurrentRevision"/>
    /// in contract even though they currently track the same write events.
    /// </summary>
    public static int CurrentStateVersion => _stateVersion;

    /// <summary>Current coarse screen label that will be emitted in the next snapshot.</summary>
    public static string? CurrentScreen => _currentScreen;

    private static string? _lastJson;
    private static string? _currentScreen;

    // Typed payload slots. When a screen exits, the caller should clear
    // the slot by passing null to the matching setter.
    private static object? _rewards;
    private static object? _cardRewardOptions;
    private static object? _combat;
    private static object? _run;
    private static object? _event;
    private static object? _shop;
    private static object? _restSite;
    private static object? _treasure;
    private static object? _map;
    private static object? _cardGrid;
    private static object? _chooseACardScreen;
    private static object? _handSelect;

    public static void SetScreen(string screen, string trigger)
    {
        BridgeTrace.Log($"SetScreen screen={screen} trigger={trigger}");
        _currentScreen = screen;
        RequestWrite(trigger);
    }

    public static void SetRewards(object? rewards, string trigger)
    {
        _rewards = rewards;
        RequestWrite(trigger);
    }

    public static void SetCardRewardOptions(object? options, string trigger)
    {
        _cardRewardOptions = options;
        RequestWrite(trigger);
    }

    public static void SetCombat(object? combat, string trigger)
    {
        _combat = combat;
        RequestWrite(trigger);
    }

    public static void SetRun(object? run, string trigger)
    {
        _run = run;
        RequestWrite(trigger);
    }

    public static void SetEvent(object? ev, string trigger)
    {
        _event = ev;
        RequestWrite(trigger);
    }

    public static void SetShop(object? shop, string trigger)
    {
        _shop = shop;
        RequestWrite(trigger);
    }

    public static void SetRestSite(object? restSite, string trigger)
    {
        _restSite = restSite;
        RequestWrite(trigger);
    }

    public static void SetTreasure(object? treasure, string trigger)
    {
        _treasure = treasure;
        RequestWrite(trigger);
    }

    public static void SetMap(object? map, string trigger)
    {
        _map = map;
        RequestWrite(trigger);
    }

    public static void SetCardGrid(object? cardGrid, string trigger)
    {
        _cardGrid = cardGrid;
        RequestWrite(trigger);
    }

    public static void SetChooseACardScreen(object? chooseACardScreen, string trigger)
    {
        _chooseACardScreen = chooseACardScreen;
        RequestWrite(trigger);
    }

    public static void SetHandSelect(object? handSelect, string trigger)
    {
        _handSelect = handSelect;
        RequestWrite(trigger);
    }

    public static void ClearTransientPayloads(string reason)
    {
        _rewards = null;
        _cardRewardOptions = null;
        _combat = null;
        _event = null;
        _shop = null;
        _restSite = null;
        _treasure = null;
        _map = null;
        _cardGrid = null;
        _chooseACardScreen = null;
        _handSelect = null;
        BridgeTrace.Log($"ClearTransientPayloads reason={reason}");
    }

    public static void RequestWrite(string trigger)
    {
        BridgeTrace.Log($"RequestWrite trigger={trigger} currentScreen={_currentScreen ?? "<null>"}");

        try
        {
            BridgePaths.EnsureDirectories();

            // Both counters advance on every RequestWrite call (matching
            // pre-existing `revision` semantics). The unchanged-payload skip
            // below is effectively never triggered because both counters are
            // baked into the JSON before comparison; preserved here only as
            // historical defensive code. Agent harnesses (autopilot-lib.ps1)
            // poll `revision -gt $afterRevision` to detect state updates, so
            // the contract is "advance per RequestWrite, regardless of
            // payload diff". stateVersion is the v1 protocol's name for the
            // same monotonic counter, exposed under a stable contract key.
            var payload = new
            {
                schemaVersion = 1,
                stateVersion = ++_stateVersion,
                modVersion = MainFile.BridgeVersion,
                timestampUtc = DateTime.UtcNow,
                revision = ++_revision,
                source = new { modId = MainFile.ModId, trigger },
                screen = new
                {
                    name = _currentScreen,
                },
                rewards = _rewards,
                cardRewardOptions = _cardRewardOptions,
                combat = _combat,
                run = _run,
                @event = _event,
                shop = _shop,
                restSite = _restSite,
                treasure = _treasure,
                map = _map,
                cardGrid = _cardGrid,
                chooseACardScreen = _chooseACardScreen,
                handSelect = _handSelect,
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (json == _lastJson)
            {
                BridgeTrace.Log($"SkipWrite trigger={trigger} reason=unchanged-json");
                return;
            }

            AtomicFileWriter.WriteText(BridgePaths.StateJsonPath, json);
            _lastJson = json;
            BridgeTrace.Log($"WroteState revision={_revision} trigger={trigger} screen={_currentScreen ?? "<null>"}");
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(BridgePaths.ErrorPath, ex.ToString());
            }
            catch
            {
            }

            MainFile.Logger.Error(ex.ToString());
        }
    }
}
