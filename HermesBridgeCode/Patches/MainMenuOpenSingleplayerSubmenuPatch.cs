using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.OpenSingleplayerSubmenu))]
internal static class MainMenuOpenSingleplayerSubmenuPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NMainMenu.OpenSingleplayerSubmenu postfix fired");
        BridgeSnapshotWriter.SetScreen("SingleplayerSubmenu", "MainMenuOpenSingleplayerSubmenu");
    }
}
