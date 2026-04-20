using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Captures the live <see cref="NTreasureRoom"/> Godot node when its
/// <c>_Ready</c> fires, so command dispatchers (OpenChest, SelectTreasureRelic,
/// Proceed) and the treasure extractor can read its transient UI state.
/// Also pushes a screen update so Hermes sees the room transition.
/// </summary>
[HarmonyPatch(typeof(NTreasureRoom), "_Ready")]
internal static class TreasureRoomReadyPatch
{
    public static void Postfix(NTreasureRoom __instance)
    {
        BridgeTrace.Log("NTreasureRoom._Ready postfix fired");
        BridgeSnapshotWriter.SetScreen("Treasure", "TreasureRoomReady");
        BridgeSingleton.SetCurrentTreasureNode(__instance, "TreasureRoomReady");
    }
}
