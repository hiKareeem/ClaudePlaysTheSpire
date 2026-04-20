using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
internal static class RestSiteRoomReadyPatch
{
    public static void Postfix(NRestSiteRoom __instance)
    {
        BridgeTrace.Log("NRestSiteRoom._Ready postfix fired");
        BridgeSnapshotWriter.SetScreen("RestSite", "RestSiteRoomReady");
        BridgeSingleton.PushCurrentRestSite("RestSiteRoomReady");
    }
}
