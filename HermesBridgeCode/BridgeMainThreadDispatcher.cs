using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace HermesBridge.HermesBridgeCode;

/// <summary>
/// A simple thread-safe queue of <see cref="Action"/>s to be drained on the Godot
/// main (engine) thread. The actual draining happens via Harmony postfix patches on
/// frequently-called <c>_Process</c> methods of always-loaded sts2 nodes
/// (see <c>Patches/MainThreadPump*Patch.cs</c>).
///
/// We tried the more-Godot-native approach of registering a custom <c>Node</c>
/// with a <c>_Process</c> override, but the Godot C# source generators do not
/// pick up partial classes inside this mod's assembly (only one .g.cs file is
/// emitted, GlobalUsings). Without script registration, <c>_Ready</c> /
/// <c>_Process</c> are never invoked. The Harmony pump is reliable instead.
/// </summary>
internal static class BridgeMainThreadDispatcher
{
    private static readonly ConcurrentQueue<Action> _queue = new();
    private static readonly object _scheduledLock = new();
    private static readonly List<ScheduledAction> _scheduled = new();
    private static long _pumpTickCount;

    private readonly record struct ScheduledAction(long DueTick, Action Action);

    /// <summary>Initialize / no-op. Kept for symmetry with the prior API.</summary>
    public static void EnsureInstalled()
    {
        BridgeTrace.Log("BridgeMainThreadDispatcher: using Harmony pump strategy (NRun._Process / NControllerManager._Process)");
    }

    /// <summary>Queue an action to run on the next main-thread frame.</summary>
    public static bool Enqueue(Action action)
    {
        _queue.Enqueue(action);
        return true;
    }

    /// <summary>
    /// Queue an action to run after N main-thread pump ticks. Useful when a
    /// command launches async game work that mutates state one or two frames
    /// later (potions, treasure selection follow-through, etc).
    /// </summary>
    public static bool EnqueueAfterPumpTicks(int ticks, Action action)
    {
        if (ticks <= 0)
        {
            return Enqueue(action);
        }

        var dueTick = Interlocked.Read(ref _pumpTickCount) + ticks;
        lock (_scheduledLock)
        {
            _scheduled.Add(new ScheduledAction(dueTick, action));
        }
        return true;
    }

    /// <summary>
    /// Drain up to N queued actions. Called by Harmony postfix patches on each frame.
    /// MUST run on the Godot main thread.
    /// </summary>
    public static void Pump()
    {
        _pumpTickCount++;
        if (_pumpTickCount == 1)
        {
            BridgeTrace.Log("BridgeMainThreadDispatcher.Pump first tick");
        }

        List<Action>? dueActions = null;
        lock (_scheduledLock)
        {
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                var scheduled = _scheduled[i];
                if (scheduled.DueTick > _pumpTickCount) continue;
                dueActions ??= new List<Action>();
                dueActions.Add(scheduled.Action);
                _scheduled.RemoveAt(i);
            }
        }

        if (dueActions is not null)
        {
            foreach (var action in dueActions)
            {
                _queue.Enqueue(action);
            }
        }

        for (var i = 0; i < 16; i++)
        {
            if (!_queue.TryDequeue(out var action)) break;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                BridgeTrace.Log($"BridgeMainThreadDispatcher action threw: {ex.Message}");
            }
        }
    }
}
