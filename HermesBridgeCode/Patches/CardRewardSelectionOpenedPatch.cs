using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayOpened))]
internal static class CardRewardSelectionOpenedPatch
{
    public static void Postfix(NCardRewardSelectionScreen __instance)
    {
        BridgeTrace.Log("NCardRewardSelectionScreen.AfterOverlayOpened postfix fired");
        CardRewardRefreshOptionsPatch.LastScreen = __instance;
        BridgeSnapshotWriter.SetScreen("CardReward", "CardRewardSelectionOpened");
    }
}
