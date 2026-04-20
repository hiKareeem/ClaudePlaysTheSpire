using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class MainMenuReadyPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("MainMenu _Ready postfix fired");
        BridgeSnapshotWriter.ClearTransientPayloads("MainMenuReady");
        BridgeSnapshotWriter.SetScreen("MainMenu", "MainMenuReady");
    }
}
