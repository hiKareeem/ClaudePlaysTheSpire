using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayClosed))]
internal static class CardRewardSelectionClosedPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NCardRewardSelectionScreen.AfterOverlayClosed postfix fired");
        BridgeSnapshotWriter.SetCardRewardOptions(null, "CardRewardSelectionClosed-ClearPayload");
        BridgeSnapshotWriter.SetScreen("CardRewardClosed", "CardRewardSelectionClosed");
    }
}
