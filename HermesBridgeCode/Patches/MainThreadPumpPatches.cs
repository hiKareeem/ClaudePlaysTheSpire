using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Drains the <see cref="BridgeMainThreadDispatcher"/> action queue on every frame
/// of <see cref="NRun._Process"/>. NRun is the central run controller node and
/// guaranteed to tick once per frame whenever a run is active.
/// </summary>
[HarmonyPatch(typeof(NRun), "_Process")]
internal static class MainThreadPumpFromNRunPatch
{
    public static void Postfix() => BridgeMainThreadDispatcher.Pump();
}

/// <summary>
/// Backup pump on <see cref="NControllerManager._Process"/>. NControllerManager is
/// always-on UI plumbing; it ticks every frame even on the main menu / before
/// any run is loaded. Together with <see cref="MainThreadPumpFromNRunPatch"/> this
/// guarantees the queue drains regardless of game phase.
/// </summary>
[HarmonyPatch(typeof(NControllerManager), "_Process")]
internal static class MainThreadPumpFromControllerManagerPatch
{
    public static void Postfix() => BridgeMainThreadDispatcher.Pump();
}
