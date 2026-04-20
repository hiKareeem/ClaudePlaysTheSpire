using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayShown))]
internal static class RewardsScreenShownPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NRewardsScreen.AfterOverlayShown postfix fired");
        BridgeSnapshotWriter.SetScreen("Rewards", "RewardsScreenShown");
    }
}
