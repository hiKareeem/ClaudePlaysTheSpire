using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayOpened))]
internal static class RewardsScreenOpenedPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NRewardsScreen.AfterOverlayOpened postfix fired");
        BridgeSnapshotWriter.SetScreen("Rewards", "RewardsScreenOpened");
    }
}
