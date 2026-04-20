using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NMerchantRoom), "_Ready")]
internal static class MerchantRoomReadyPatch
{
    public static void Postfix(NMerchantRoom __instance)
    {
        BridgeTrace.Log("NMerchantRoom._Ready postfix fired");
        BridgeSnapshotWriter.SetScreen("Shop", "MerchantRoomReady");
        BridgeSingleton.PushCurrentShop("MerchantRoomReady");
    }
}
