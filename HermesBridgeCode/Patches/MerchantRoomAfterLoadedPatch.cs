using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HermesBridge.HermesBridgeCode.Patches;

/// <summary>
/// Called after the MerchantRoom finishes loading — inventory is populated
/// here, so this is a better moment to snapshot than <c>_Ready</c> alone.
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class MerchantRoomAfterLoadedPatch
{
    public static void Postfix(NMerchantRoom __instance)
    {
        BridgeTrace.Log("NMerchantRoom.AfterRoomIsLoaded postfix fired");
        BridgeSingleton.PushCurrentShop("MerchantRoomAfterLoaded");
    }
}
