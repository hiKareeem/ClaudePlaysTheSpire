using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HermesBridge.HermesBridgeCode.Patches;

[HarmonyPatch(typeof(NSingleplayerSubmenu), "OpenCharacterSelect")]
internal static class SingleplayerOpenCharacterSelectPatch
{
    public static void Postfix()
    {
        BridgeTrace.Log("NSingleplayerSubmenu.OpenCharacterSelect postfix fired");
        BridgeSnapshotWriter.SetScreen("CharacterSelect", "SingleplayerOpenCharacterSelect");
    }
}
