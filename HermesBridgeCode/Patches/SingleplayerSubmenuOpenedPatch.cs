using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NSingleplayerSubmenu), nameof(NSingleplayerSubmenu.OnSubmenuOpened))]
internal static class SingleplayerSubmenuOpenedPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NSingleplayerSubmenu.OnSubmenuOpened postfix fired");
        BridgeSnapshotWriter.SetScreen("SingleplayerSubmenu", "SingleplayerSubmenuOpened");
    }
}
