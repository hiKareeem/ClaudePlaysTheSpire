using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen._Ready))]
internal static class CardRewardSelectionScreenReadyPatch
{
    public static void Postfix(NCardRewardSelectionScreen __instance)
    {
        BridgeTrace.Log("CardRewardSelectionScreen _Ready postfix fired");
        // Always refresh LastScreen to the most recently constructed instance so we
        // never invoke SelectCard against a disposed/ghost screen left from a prior
        // open/close cycle (or a duplicate overlay spawned by a manual user click).
        CardRewardRefreshOptionsPatch.LastScreen = __instance;
        BridgeSnapshotWriter.SetScreen("CardReward", "CardRewardSelectionScreenReady");
    }
}
