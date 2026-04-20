using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayShown))]
internal static class CardRewardSelectionShownPatch
{
    public static void Postfix(NCardRewardSelectionScreen __instance)
    {
        BridgeTrace.Log("NCardRewardSelectionScreen.AfterOverlayShown postfix fired");
        // The overlay can close+reshow without a fresh RefreshOptions call (game
        // animation cycle, or a duplicate overlay opened manually). Always re-cache
        // the live screen instance here so the dispatcher targets whatever is
        // actually visible/usable.
        CardRewardRefreshOptionsPatch.LastScreen = __instance;
        BridgeSnapshotWriter.SetScreen("CardReward", "CardRewardSelectionShown");
    }
}
