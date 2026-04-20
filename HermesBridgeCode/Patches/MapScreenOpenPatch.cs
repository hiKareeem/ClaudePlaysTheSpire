using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
internal static class MapScreenOpenPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NMapScreen.Open postfix fired");
        BridgeSnapshotWriter.ClearTransientPayloads("MapScreenOpen");
        BridgeSnapshotWriter.SetScreen("Map", "MapScreenOpen");
        BridgeSingleton.PushCurrentMap("MapScreenOpen");
    }
}
